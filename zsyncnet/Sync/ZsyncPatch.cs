using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using NLog;
using zsyncnet.Control;
using zsyncnet.Hash;
using zsyncnet.Util;

namespace zsyncnet.Sync
{
    internal static class ZsyncPatch
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void Patch(List<Stream> seeds, ControlFile cf, IRangeDownloader downloader, Stream output, IProgress<ulong> progress = null, CancellationToken cancellationToken = default)
        {
            var header = cf.GetHeader();

            var remoteBlockSums = cf.GetBlockSums();

            Logger.Trace($"Building checksum table");
            var checksumTable = new CheckSumTable(remoteBlockSums);

            Logger.Info($"Comparing files...");

            // TODO: should we copy right when we find a block, instead of having to remember which stream it came from?
            var existingBlocks = new Dictionary<int, (Stream source, long offset)>();

            foreach (var seed in seeds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FindExistingBlocks(seed, existingBlocks, header, checksumTable, false, cancellationToken);
            }

            // output stream needs to prevent overwriting itself, so has more restrictions -> can use less optimizations
            //  it should therefore run last, to use as many existing blocks from other seeds as possible
            FindExistingBlocks(output, existingBlocks, header, checksumTable, true, cancellationToken);

            Logger.Info($"Total existing blocks {existingBlocks.Count}");

            var singleBlockSyncOps = BuildSyncOps(header.Length, header.BlockSize, existingBlocks);

            // TODO: we should probably combine copies as well
            var syncOps = CombineDownloads(singleBlockSyncOps);

            var copyBuffer = new byte[header.BlockSize];

            ulong done = 0;

            var blockIndex = 0;
            foreach (var syncOp in syncOps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (syncOp.IsLocal)
                {
                    var input = syncOp.Source;
                    input.Position = syncOp.LocalOffset;
                    var length = header.BlockSize;
                    if (syncOp.LocalOffset + length > input.Length)
                        length = (int)(input.Length - syncOp.LocalOffset);
                    input.Read(copyBuffer, 0, length);
                    // input and output stream might be the same -> need to set the position
                    output.Position = blockIndex * header.BlockSize;
                    output.Write(copyBuffer, 0, length);
                    done += (ulong)length;
                    blockIndex++;
                }
                else
                {
                    output.Position = blockIndex * header.BlockSize;
                    var from = syncOp.BlockIndex * (long)header.BlockSize;
                    var to = (syncOp.BlockIndex + syncOp.BlockCount) * (long)header.BlockSize;
                    if (to > header.Length) to = header.Length;
                    var content = downloader.DownloadRange(from, to);
                    content.CopyTo(output);
                    done += (ulong)(to - from);
                    blockIndex += syncOp.BlockCount;
                }
                progress?.Report(done);
            }

            output.Flush();

            Logger.Info("Verifying file");

            cancellationToken.ThrowIfCancellationRequested();
            if (!VerifyFile(output, header.Sha1))
                throw new Exception("Verification failed");
        }

        private static bool VerifyFile(Stream stream, string checksum)
        {
            stream.Position = 0;
            using var crypto = new SHA1CryptoServiceProvider();
            var hash = crypto.ComputeHash(stream).ToHex();
            return hash == checksum;
        }

        private static List<SyncOperation> BuildSyncOps(long fileSize, int blockSize,
            Dictionary<int, (Stream source, long offset)> existingBlocks)
        {
            var result = new List<SyncOperation>();

            var totalBlockCount = (int)(fileSize / blockSize);
            if (fileSize % blockSize > 0) totalBlockCount++;

            // for every block that we need, check if we have a local copy. otherwise download
            for (int i = 0; i < totalBlockCount; i++)
            {
                result.Add(existingBlocks.TryGetValue(i, out var localInfo)
                    ? new SyncOperation(i, 1, true, localInfo.offset, localInfo.source)
                    : new SyncOperation(i, 1, false));
            }

            return result;
        }


        private static List<SyncOperation> CombineDownloads(List<SyncOperation> syncOperations)
        {
            // combine consecutive download operations

            if (!syncOperations.Any()) return new List<SyncOperation>();

            var result = new List<SyncOperation>();

            var current = syncOperations[0];
            foreach (var operation in syncOperations.Skip(1))
            {
                if (operation.IsLocal || current.IsLocal)
                {
                    result.Add(current);
                    current = operation;
                    continue;
                }
                current = current with { BlockCount = current.BlockCount + 1 };
            }
            result.Add(current);

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns>Dict remoteBlockIndex -> localOffset</returns>
        private static void FindExistingBlocks(Stream stream, Dictionary<int, (Stream source, long offset)> result,
            Header header, CheckSumTable remoteBlockSums, bool isOutputStream, CancellationToken cancellationToken)
        {
            if (stream.Length < header.BlockSize * 2) return;

            if (header.SequenceMatches is < 1 or > 2)
                throw new NotSupportedException();

            var start = DateTime.Now;

            const long sectionSize = 10 * 1024 * 1024; // read ~10mb at a time
            var totalLength = stream.Length;

            var buffer = new byte[sectionSize + header.BlockSize];

            for (long offset = 0; offset < totalLength; offset += sectionSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                stream.Position = offset;

                // read two blocksizes into the next section to have overlapping slices
                var length = sectionSize + 2 * header.BlockSize;
                if (offset + length <= totalLength) // somewhere in the middle of the file. can just read.
                {
                    stream.Read(buffer);
                }
                else
                {
                    // at the end of the file
                    // round up to next block border and pad
                    length = totalLength - offset;
                    var blockCount = length / header.BlockSize;
                    if (length % header.BlockSize > 0) blockCount++;
                    length = blockCount * header.BlockSize;

                    buffer = new byte[length];
                    Array.Fill<byte>(buffer, 0); // pad with zeroes
                    stream.Read(buffer, 0, (int)(totalLength - offset));
                }

                FindExistingBlocks(buffer, offset, header, remoteBlockSums, result, stream, isOutputStream);
            }

            Logger.Info($"Done in {(DateTime.Now-start).TotalSeconds:F2}");
        }


        private static void FindExistingBlocks(byte[] buffer, long bufferOffset, Header header,
            CheckSumTable remoteBlockSums, Dictionary<int, (Stream source, long offset)> result, Stream source,
            bool isOutputStream)
        {
            var rollingChecksum = new RollingChecksum(buffer, header.BlockSize, header.WeakChecksumLength);

            // if we find a block, we only check at the next possible block location. keep track of this here.
            var earliest = header.BlockSize;

            // allocate buffers
            var md4Hash = new byte[16];
            var previousMd4Hash = new byte[16];
            var md4Hasher = new Md4(header.BlockSize);

            // rolling buffer for one rsum hashes one blocksize behind the read-head. needed for sequence checks,
            //  and faster then keeping two rolling checksums around
            var oldRsums = new uint[header.BlockSize];

            // feed the rolling checksum with one block.
            // after this, the rolling checksum is ready to read block number three
            for (int i = 0; i < header.BlockSize; i++)
            {
                oldRsums[i] = rollingChecksum.Current;
                rollingChecksum.Next();
            }

            for (int i = 2 * header.BlockSize; i <= buffer.Length; i++)
            {
                var previousRSum = oldRsums[i % header.BlockSize];
                var rSum = rollingChecksum.Current;

                // keep oldRsums updated
                oldRsums[i % header.BlockSize] = rSum;

                // don't get a new one past EOF
                if (i < buffer.Length) rollingChecksum.Next();

                // we are in an active block. skip past it, ahead to the next earliest possible block index
                if (i < earliest) continue;

                // try to find an rsum match
                if (!remoteBlockSums.TryGetValue(rSum, out var blocks)) continue;

                // keep hashes lazy
                var hashed = false;
                var hashedPrevious = false;

                // check all possibly matching block for this rum
                foreach (var (expectedPreviousRSum, md4, previousMd4, remoteBlockIndex) in blocks)
                {
                    if (result.ContainsKey(remoteBlockIndex)) continue; // we already have a source for that block.

                    // when using sequence matches, we check the previous rsum as well. this allows smaller rsum sizes.
                    if (header.SequenceMatches == 2 && previousRSum != expectedPreviousRSum) continue;

                    if (!hashed)
                    {
                        md4Hasher.Hash(buffer, i - header.BlockSize, md4Hash);
                        hashed = true;
                    }

                    if (!HashEqual(md4, md4Hash)) continue;

                    // earliest index at which a new block can start
                    earliest = i + header.BlockSize;

                    // if this seed is also the output stream, we can't copy blocks from the start of the file to the end,
                    //  as they would have been overwritten by the time we need them.
                    // We didn't check before hashing, because it's still a valid block for the purpose of skipping to a new block.
                    if (isOutputStream && remoteBlockIndex * header.BlockSize > i - header.BlockSize + bufferOffset)
                        continue;

                    result.Add(remoteBlockIndex, (source, i - header.BlockSize + bufferOffset));

                    // previous one might have been the start of a sequence and not been added yet
                    if (remoteBlockIndex > 0 && !result.ContainsKey(remoteBlockIndex - 1))
                    {
                        if (!hashedPrevious)
                        {
                            md4Hasher.Hash(buffer, i - 2 * header.BlockSize, previousMd4Hash);
                            hashedPrevious = true;
                        }

                        if (HashEqual(previousMd4, previousMd4Hash))
                        {
                            result.Add(remoteBlockIndex - 1, (source, i - 2 * header.BlockSize + bufferOffset));
                        }
                    }

                }
            }
        }

        private static bool HashEqual(byte[] a, byte[] b)
        {
            var length = a.Length;
            if (b.Length < length) length = b.Length;
            for (int i = 0; i < length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private record RemoteBlock(uint PreviousRSum, byte[] Hash, byte[] PreviousHash, int BlockIndex);

        private class CheckSumTable : Dictionary<uint, List<RemoteBlock>>
        {
            public CheckSumTable(IEnumerable<BlockSum> blockSums)
            {
                uint lastRSum = 0;
                byte[] previousHash = null;
                foreach (var blockSum in blockSums)
                {
                    if (!TryGetValue(blockSum.Rsum, out var bin))
                    {
                        bin = new List<RemoteBlock>();
                        Add(blockSum.Rsum, bin);
                    }

                    bin.Add(new RemoteBlock(lastRSum, blockSum.Checksum, previousHash, blockSum.BlockStart));

                    lastRSum = blockSum.Rsum;
                    previousHash = blockSum.Checksum;
                }
            }
        }
    }
}

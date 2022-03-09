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
            if (seeds.Contains(output))
                throw new ArgumentException("seeds must not include the working/output stream");

            var header = cf.GetHeader();

            // remember data size, so that we don't scan past useful input
            var existingOutputSize = output.Length;

            // before we have done any checking, assume that all of the file contains useful data
            var writtenLength = output.Length;

            // set length here to reserve space.
            if (output.Length < header.Length)
                output.SetLength(header.Length);

            var remoteBlockSums = cf.GetBlockSums();

            Logger.Trace($"Building checksum table");
            var checksumTable = new CheckSumTable(remoteBlockSums);

            Logger.Trace($"Comparing files...");

            // block indices for which we found a local source
            var existingBlocks = new HashSet<int>();

            // we'll be writing to this stream, so handle it first, before we overwrite anything useful
            // .part should also only have useful data, so it doesn't hurt do check first
            try
            {
                FindExistingBlocks(output, output, existingOutputSize, existingBlocks, header, checksumTable, progress, cancellationToken);

                foreach (var seed in seeds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FindExistingBlocks(output, seed, seed.Length, existingBlocks, header, checksumTable, progress, cancellationToken);
                }

                // we checked all local data now and copied it into the output. if there was any data beyond that, it was junk
                writtenLength = existingBlocks.Any() ? (existingBlocks.Max() + 1) * header.BlockSize : 0;

                Logger.Debug($"Total existing blocks {existingBlocks.Count}");

                var downloadRanges = BuildDownloadRanges(header.Length, header.BlockSize, existingBlocks);

                foreach (var (blockIndex, blockCount) in downloadRanges)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output.Position = blockIndex * header.BlockSize;
                    var from = blockIndex * (long)header.BlockSize;
                    var to = (blockIndex + blockCount) * (long)header.BlockSize;
                    if (to > header.Length) to = header.Length;

                    // as we download, the amount of "useful" data in the output grows
                    if (to > writtenLength) writtenLength = to;

                    var content = downloader.DownloadRange(from, to);
                    content.CopyToWithProgress(output, 1024 * 1024, progress, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Sync canceled. Shrinking output-file");
                output.SetLength(writtenLength);
                throw;
            }

            output.SetLength(header.Length);
            output.Flush();

            Logger.Debug("Verifying file");

            cancellationToken.ThrowIfCancellationRequested();
            if (!VerifyFile(output, header.Sha1))
                throw new Exception("Verification failed");
        }

        private static bool VerifyFile(Stream stream, string checksum)
        {
            stream.Position = 0;
            using var crypto = SHA1.Create();
            var hash = crypto.ComputeHash(stream).ToHex();
            return hash == checksum;
        }

        private static List<DownloadRange> BuildDownloadRanges(long fileSize, int blockSize, HashSet<int> existingBlocks)
        {
            var result = new List<DownloadRange>();

            var totalBlockCount = (int)(fileSize / blockSize);
            if (fileSize % blockSize > 0) totalBlockCount++;

            var currentIndex = -1;
            var currentLength = -1;

            for (int i = 0; i < totalBlockCount; i++)
            {
                // for every block that we need, check if we have a local copy. otherwise download
                if (existingBlocks.Contains(i))
                    continue;

                if (i == currentIndex + currentLength)
                {
                    currentLength++;
                    continue;
                }

                if (currentIndex >= 0)
                    result.Add(new DownloadRange(currentIndex, currentLength));

                currentIndex = i;
                currentLength = 1;
            }

            if (currentIndex >= 0)
                result.Add(new DownloadRange(currentIndex, currentLength));

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns>Dict remoteBlockIndex -> localOffset</returns>
        private static void FindExistingBlocks(Stream output, Stream input, long inputLength, HashSet<int> existingBlocks,
            ControlFileHeader header, CheckSumTable remoteBlockSums, IProgress<ulong> progress, CancellationToken cancellationToken)
        {
            if (inputLength < header.BlockSize * 2) return;

            if (header.SequenceMatches is < 1 or > 2)
                throw new NotSupportedException();

            var start = DateTime.Now;

            const long sectionSize = 10 * 1024 * 1024; // read ~10mb at a time

            var buffer = new byte[sectionSize + header.BlockSize];

            for (long offset = 0; offset < inputLength; offset += sectionSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                input.Position = offset;

                // read two blocksizes into the next section to have overlapping slices
                var length = sectionSize + 2 * header.BlockSize;
                if (offset + length <= inputLength) // somewhere in the middle of the file. can just read.
                {
                    input.Read(buffer);
                }
                else
                {
                    // at the end of the file
                    // round up to next block border and pad
                    length = inputLength - offset;
                    var blockCount = length / header.BlockSize;
                    if (length % header.BlockSize > 0) blockCount++;
                    length = blockCount * header.BlockSize;

                    buffer = new byte[length];
                    Array.Fill<byte>(buffer, 0); // pad with zeroes
                    input.Read(buffer, 0, (int)(inputLength - offset));
                }

                FindExistingBlocks(output, buffer, offset, header, remoteBlockSums, existingBlocks, output == input, progress);
            }

            Logger.Debug($"Finding blocks done in {(DateTime.Now-start).TotalSeconds:F2}s");
        }


        private static void FindExistingBlocks(Stream output, byte[] inputBuffer, long bufferOffset, ControlFileHeader header,
            CheckSumTable remoteBlockSums, HashSet<int> existingBlocks, bool isOutputStream,
            IProgress<ulong> progress)
        {
            var rollingChecksum = new RollingChecksum(inputBuffer, header.BlockSize, header.WeakChecksumLength);

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

            for (int i = 2 * header.BlockSize; i <= inputBuffer.Length; i++)
            {
                var previousRSum = oldRsums[i % header.BlockSize];
                var rSum = rollingChecksum.Current;

                // keep oldRsums updated
                oldRsums[i % header.BlockSize] = rSum;

                // don't get a new one past EOF
                if (i < inputBuffer.Length) rollingChecksum.Next();

                // we are in an active block. skip past it, ahead to the next earliest possible block index
                if (i < earliest) continue;

                // try to find an rsum match
                if (!remoteBlockSums.TryGetValue(rSum, out var blocks)) continue;

                // keep hashes lazy
                var hashed = false;
                var hashedPrevious = false;

                // we want to check all remote blocks that we don't have yet, and existing ones only as long as we don't have one that allows us to skip ahead
                var blocksWithoutMatch = blocks.Where(b => !existingBlocks.Contains(b.BlockIndex));
                var blocksWithMatch = blocks.Where(b => existingBlocks.Contains(b.BlockIndex));

                foreach (var (expectedPreviousRSum, md4, previousMd4, remoteBlockIndex) in blocksWithoutMatch.Concat(blocksWithMatch))
                {
                    // when using sequence matches, we check the previous rsum as well. this allows smaller rsum sizes.
                    if (header.SequenceMatches == 2 && previousRSum != expectedPreviousRSum) continue;

                    if (!hashed)
                    {
                        md4Hasher.Hash(inputBuffer, i - header.BlockSize, md4Hash);
                        hashed = true;
                    }

                    if (!HashEqual(md4, md4Hash)) continue;

                    // earliest index at which a new block can start
                    earliest = i + header.BlockSize;

                    if (existingBlocks.Contains(remoteBlockIndex))
                        break; // we just needed a reason to skip. no need to copy anything.

                    // if this seed is also the output stream, we can't copy blocks from the start of the file to the end,
                    //  as they would have been overwritten by the time we need them.
                    // We didn't check before hashing, because it's still a valid block for the purpose of skipping to a new block.
                    if (isOutputStream && remoteBlockIndex * header.BlockSize < bufferOffset)
                        continue;

                    CopyBlock(output, remoteBlockIndex, inputBuffer, i - header.BlockSize, header, progress);
                    existingBlocks.Add(remoteBlockIndex);

                    // previous one might have been the start of a sequence and not been added yet
                    if (remoteBlockIndex == 0 || existingBlocks.Contains(remoteBlockIndex - 1)) continue;

                    if (!hashedPrevious)
                    {
                        md4Hasher.Hash(inputBuffer, i - 2 * header.BlockSize, previousMd4Hash);
                        hashedPrevious = true;
                    }

                    if (!HashEqual(previousMd4, previousMd4Hash)) continue;

                    CopyBlock(output, remoteBlockIndex - 1, inputBuffer, i - 2 * header.BlockSize,
                        header, progress);
                    existingBlocks.Add(remoteBlockIndex - 1);

                }
            }
        }

        private static void CopyBlock(Stream output, int blockIndex, byte[] source, int index, ControlFileHeader header, IProgress<ulong> progress)
        {
            // TODO: don't need to copy if input == output and the position is the same (ie data is where it's supposed to be already)
            output.Position = blockIndex * header.BlockSize;
            var length = header.BlockSize;
            if (blockIndex * header.BlockSize + length > header.Length)
                length = (int)(header.Length - blockIndex * header.BlockSize);
            output.Write(source, index, length);
            progress?.Report((ulong)length);
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

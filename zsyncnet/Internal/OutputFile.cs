using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using zsyncnet.Internal.ControlFile;

namespace zsyncnet.Internal
{
    public class OutputFile
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void Patch(Stream input, zsyncnet.ControlFile cf, IRangeDownloader downloader, Stream output)
        {
            var header = cf.GetHeader();

            output ??= new MemoryStream((int)header.Length);
            output.SetLength(header.Length);

            var remoteBlockSums = cf.GetBlockSums();

            Logger.Trace($"Building checksum table");
            var checksumTable = new CheckSumTable(remoteBlockSums);

            Logger.Info($"Comparing files...");
            var existingBlocks = FindExistingBlocks(input, header, checksumTable);
            Logger.Info($"Total existing blocks {existingBlocks.Count}");

            var singleBlockSyncOps = BuildSyncOps(header.Length, header.BlockSize, existingBlocks);
            var syncOps = CombineDownloads(singleBlockSyncOps, header.BlockSize);

            var copyBuffer = new byte[header.BlockSize];

            // TODO: adjust for padding!

            foreach (var syncOp in syncOps)
            {
                if (syncOp.IsLocal)
                {
                    input.Position = syncOp.LocalOffset;
                    var length = header.BlockSize;
                    if (syncOp.LocalOffset + length > input.Length)
                        length = (int)(input.Length - syncOp.LocalOffset);
                    input.Read(copyBuffer, 0, length);
                    output.Write(copyBuffer, 0, length);
                }
                else
                {
                    var from = syncOp.BlockIndex * (long)header.BlockSize;
                    var to = (syncOp.BlockIndex + syncOp.BlockCount) * (long)header.BlockSize;
                    if (to > header.Length) to = header.Length;
                    var content = downloader.DownloadRange(from, to);
                    content.CopyTo(output);
                }
            }

            output.Flush();

            Logger.Info("Verifying file");

            if (!VerifyFile(output, header.Sha1))
                throw new Exception("Verification failed");
        }

        private static bool VerifyFile(Stream stream, string checksum)
        {
            stream.Position = 0;
            using var crypto = new SHA1CryptoServiceProvider();
            var hash = ZsyncUtil.ByteToHex(crypto.ComputeHash(stream));
            return hash == checksum;
        }

        private static List<SyncOperation> BuildSyncOps(long fileSize, int blockSize,
            Dictionary<int, long> existingBlocks)
        {
            var result = new List<SyncOperation>();

            var totalBlockCount = (int)(fileSize / blockSize);
            if (fileSize % blockSize > 0) totalBlockCount++;

            for (int i = 0; i < totalBlockCount; i++)
            {
                result.Add(existingBlocks.TryGetValue(i, out var localOffset)
                    ? new SyncOperation(i, 1, true, localOffset)
                    : new SyncOperation(i, 1, false));
            }

            return result;
        }


        private static List<SyncOperation> CombineDownloads(List<SyncOperation> syncOperations, int blockSize)
        {
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
        private static Dictionary<int, long> FindExistingBlocks(Stream existingStream, Header header, CheckSumTable remoteBlockSums)
        {
            if (existingStream.Length == 0) return new Dictionary<int, long>();

            existingStream.Position = 0;
            var result = new Dictionary<int, long>();

            var existingBlockCount = existingStream.Length / header.BlockSize;
            if (existingStream.Length % header.BlockSize != 0) existingBlockCount++;
            var existingData = new byte[existingBlockCount * header.BlockSize];
            if (existingStream.Length % header.BlockSize != 0)
            {
                // padding
                Array.Fill<byte>(existingData, 0, (int)existingBlockCount - 2, header.BlockSize);
            }

            using var memStream = new MemoryStream(existingData);
            {
                existingStream.CopyTo(memStream);
            }

            var rollingChecksum = RollingChecksum.GetRollingChecksum(existingData, header.BlockSize);

            var i = header.BlockSize;
            var md4Buffer = new byte[header.BlockSize];

            var earliest = i;

            foreach (var rSum in rollingChecksum)
            {
                i++;
                if (i - 1 < earliest) continue; // TODO: doc

                if (!remoteBlockSums.TryGetValue((ushort)rSum, out var blocks)) continue;

                Array.Copy(existingData, i - 1 - header.BlockSize, md4Buffer, 0, header.BlockSize);
                var hash = ZsyncUtil.Md4Hash(md4Buffer);
                Array.Resize(ref hash, header.StrongChecksumLength);

                foreach (var (md4, remoteBlockIndices) in blocks)
                {
                    // TODO: we could skip remote block indices that we already have a result for. could skip md4 hashing if there are none left to check.
                    if (!md4.SequenceEqual(hash)) continue;
                    foreach (var remoteBlockIndex in remoteBlockIndices)
                    {
                        if (result.ContainsKey(remoteBlockIndex)) continue;
                        result.Add(remoteBlockIndex, i - 1 - header.BlockSize);
                    }
                    earliest = i - 1 + header.BlockSize;
                    break;
                }
            }

            return result;
        }

        private class CheckSumTable : Dictionary<ushort, List<(byte[] hash, List<int> blockIndices)>>
        {
            public CheckSumTable(IEnumerable<BlockSum> blockSums)
            {
                foreach (var blockSum in blockSums)
                {
                    if (!TryGetValue(blockSum.Rsum, out var bin))
                    {
                        bin = new List<(byte[] hash, List<int> blockIndices)>();
                        Add(blockSum.Rsum, bin);
                    }

                    var (_, blockIndices) = bin.SingleOrDefault(entry => entry.hash.SequenceEqual(blockSum.Checksum));
                    if (blockIndices != null)
                        blockIndices.Add(blockSum.BlockStart);
                    else
                        bin.Add((blockSum.Checksum, new List<int> { blockSum.BlockStart }));
                }
            }
        }
    }
}

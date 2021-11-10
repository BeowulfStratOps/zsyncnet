using System;
using System.IO;
using System.Text;

namespace zsyncnet.Control
{
    internal class Header
    {
        public string Version { get; }
        public string Filename { get; }
        public DateTime MTime { get; }
        public int BlockSize { get; }
        public long Length { get; }
        public int WeakChecksumLength { get; }
        public int StrongChecksumLength { get; }
        public int SequenceMatches { get; }
        public string Url { get; }
        public string Sha1 { get; }

        /// <summary>
        /// Creates new control file
        /// </summary>
        /// <param name="version"></param>
        /// <param name="filename"></param>
        /// <param name="mTime"></param>
        /// <param name="blockSize"></param>
        /// <param name="length"></param>
        /// <param name="sequenceMatches"></param>
        /// <param name="url"></param>
        /// <param name="sha1"></param>
        public Header(string version, string filename, DateTime mTime, int blockSize, long length, int sequenceMatches, int weakChecksumLength, int strongChecksumLength ,string url, string sha1)
        {
            Version = version;
            Filename = filename;
            MTime = mTime;
            BlockSize = blockSize;
            Length = length;
            SequenceMatches = sequenceMatches;
            Url = url;
            Sha1 = sha1;
            WeakChecksumLength = weakChecksumLength;
            StrongChecksumLength = strongChecksumLength;
        }

        /// <summary>
        /// Returns the expected number of blocks for this control file based on the file size and block size
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfBlocks()
        {
            return (int)(Length + BlockSize - 1) / BlockSize;
        }
        /// <summary>
        /// Reads the header of a control file
        /// </summary>
        /// <param name="input">byte[] representing header</param>
        public Header(byte[] input)
        {
            string headerText = Encoding.ASCII.GetString(input);
            string line;
            using (var sr = new StringReader(headerText))
            {
                while (null != (line = sr.ReadLine()))
                {
                    var pair = SplitKeyValuePair(line);
                    switch (pair.Key)
                    {
                        case "zsync":
                            Version = pair.Value;
                            break;
                        case "Filename":
                            Filename = pair.Value;
                            break;
                        case "MTime":
                            MTime = DateTime.Parse(pair.Value);
                            break;
                        case "Blocksize":
                            BlockSize = Convert.ToInt32(pair.Value);
                            break;
                        case "Length":
                            Length = Convert.ToInt64(pair.Value);
                            break;
                        case "Hash-Lengths":
                            var hashLengths = SplitHashLengths(pair.Value);
                            SequenceMatches = hashLengths.SequenceMatches;
                            WeakChecksumLength = hashLengths.WeakChecksumLength;
                            StrongChecksumLength = hashLengths.StrongChecksumLength;
                            break;
                        case "URL":
                            Url = pair.Value;
                            break;
                        case "SHA-1":
                            Sha1 = pair.Value;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Splits a zsync key:value pair into its constituent parts
        /// </summary>
        /// <param name="str">String to split</param>
        /// <returns>Key, Value</returns>
        /// <exception cref="ArgumentException"></exception>
        private (string Key, string Value) SplitKeyValuePair(string str)
        {
            var split = str.Split(':',2);
            if (split.Length != 2)
            {
                throw new ArgumentException("str not a valid key:value pair");
            }

            return (split[0], split[1].Trim());
        }

        private (int SequenceMatches, int WeakChecksumLength, int StrongChecksumLength) SplitHashLengths(string str)
        {
            var split = str.Split(',', 3);
            if (split.Length != 3)
            {
                throw new ArgumentException("str not valid Hash-Lengths");
            }

            return (Convert.ToInt32(split[0]), Convert.ToInt32(split[1]), Convert.ToInt32(split[2]));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace PlayniteAchievements.Providers.Unleashed
{
    internal static class UnleashedAchievementReader
    {
        private const int HeaderLength = 16;
        private const int RecordLength = 16;
        private const int RecordCount = 50;
        internal const int ExpectedLength = HeaderLength + RecordLength * RecordCount;
        private const long MaxUnixTime = 253402300799L;

        internal static bool TryReadUnlocks(
            string path, out Dictionary<ushort, DateTime?> unlocks)
        {
            unlocks = new Dictionary<ushort, DateTime?>();
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                byte[] data = new byte[ExpectedLength];
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    if (file.Length != ExpectedLength)
                    {
                        return false;
                    }

                    int offset = 0;
                    while (offset < data.Length)
                    {
                        int count = file.Read(data, offset, data.Length - offset);
                        if (count == 0)
                        {
                            return false;
                        }
                        offset += count;
                    }
                }

                if (data[0] != 'A' || data[1] != 'C' ||
                    data[2] != 'H' || data[3] != ' ')
                {
                    return false;
                }

                if (BitConverter.ToUInt32(data, 4) > 1)
                {
                    return false;
                }

                uint checksum = 0;
                for (int i = HeaderLength; i < data.Length; i++)
                {
                    checksum ^= data[i];
                }
                if (BitConverter.ToUInt32(data, 8) != checksum)
                {
                    return false;
                }

                var entries = new Dictionary<ushort, DateTime?>();
                for (int i = 0; i < RecordCount; i++)
                {
                    int start = HeaderLength + i * RecordLength;
                    ushort id = BitConverter.ToUInt16(data, start);
                    if (id == 0)
                    {
                        break;
                    }

                    long seconds = BitConverter.ToInt64(data, start + 2);
                    DateTime? timestamp = seconds > 0 && seconds <= MaxUnixTime
                        ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                        : null;
                    if (entries.ContainsKey(id))
                    {
                        return false;
                    }
                    entries.Add(id, timestamp);
                }

                unlocks = entries;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static string FindSavePath(string gameRoot, string roamingData)
        {
            var bases = new List<string>();
            if (!string.IsNullOrWhiteSpace(gameRoot) &&
                File.Exists(Path.Combine(gameRoot, "portable.txt")))
            {
                bases.Add(gameRoot);
            }

            if (!string.IsNullOrWhiteSpace(roamingData))
            {
                bases.Add(Path.Combine(roamingData, "UnleashedRecomp"));
            }

            foreach (var folder in bases)
            {
                foreach (var subdir in new[] { "mlsave", "save" })
                {
                    var candidate = Path.Combine(folder, subdir, "ACH-DATA");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }
    }
}

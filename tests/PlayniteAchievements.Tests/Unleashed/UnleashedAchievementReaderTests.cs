using Microsoft.VisualStudio.TestTools.UnitTesting;
using PlayniteAchievements.Providers.Unleashed;
using System;
using System.IO;

namespace PlayniteAchievements.Tests.Unleashed
{
    [TestClass]
    public sealed class UnleashedAchievementReaderTests
    {
        [TestMethod]
        public void ReadsValidRecordsAndTimestamps()
        {
            var path = Path.GetTempFileName();
            try
            {
                var data = NewData();
                BitConverter.GetBytes((ushort)24).CopyTo(data, 16);
                var date = new DateTime(2026, 10, 30, 12, 30, 0, DateTimeKind.Utc);
                var unix = new DateTimeOffset(date).ToUnixTimeSeconds();
                BitConverter.GetBytes(unix).CopyTo(data, 18);
                BitConverter.GetBytes((ushort)83).CopyTo(data, 32);
                SetChecksum(data);
                File.WriteAllBytes(path, data);

                Assert.IsTrue(UnleashedAchievementReader.TryReadUnlocks(
                    path, out var unlocked));
                Assert.AreEqual(2, unlocked.Count);
                Assert.AreEqual(date, unlocked[24]);
                Assert.IsNull(unlocked[83]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void AcceptsEmptyDataAndRejectsInvalidHeaders()
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = NewData();
                File.WriteAllBytes(path, bytes);
                Assert.IsTrue(UnleashedAchievementReader.TryReadUnlocks(
                    path, out var unlocked));
                Assert.AreEqual(0, unlocked.Count);

                bytes[0] = (byte)'X';
                File.WriteAllBytes(path, bytes);
                Assert.IsFalse(UnleashedAchievementReader.TryReadUnlocks(path, out _));

                bytes = NewData();
                BitConverter.GetBytes(2u).CopyTo(bytes, 4);
                File.WriteAllBytes(path, bytes);
                Assert.IsFalse(UnleashedAchievementReader.TryReadUnlocks(path, out _));

                bytes = NewData();
                bytes[16] = 24;
                File.WriteAllBytes(path, bytes);
                Assert.IsFalse(UnleashedAchievementReader.TryReadUnlocks(path, out _));

                File.WriteAllBytes(path, new byte[16]);
                Assert.IsFalse(UnleashedAchievementReader.TryReadUnlocks(path, out _));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void FindsModSaveWithoutChangingGameFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var roaming = Path.Combine(root, "roaming");
            var modFolder = Path.Combine(roaming, "UnleashedRecomp", "mlsave");
            var game = Path.Combine(root, "game");
            try
            {
                Directory.CreateDirectory(modFolder);
                Directory.CreateDirectory(game);
                var path = Path.Combine(modFolder, "ACH-DATA");
                File.WriteAllBytes(path, NewData());
                Assert.AreEqual(path,
                    UnleashedAchievementReader.FindSavePath(game, roaming));
                File.WriteAllText(Path.Combine(game, "portable.txt"), string.Empty);
                var portable = Path.Combine(game, "save");
                Directory.CreateDirectory(portable);
                var file = Path.Combine(portable, "ACH-DATA");
                File.WriteAllBytes(file, NewData());
                Assert.AreEqual(file,
                    UnleashedAchievementReader.FindSavePath(game, roaming));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static byte[] NewData()
        {
            var bytes = new byte[UnleashedAchievementReader.ExpectedLength];
            bytes[0] = (byte)'A';
            bytes[1] = (byte)'C';
            bytes[2] = (byte)'H';
            bytes[3] = (byte)' ';
            BitConverter.GetBytes(1u).CopyTo(bytes, 4);
            return bytes;
        }

        private static void SetChecksum(byte[] bytes)
        {
            uint value = 0;
            for (int i = 16; i < bytes.Length; i++)
            {
                value ^= bytes[i];
            }
            BitConverter.GetBytes(value).CopyTo(bytes, 8);
        }
    }
}

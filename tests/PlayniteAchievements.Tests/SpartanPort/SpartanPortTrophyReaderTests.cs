using Microsoft.VisualStudio.TestTools.UnitTesting;
using PlayniteAchievements.Providers.SpartanPort;
using System;
using System.IO;
using System.Linq;

namespace PlayniteAchievements.Tests.SpartanPort
{
    [TestClass]
    public sealed class SpartanPortTrophyReaderTests
    {
        [TestMethod]
        public void ReadsAllFlagWordsAndIgnoresUnusedBits()
        {
            var path = Path.GetTempFileName();
            try
            {
                var bytes = new byte[16];
                foreach (var id in new[] { 0, 1, 31, 32, 34, 127 })
                {
                    bytes[id / 8] |= (byte)(1 << (id % 8));
                }

                File.WriteAllBytes(path, bytes);
                Assert.IsTrue(SpartanPortTrophyReader.TryReadUnlocks(path, out var unlocked));
                CollectionAssert.AreEquivalent(new[] { 0, 1, 31, 32, 34 }, unlocked.ToList());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void MissingOrIncompleteSaveIsNotTreatedAsZeroUnlocks()
        {
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "trophies.dat");
            Assert.IsFalse(SpartanPortTrophyReader.TryReadUnlocks(missing, out _));

            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, new byte[8]);
                Assert.IsFalse(SpartanPortTrophyReader.TryReadUnlocks(path, out _));
                File.WriteAllBytes(path, new byte[16]);
                Assert.IsTrue(SpartanPortTrophyReader.TryReadUnlocks(path, out var unlocked));
                Assert.AreEqual(0, unlocked.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ExtractsOnlyValidOriginalPngEntries()
        {
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/i/8AAAAASUVORK5CYII=");
            var trp = new byte[0x300];
            trp[0] = 0xDC;
            trp[1] = 0xA2;
            trp[2] = 0x4D;
            trp[3] = 0x00;
            trp[0x13] = 2;
            trp[0x17] = 0x30;
            WriteEntry(trp, 0x60, "TROP000.PNG", 0x120, png);
            WriteEntry(trp, 0x90, "TROP034.PNG", 0x200, png);

            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, trp);
                var icons = SpartanPortTrophyReader.ReadIcons(path);
                Assert.AreEqual(2, icons.Count);
                CollectionAssert.AreEqual(png, icons[0]);
                CollectionAssert.AreEqual(png, icons[34]);
                trp[0] = 0;
                File.WriteAllBytes(path, trp);
                Assert.AreEqual(0, SpartanPortTrophyReader.ReadIcons(path).Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ProvidesThirtyFiveLocalizedTrophiesAndCorrectTiers()
        {
            var text = SpartanPortTrophyMetadata.Load(null);
            Assert.AreEqual(35, text.Count);
            Assert.AreEqual("platinum", SpartanPortTrophyMetadata.TrophyType(0));
            Assert.AreEqual("bronze", SpartanPortTrophyMetadata.TrophyType(18));
            Assert.AreEqual("silver", SpartanPortTrophyMetadata.TrophyType(19));
            Assert.AreEqual("silver", SpartanPortTrophyMetadata.TrophyType(29));
            Assert.AreEqual("gold", SpartanPortTrophyMetadata.TrophyType(30));
        }

        [TestMethod]
        public void UsesPortugueseFilesFromPortWhenAvailable()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "trophy_texts"));
                File.WriteAllText(Path.Combine(root, "trophy_texts", "pt.txt"),
                    "0=Rei da colina | Desbloqueie todos os troféus\n34=Labirintite | Supere o labirinto");
                var text = SpartanPortTrophyMetadata.Load(root);
                Assert.AreEqual("Rei da colina", text[0].Name);
                Assert.AreEqual("Supere o labirinto", text[34].Description);
                Assert.AreEqual(35, text.Count);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void WriteEntry(byte[] trp, int entry, string name, int offset, byte[] content)
        {
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
            Buffer.BlockCopy(nameBytes, 0, trp, entry, nameBytes.Length);
            WriteBigEndian(trp, entry + 0x20, offset);
            WriteBigEndian(trp, entry + 0x28, content.Length);
            Buffer.BlockCopy(content, 0, trp, offset, content.Length);
        }

        private static void WriteBigEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset + 4] = (byte)(value >> 24);
            bytes[offset + 5] = (byte)(value >> 16);
            bytes[offset + 6] = (byte)(value >> 8);
            bytes[offset + 7] = (byte)value;
        }
    }
}
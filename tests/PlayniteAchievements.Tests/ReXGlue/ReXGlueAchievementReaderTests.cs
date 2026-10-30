using Microsoft.VisualStudio.TestTools.UnitTesting;
using PlayniteAchievements.Providers.ReXGlue;
using System;
using System.IO;
using System.Linq;

namespace PlayniteAchievements.Tests.ReXGlue
{
    [TestClass]
    public sealed class ReXGlueAchievementReaderTests
    {
        [TestMethod]
        public void ReadsSdkCatalogIncludingTitleIdAndIcons()
        {
            var directory = NewFolder();
            try
            {
                var catalog = Path.Combine(directory, "achievements.toml");
                Directory.CreateDirectory(Path.Combine(directory, "icons"));
                File.WriteAllText(Path.Combine(directory, "icons", "10.png"), "x");
                File.WriteAllText(catalog, string.Join("\n", new[]
                {
                    "# Title ID: 584108FF",
                    "[[achievements]]",
                    "id = 1",
                    "label = \"Missão especial\"",
                    "description = \"Vença a fase\"",
                    "unachieved_description = \"Conquista secreta\"",
                    "gamerscore = 50",
                    "image_id = 10",
                    "icon_path = \"icons/10.png\"",
                    "flags = 0",
                    "[[achievements]]",
                    "id = 2",
                    "label = \"Outra\"",
                    "gamerscore = 15",
                    "flags = 8"
                }));
                Assert.IsTrue(ReXGlueAchievementReader.TryReadCatalog(
                    catalog, out var achievements, out var titleId));
                Assert.AreEqual("584108FF", titleId);
                Assert.AreEqual(2, achievements.Count);
                Assert.AreEqual("Missão especial", achievements[0].Name);
                Assert.AreEqual(50, achievements[0].Gamerscore);
                Assert.IsTrue(achievements[0].Hidden);
                Assert.IsFalse(achievements[1].Hidden);
                Assert.AreEqual(Path.Combine(directory, "icons", "10.png"),
                    ReXGlueAchievementReader.ResolveIcon(catalog, achievements[0].IconPath));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void PreservesPortugueseSubtableWithoutSwitchingToLaterLanguages()
        {
            var directory = NewFolder();
            try
            {
                var catalog = Path.Combine(directory, "achievements.toml");
                File.WriteAllText(catalog, string.Join("\n", new[]
                {
                    "# Title ID: 58410A07",
                    "[[achievements]]",
                    "id = 1",
                    "label = \"The Story Begins\"",
                    "description = \"Finish stage one\"",
                    "gamerscore = 5",
                    "[achievements.Portuguese]",
                    "label = \"A história começa\"",
                    "description = \"Termine a primeira fase\"",
                    "[achievements.Japanese]",
                    "label = \"別のタイトル\"",
                    "[[achievements]]",
                    "id = 2",
                    "label = \"Next\"",
                    "description = \"Finish stage two\""
                }));
                Assert.IsTrue(ReXGlueAchievementReader.TryReadCatalog(
                    catalog, out var achievements, out var titleId));
                Assert.AreEqual("58410A07", titleId);
                Assert.AreEqual(2, achievements.Count);
                Assert.AreEqual("A história começa", achievements[0].Name);
                Assert.AreEqual("Termine a primeira fase", achievements[0].Description);
                Assert.AreEqual(5, achievements[0].Gamerscore);
                Assert.AreEqual("Next", achievements[1].Name);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ReadsArmyOfTwoTabSeparatedCatalog()
        {
            var directory = NewFolder();
            try
            {
                var catalog = Path.Combine(directory, "catalog.tsv");
                File.WriteAllText(catalog, string.Join("\n", new[]
                {
                    "A\t21\t0\t30\t502\t1",
                    "T\t21\ten\tEnglish title\tEnglish description\tHidden",
                    "T\t21\tpt\tTítulo em português\tDescrição\tOculta",
                    "A\t22\t1\t10\t503\t0",
                    "T\t22\ten\tEnglish second\tSecond description\tSecret"
                }));
                Assert.IsTrue(ReXGlueAchievementReader.TryReadCatalog(
                    catalog, out var achievements, out var id));
                Assert.IsNull(id);
                Assert.AreEqual(2, achievements.Count);
                Assert.AreEqual("Título em português", achievements[0].Name);
                Assert.AreEqual(30, achievements[0].Gamerscore);
                Assert.AreEqual(Path.Combine("icons", "502.png"), achievements[0].IconPath);
                Assert.AreEqual("English second", achievements[1].Name);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ReadsTimestampedUnlocksAndLegacyLists()
        {
            var directory = NewFolder();
            try
            {
                var file = Path.Combine(directory, "584108FF.toml");
                var date = new DateTime(2026, 10, 30, 12, 30, 0, DateTimeKind.Utc);
                File.WriteAllText(file, "[unlocked.21]\nfiletime = " +
                    date.ToFileTimeUtc() + "\n[unlocked.5]\nfiletime = 0\n");
                Assert.IsTrue(ReXGlueAchievementReader.TryReadUnlocks(file, out var unlocked));
                Assert.AreEqual(2, unlocked.Count);
                Assert.AreEqual(date, unlocked[21]);
                Assert.IsNull(unlocked[5]);

                File.WriteAllText(file, "unlocked = [3, 8, 12]\n");
                Assert.IsTrue(ReXGlueAchievementReader.TryReadUnlocks(file, out unlocked));
                CollectionAssert.AreEquivalent(new uint[] { 3, 8, 12 }, unlocked.Keys.ToArray());
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void MissingSaveAndUnsafeIconAreNotAccepted()
        {
            var directory = NewFolder();
            try
            {
                var catalog = Path.Combine(directory, "achievements.toml");
                File.WriteAllText(catalog, "[[achievements]]\nid = 1\nlabel = \"One\"\n");
                Assert.IsFalse(ReXGlueAchievementReader.TryReadUnlocks(
                    Path.Combine(directory, "unknown.toml"), out _));
                Assert.IsNull(ReXGlueAchievementReader.ResolveIcon(catalog, "../outside.png"));
                Assert.IsNull(ReXGlueAchievementReader.ResolveIcon(catalog, "C:\\outside.png"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void RejectsMalformedProgressAndAcceptsEmptyRuntimeSave()
        {
            var directory = NewFolder();
            try
            {
                var path = Path.Combine(directory, "584108FF.toml");
                File.WriteAllText(path, "unrecognized = 123\n");
                Assert.IsFalse(ReXGlueAchievementReader.TryReadUnlocks(path, out _));
                File.WriteAllText(path,
                    "# Achievement unlock state - managed by ReXGlue runtime\n\n");
                Assert.IsTrue(ReXGlueAchievementReader.TryReadUnlocks(path, out var unlocked));
                Assert.AreEqual(0, unlocked.Count);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void FindsUniqueSavesWithoutGuessingMultipleTitleIds()
        {
            var directory = NewFolder();
            try
            {
                var achievements = Path.Combine(directory, "achievements");
                Directory.CreateDirectory(achievements);
                File.WriteAllText(Path.Combine(achievements, "584108FF.toml"), "saved");
                Assert.AreEqual(Path.Combine(achievements, "584108FF.toml"),
                    ReXGluePaths.FindSavePath(new[] { directory }, null));
                File.WriteAllText(Path.Combine(achievements, "ABCD0001.toml"), "saved");
                Assert.IsNull(ReXGluePaths.FindSavePath(new[] { directory }, null));
                Assert.AreEqual(Path.Combine(achievements, "ABCD0001.toml"),
                    ReXGluePaths.FindSavePath(new[] { directory }, "abcd0001"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void FindsDantesPortableAndReblueProfileUnlocks()
        {
            var directory = NewFolder();
            try
            {
                var dante = Path.Combine(directory, "Dante");
                var portable = Path.Combine(dante, "userdata");
                var portableAch = Path.Combine(portable, "achievements");
                Directory.CreateDirectory(portableAch);
                var danteFile = Path.Combine(portableAch, "454108CF.toml");
                File.WriteAllText(danteFile, "[unlocked.25]\nfiletime = 0\n");
                Assert.AreEqual(danteFile, ReXGluePaths.FindSavePath(
                    new[] { portable, Path.Combine(dante, "user_data"), dante },
                    "454108CF"));

                var blue = Path.Combine(directory, "Blue Dragon");
                var profile = Path.Combine(blue, "profiles", "default");
                var blueAch = Path.Combine(profile, "achievements");
                Directory.CreateDirectory(blueAch);
                var blueFile = Path.Combine(blueAch, "4D5307DF.toml");
                File.WriteAllText(blueFile, "[unlocked.65542]\nfiletime = 0\n");
                var detected = ReXGluePaths.DiscoverProfileRoots(blue);
                Assert.AreEqual(1, detected.Count);
                Assert.AreEqual(profile, detected[0]);
                Assert.AreEqual(blueFile,
                    ReXGluePaths.FindSavePath(detected, "4D5307DF"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void IncludesAllEightReblueCustomAchievementsWithoutDuplicating()
        {
            var achievements = new System.Collections.Generic.List<ReXGlueAchievement>
            {
                new ReXGlueAchievement { Id = 1, Name = "Original" }
            };
            ReblueExtraAchievements.AppendTo(achievements);
            ReblueExtraAchievements.AppendTo(achievements);
            Assert.AreEqual(9, achievements.Count);
            var custom = achievements.Single(row => row.Id == 65542);
            Assert.AreEqual("Cloaca Maxima", custom.Name);
            Assert.AreEqual(420, custom.Gamerscore);
            Assert.IsTrue(custom.Hidden);
        }

        [TestMethod]
        public void ParsesGameLaunchCommandsWithoutTreatingArgumentsAsFolders()
        {
            Assert.AreEqual(
                @"D:\Games\Microsoft Games\Blue Dragon\reblue.exe",
                ReXGluePaths.ExtractExecutablePath(
                    "\"D:\\Games\\Microsoft Games\\Blue Dragon\\reblue.exe\" --profile default"));
            Assert.AreEqual(
                @"C:\Games\Sonic Unleashed\UnleashedRecomp.exe",
                ReXGluePaths.ExtractExecutablePath(
                    @"C:\Games\Sonic Unleashed\UnleashedRecomp.exe --console"));
        }

        [TestMethod]
        public void FindsXexInCommonRecompGameLayout()
        {
            var root = NewFolder();
            try
            {
                var game = Path.Combine(root, "game");
                Directory.CreateDirectory(game);
                var xex = Path.Combine(game, "default.xex");
                File.WriteAllBytes(xex, new byte[] { 1, 2, 3 });
                Assert.AreEqual(xex, ReXGluePaths.FindXexPath(new[] { root }));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string NewFolder()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}

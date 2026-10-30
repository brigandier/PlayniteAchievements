using Microsoft.VisualStudio.TestTools.UnitTesting;
using Playnite.SDK.Models;
using PlayniteAchievements.Providers.SpartanPort;
using System;
using System.IO;

namespace PlayniteAchievements.Tests.SpartanPort
{
    [TestClass]
    public sealed class SpartanPortPathsTests
    {
        [TestMethod]
        public void ResolvesStandaloneInstallDirectoryAndDefaultSaveFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "user"));
                File.WriteAllText(Path.Combine(root, "settings.json"), "{\"user_dir\":\"\"}");
                var game = new Game
                {
                    Name = "God of War III Remastered",
                    InstallDirectory = root
                };
                var resolved = SpartanPortPaths.Resolve(game, new SpartanPortSettings(), null);
                Assert.IsNotNull(resolved);
                Assert.AreEqual(Path.Combine(root, "user"), resolved.UserDirectory);
                Assert.AreEqual(Path.Combine(root, "user", "trophies.dat"), resolved.TrophyFile);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void UsesExplicitPortRootAndConfiguredSaveFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var save = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(save);
                var game = new Game { Name = "God of War 3 Remastered" };
                var settings = new SpartanPortSettings
                {
                    GameRoot = root,
                    UserDirectory = save
                };
                var resolved = SpartanPortPaths.Resolve(game, settings, null);
                Assert.IsNotNull(resolved);
                Assert.AreEqual(root, resolved.Root);
                Assert.AreEqual(save, resolved.UserDirectory);
            }
            finally
            {
                Directory.Delete(root, true);
                Directory.Delete(save, true);
            }
        }

        [TestMethod]
        public void RejectsDifferentGameAndUnrelatedInstallDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var settings = new SpartanPortSettings();
                Assert.IsNull(SpartanPortPaths.Resolve(
                    new Game { Name = "God of War Ascension", InstallDirectory = root },
                    settings, null));
                Assert.IsNull(SpartanPortPaths.Resolve(
                    new Game { Name = "God of War III Remastered", InstallDirectory = root },
                    settings, null));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
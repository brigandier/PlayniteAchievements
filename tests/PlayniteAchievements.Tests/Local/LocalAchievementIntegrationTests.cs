using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK.Models;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievements.Tests.Local
{
    [TestClass]
    public sealed class LocalAchievementIntegrationTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [TestCategory("LocalInstallation")]
        public void InstalledGamesReadOnlyValidation()
        {
            var manifest = Environment.GetEnvironmentVariable("PLAYACH_LOCAL_VALIDATION");
            if (string.IsNullOrWhiteSpace(manifest))
                Assert.Inconclusive("Optional read-only installation validation requires PLAYACH_LOCAL_VALIDATION.");
            foreach (var item in JArray.Parse(File.ReadAllText(manifest)))
            {
                var game = new Game(item.Value<string>("Name")) { Id = Guid.NewGuid(), InstallDirectory = item.Value<string>("InstallDirectory") };
                var configuration = item["Configuration"].ToObject(Type("Providers.Local.LocalAchievementConfiguration"));
                Set(configuration, "GameId", game.Id);
                var progressPath = (string)Get(configuration, "ProgressPath");
                var before = File.ReadAllBytes(progressPath);
                if (!string.IsNullOrEmpty(game.InstallDirectory))
                {
                    var detected = Type("Providers.Local.LocalAchievementLoader").GetMethod("Detect", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { game, null });
                    Assert.IsNotNull(detected, game.Name + ": automatic discovery");
                    Assert.AreEqual(Path.GetFullPath(progressPath), Path.GetFullPath((string)Get(detected, "ProgressPath")), true,
                        game.Name + ": automatically selected progress file");
                    Assert.AreEqual(Get(configuration, "Format"), Get(detected, "Format"));
                }
                var data = Type("Providers.Local.LocalAchievementLoader").GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new[] { game, configuration, Path.GetDirectoryName(manifest), null });
                var rows = JArray.FromObject(Get(data, "Achievements"));
                Assert.AreEqual(item.Value<int>("Total"), rows.Count, game.Name);
                Assert.AreEqual(item.Value<int>("Unlocked"), rows.Count(row => row.Value<bool>("Unlocked")), game.Name);
                if (item["ExpectedUnlock"] != null)
                {
                    var row = rows.Single(entry => entry.Value<string>("DisplayName") == item.Value<string>("ExpectedUnlock"));
                    Assert.IsTrue(row.Value<bool>("Unlocked"));
                    Assert.AreEqual(420, row.Value<int>("Points"));
                }
                foreach (var row in rows) Assert.IsTrue(File.Exists(row.Value<string>("UnlockedIconPath")), game.Name + ": " + row.Value<string>("DisplayName"));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(progressPath), "Progress files must remain unchanged.");
                TestContext.WriteLine($"{game.Name}: {rows.Count} achievements; {rows.Count(row => row.Value<bool>("Unlocked"))} unlocked; all icons present; progress unchanged.");
            }
        }

        private static Assembly Plugin => AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "PlayniteAchievements");
        private static Type Type(string name) => Plugin.GetType("PlayniteAchievements." + name, true);
        private static object New(string name, params object[] arguments) => Activator.CreateInstance(Type(name), arguments);
        private static object Get(object value, string property) => value.GetType().GetProperty(property).GetValue(value);
        private static void Set(object value, string property, object item) => value.GetType().GetProperty(property).SetValue(value, item);
        private static object Call(object value, string method, params object[] arguments) =>
            value.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == arguments.Length).Invoke(value, arguments);

        [TestMethod]
        public void LockedCatalogDoesNotDependOnUnlocksAndRejectsIncompatibleSources()
        {
            var folder = Path.Combine(Path.GetTempPath(), "playach-local-catalog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var game = new Game("Arbitrary library title") { Id = Guid.NewGuid() };
                var progress = Path.Combine(folder, "ACH-DATA");
                var catalog = Path.Combine(folder, "catalog.toml");
                var bytes = new byte[816];
                Array.Copy(new byte[] { 65, 67, 72, 32, 1 }, bytes, 5);
                File.WriteAllBytes(progress, bytes);
                File.WriteAllText(catalog, "# Title ID: 53450812\n" + CatalogEntry(24) + CatalogEntry(25));
                var configuration = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new
                {
                    GameId = game.Id, ProgressPath = progress, CatalogPath = catalog, TitleId = "53450812"
                }), Type("Providers.Local.LocalAchievementConfiguration"));
                var load = Type("Providers.Local.LocalAchievementLoader").GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic);
                Func<object> read = () => load.Invoke(null, new[] { game, configuration, folder, null });
                var rows = JArray.FromObject(Get(read(), "Achievements"));
                Assert.AreEqual(2, rows.Count);
                Assert.IsTrue(rows.All(row => !row.Value<bool>("Unlocked")));
                Set(configuration, "TitleId", "454108CF");
                Assert.IsInstanceOfType<InvalidDataException>(Assert.ThrowsException<TargetInvocationException>(() => read()).InnerException);
                Set(configuration, "TitleId", "53450812");
                bytes[16] = 83;
                bytes[8] = 83;
                File.WriteAllBytes(progress, bytes);
                Assert.IsInstanceOfType<InvalidDataException>(Assert.ThrowsException<TargetInvocationException>(() => read()).InnerException);
                bytes[8] = 0;
                File.WriteAllBytes(progress, bytes);
                Assert.IsInstanceOfType<InvalidDataException>(Assert.ThrowsException<TargetInvocationException>(() => read()).InnerException);
            }
            finally { Directory.Delete(folder, true); }
        }

        [DataTestMethod]
        [DataRow("ReXGlue", "454108CF.toml", 1)]
        [DataRow("UnleashedRecompiled", "ACH-DATA", 24)]
        [DataRow("SpartanPort", "trophies.dat", 0)]
        [DataRow("AchievementJson", "Achievements.json", 1)]
        public async Task FileProviderSqliteWatcherAndNotificationPolicy(string format, string fileName, int firstId)
        {
            var folder = Path.Combine(Path.GetTempPath(), "playach-local-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            object store = null;
            object watchers = null;
            try
            {
                var game = new Game("Controlled local achievements") { Id = Guid.NewGuid() };
                var progressPath = Path.Combine(folder, fileName);
                var catalogPath = Path.Combine(folder, "achievements.toml");
                var oldDate = DateTime.UtcNow.AddDays(-1);
                WriteProgress(progressPath, format, firstId, oldDate, false);
                if (format != "SpartanPort" && format != "AchievementJson")
                    File.WriteAllText(catalogPath, "# Title ID: " + (format == "ReXGlue" ? "454108CF" : "53450812") + "\n" +
                        CatalogEntry(firstId) + CatalogEntry(firstId + 1));

                var settings = New("Models.PlayniteAchievementsSettings");
                var provider = New("Providers.Local.LocalAchievementDataProvider", null, null, settings, folder);
                var providerSettings = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new
                {
                    Games = new[] { new { GameId = game.Id, Format = format, ProgressPath = progressPath,
                        CatalogPath = format == "SpartanPort" || format == "AchievementJson" ? null : catalogPath, Monitor = true } }
                }), Type("Providers.Local.LocalAchievementSettings"));
                Call(provider, "ApplySettings", providerSettings);
                Assert.AreEqual(true, Call(provider, "IsCapable", game));
                var restored = New("Providers.Local.LocalAchievementSettings");
                Call(restored, "DeserializeFromJson", Call(providerSettings, "SerializeToJson"));
                Assert.AreEqual(progressPath, Get(Call(restored, "Find", game.Id), "ProgressPath"));

                var plugin = FormatterServices.GetUninitializedObject(Type("PlayniteAchievementsPlugin"));
                var vm = FormatterServices.GetUninitializedObject(Type("ViewModels.PlayniteAchievementsSettingsViewModel"));
                Set(vm, "Settings", settings);
                Type("PlayniteAchievementsPlugin").GetField("_settingsViewModel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(plugin, vm);
                store = New("Services.Database.SqlNadoCacheStore", plugin, null, folder);
                var currentStore = store;
                var callbackType = provider.GetType().GetMethod("RefreshAsync").GetParameters()[2].ParameterType;
                var callbackArguments = callbackType.GetMethod("Invoke").GetParameters().Select(parameter => Expression.Parameter(parameter.ParameterType)).ToArray();
                Func<object, Task> persist = data =>
                {
                    Assert.AreEqual(format == "SpartanPort" ? "ps4" : "xbox-360", Get(Get(data, "MetadataHints"), "PlatformSlug"));
                    Call(currentStore, "SaveCurrentUserGameData", game.Id.ToString(), data);
                    return Task.CompletedTask;
                };
                var callback = Expression.Lambda(callbackType,
                    Expression.Invoke(Expression.Constant(persist), Expression.Convert(callbackArguments[1], typeof(object))), callbackArguments).Compile();
                await ((Task)Call(provider, "RefreshAsync", new[] { game }, null, callback, CancellationToken.None));
                var cached = Call(store, "LoadCurrentUserGameData", game.Id.ToString());
                Assert.IsNotNull(cached, "The provider must persist a complete catalog through the normal refresh callback.");
                if (format == "SpartanPort")
                {
                    var textsDirectory = Path.Combine(folder, "trophy_texts");
                    Directory.CreateDirectory(textsDirectory);
                    File.WriteAllLines(Path.Combine(textsDirectory, "en.txt"), Enumerable.Range(0, 35)
                        .Select(id => $"{id} = English trophy {id} | Controlled English description").ToArray());
                    var englishTask = (Task)Type("Providers.IEnglishTitleSource").GetMethod("GetEnglishTitlesAsync")
                        .Invoke(provider, new[] { game, cached, CancellationToken.None });
                    await englishTask;
                    var englishRows = JArray.FromObject(Get(englishTask, "Result"));
                    Assert.AreEqual(35, englishRows.Count);
                    Assert.AreEqual("gow3_spartanport_000", englishRows[0].Value<string>("ApiName"));
                    Assert.AreEqual("English trophy 0", englishRows[0].Value<string>("DisplayName"));
                }
                var rows = JArray.FromObject(Get(cached, "Achievements"));
                Assert.AreEqual(format == "SpartanPort" ? 35 : 2, rows.Count);
                Assert.AreEqual(1, rows.Count(row => row.Value<bool>("Unlocked")));
                foreach (var achievement in ((IEnumerable)Get(cached, "Achievements")).Cast<object>())
                {
                    Set(achievement, "Unlocked", false);
                    Set(achievement, "UnlockTimeUtc", null);
                }
                Call(store, "SaveCurrentUserGameData", game.Id.ToString(), cached);
                var registration = Call(provider, "TryRegister", game, cached);
                Assert.IsNotNull(registration);
                Assert.AreEqual(TimeSpan.FromMilliseconds(500), Get(registration, "PollInterval"));
                var context = New("Providers.InGameTrackingContext");
                Set(context, "Game", game);
                Set(context, "CachedSchema", cached);
                Set(context, "Registration", registration);
                var contexts = Array.CreateInstance(context.GetType(), 1);
                contexts.SetValue(context, 0);
                var sessionStart = DateTime.UtcNow;
                var notifications = 0;
                Func<bool, Task> readAndApply = async primed =>
                {
                    var queryTask = (Task)Call(provider, "QueryAsync", contexts, CancellationToken.None);
                    await queryTask;
                    var result = ((IEnumerable)Get(queryTask, "Result")).Cast<object>().Single();
                    Assert.AreEqual(true, Get(result, "Success"));
                    var write = Call(currentStore, "ApplyInGameProgress", game.Id.ToString(), "LocalAchievements", Get(result, "Achievements"));
                    Assert.AreEqual(true, Get(write, "Success"));
                    foreach (var key in ((IEnumerable)Get(write, "NewlyUnlockedKeys")).Cast<string>())
                    {
                        var loaded = Call(currentStore, "LoadCurrentUserGameData", game.Id.ToString());
                        var row = ((IEnumerable)Get(loaded, "Achievements")).Cast<object>().Single(item => (string)Get(item, "ApiName") == key);
                        var emit = (bool)Type("Services.InGameMonitoring.InGameUnlockEmissionPolicy").GetMethod("ShouldEmit")
                            .Invoke(null, new[] { (object)primed, sessionStart, Get(row, "UnlockTimeUtc") });
                        if (emit) notifications++;
                    }
                };
                await readAndApply(false);
                Assert.AreEqual(0, notifications, "Existing unlocks must remain silent.");
                Assert.AreEqual(1, JArray.FromObject(Get(Call(store, "LoadCurrentUserGameData", game.Id.ToString()), "Achievements"))
                    .Count(row => row.Value<bool>("Unlocked")), "The silent baseline must still update the cache.");
                using (var changed = new ManualResetEventSlim())
                {
                    watchers = New("Services.InGameMonitoring.ExactFileWatcherPool", new object[] { null });
                    var subscribe = watchers.GetType().GetMethod("Subscribe");
                    using ((IDisposable)subscribe.Invoke(watchers, new object[] { progressPath, (Action<string, bool>)((path, overflow) => changed.Set()) }))
                    {
                        var replacement = Path.Combine(folder, "replacement.tmp");
                        WriteProgress(replacement, format, firstId, oldDate, true);
                        File.Replace(replacement, progressPath, null);
                        Assert.IsTrue(changed.Wait(TimeSpan.FromSeconds(5)), "Atomic replacement must signal the existing watcher.");
                        await readAndApply(true);
                        await readAndApply(true);
                    }
                }
                Assert.AreEqual(1, notifications, "A new unlock must notify once, even after repeated reads.");
                var after = JArray.FromObject(Get(Call(store, "LoadCurrentUserGameData", game.Id.ToString()), "Achievements"));
                Assert.AreEqual(2, after.Count(row => row.Value<bool>("Unlocked")));
                Assert.AreEqual(rows.Count, after.Count);
                var liveSettings = Call(provider, "GetSettings");
                Set(Call(liveSettings, "Find", game.Id), "Monitor", false);
                Assert.AreEqual(false, Call(provider, "ShouldMonitor", game));
                Assert.IsNull(Call(provider, "TryRegister", game, cached));
                Assert.IsTrue((bool)Call(provider, "IsCapable", game), "Disabling monitoring must preserve manual refresh.");
                if (format != "SpartanPort" && format != "AchievementJson")
                    Assert.AreEqual(oldDate.ToFileTimeUtc() / 10000000,
                        after.Single(row => row.Value<bool>("Unlocked") && row.Value<string>("DisplayName") == "Achievement " + firstId)
                            .Value<DateTime>("UnlockTimeUtc").ToFileTimeUtc() / 10000000);
            }
            finally
            {
                (watchers as IDisposable)?.Dispose();
                (store as IDisposable)?.Dispose();
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void HollowJsonAcceptsValidMetadataAndRejectsCorruptStates()
        {
            var folder = Path.Combine(Path.GetTempPath(), "playach-hollow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var state = Path.Combine(folder, "Achievements.json");
                var game = new Game("Gears of War 2: Hollow") { Id = Guid.NewGuid() };
                var settings = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new
                {
                    GameId = game.Id, Format = "AchievementJson", ProgressPath = state,
                    TitleId = "4D53082D", Monitor = true
                }), Type("Providers.Local.LocalAchievementConfiguration"));
                var loader = Type("Providers.Local.LocalAchievementLoader").GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic);
                Func<object> load = () => loader.Invoke(null, new[] { game, settings, folder, null });
                var record = new
                {
                    title_id = "4D53082D",
                    achievements = new[]
                    {
                        new { id = 1, name = "Welcome", description = "Complete the first mission.", gamerscore = 10, unlocked = true, hidden = false },
                        new { id = 2, name = "The Road Ahead", description = "Continue the campaign.", gamerscore = 20, unlocked = false, hidden = true }
                    }
                };
                File.WriteAllText(state, JsonConvert.SerializeObject(record));
                var entries = JArray.FromObject(Get(load(), "Achievements"));
                Assert.AreEqual(2, entries.Count);
                Assert.AreEqual(1, entries.Count(row => row.Value<bool>("Unlocked")));
                Assert.IsTrue(entries.All(row => row["UnlockTimeUtc"]?.Type == JTokenType.Null));
                Assert.AreEqual(30, entries.Sum(row => row.Value<int>("Points")));

                File.WriteAllText(state, "{");
                Assert.IsInstanceOfType<InvalidDataException>(Assert.ThrowsException<TargetInvocationException>(() => load()).InnerException);
                File.WriteAllText(state, "{\"achievements\":[{\"id\":1,\"name\":\"One\",\"description\":\"a\",\"gamerscore\":10,\"unlocked\":true},{\"id\":1,\"name\":\"Two\",\"description\":\"b\",\"gamerscore\":10,\"unlocked\":false}]}");
                Assert.IsInstanceOfType<InvalidDataException>(Assert.ThrowsException<TargetInvocationException>(() => load()).InnerException);
                File.WriteAllText(state, "{\"title_id\":\"11111111\",\"achievements\":[{\"id\":1,\"name\":\"One\",\"description\":\"a\",\"gamerscore\":10,\"unlocked\":true}]}");
                Assert.IsInstanceOfType<InvalidDataException>(Assert.ThrowsException<TargetInvocationException>(() => load()).InnerException);
            }
            finally { Directory.Delete(folder, true); }
        }

        [TestMethod]
        public void HollowDiscoveryRequiresMatchingGameAndInstallation()
        {
            var parent = Path.Combine(Path.GetTempPath(), "Gears of War 2 Hollow " + Guid.NewGuid().ToString("N"));
            var save = Path.Combine(parent, "GearGame", "SaveData");
            var executable = Path.Combine(parent, "Binaries", "GearGame.exe");
            Directory.CreateDirectory(save);
            Directory.CreateDirectory(Path.GetDirectoryName(executable));
            try
            {
                File.WriteAllBytes(executable, new byte[] { 77, 90 });
                WriteProgress(Path.Combine(save, "Achievements.json"), "AchievementJson", 1, DateTime.UtcNow, false);
                var validGame = new Game("Gears of War 2: Hollow") { Id = Guid.NewGuid(), InstallDirectory = parent };
                var detect = Type("Providers.Local.HollowAchievementReader").GetMethod("Detect",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var found = detect.Invoke(null, new object[] { validGame, null });
                Assert.IsNotNull(found);
                Assert.AreEqual(Path.Combine(save, "Achievements.json"), Get(found, "ProgressPath"));
                Assert.AreEqual("4D53082D", Get(found, "TitleId"));

                File.Delete(executable);
                Assert.IsNull(detect.Invoke(null, new object[] { validGame, null }));
            }
            finally { Directory.Delete(parent, true); }
        }

        private static string CatalogEntry(int id) => $"[[achievements]]\nid = {id}\nlabel = \"Achievement {id}\"\ndescription = \"Controlled catalog\"\ngamerscore = 10\n";

        private static void WriteProgress(string path, string format, int id, DateTime oldDate, bool addUnlock)
        {
            if (format == "ReXGlue")
            {
                File.WriteAllText(path, $"[unlocked.{id}]\nfiletime = {oldDate.ToFileTimeUtc()}\n" +
                    (addUnlock ? $"[unlocked.{id + 1}]\nfiletime = {DateTime.UtcNow.ToFileTimeUtc()}\n" : ""));
                return;
            }
            if (format == "AchievementJson")
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    achievements = new[]
                    {
                        new { id, name = "Achievement " + id, description = "Controlled catalog", gamerscore = 10, unlocked = true },
                        new { id = id + 1, name = "Achievement " + (id + 1), description = "Controlled catalog", gamerscore = 10, unlocked = addUnlock }
                    }
                }));
                return;
            }
            if (format == "SpartanPort")
            {
                var bitset = new byte[16];
                bitset[0] = (byte)(addUnlock ? 3 : 1);
                File.WriteAllBytes(path, bitset);
                return;
            }
            var bytes = new byte[816];
            Array.Copy(new byte[] { 65, 67, 72, 32, 1 }, bytes, 5);
            BitConverter.GetBytes((ushort)id).CopyTo(bytes, 16);
            BitConverter.GetBytes(new DateTimeOffset(oldDate).ToUnixTimeSeconds()).CopyTo(bytes, 18);
            if (addUnlock)
            {
                BitConverter.GetBytes((ushort)(id + 1)).CopyTo(bytes, 32);
                BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CopyTo(bytes, 34);
            }
            uint checksum = 0;
            for (var index = 16; index < bytes.Length; index++) checksum ^= bytes[index];
            BitConverter.GetBytes(checksum).CopyTo(bytes, 8);
            File.WriteAllBytes(path, bytes);
        }
    }
}

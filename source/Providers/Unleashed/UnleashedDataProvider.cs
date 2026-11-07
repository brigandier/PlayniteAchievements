using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Common;
using PlayniteAchievements.Models;
using PlayniteAchievements.Models.Achievements;
using PlayniteAchievements.Models.Friends;
using PlayniteAchievements.Providers.ReXGlue;
using PlayniteAchievements.Providers.Settings;
using PlayniteAchievements.Providers.Xenia;
using PlayniteAchievements.Services.Refresh;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievements.Providers.Unleashed
{
    internal sealed class UnleashedDataProvider :
        DataProviderBase<UnleashedSettings>, IDataProvider, IInGameProgressSource
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _api;
        private readonly string _pluginUserDataPath;

        public UnleashedDataProvider(ILogger logger, IPlayniteAPI api,
            PlayniteAchievementsSettings settings, string pluginUserDataPath)
        {
            _logger = logger;
            _api = api;
            _pluginUserDataPath = pluginUserDataPath;
        }

        public string ProviderName => "Unleashed Recompiled";
        public string ProviderKey => "UnleashedRecompiled";
        public string ProviderIconKey => "ProviderIconReXGlue";
        public string ProviderColorHex => "#287EB0";
        public bool IsAuthenticated => true;
        public ISessionManager AuthSession => null;
        public IFriendsProvider Friends => null;

        public bool IsCapable(Game game)
        {
            if (!ProviderSettings.IsEnabled)
            {
                return false;
            }
            return Resolve(game) != null;
        }

        public async Task<RebuildPayload> RefreshAsync(
            IReadOnlyList<Game> gamesToRefresh, Action<Game> onGameStarting,
            Func<Game, GameAchievementData, Task> onGameCompleted, CancellationToken cancel)
        {
            return await ProviderRefreshExecutor.RunProviderGamesAsync(
                gamesToRefresh, onGameStarting,
                (game, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var paths = Resolve(game);
                    if (paths == null)
                    {
                        return Task.FromResult(ProviderRefreshExecutor.ProviderGameResult.Skipped());
                    }

                    _logger?.Info($"[UnleashedRecompiled] '{game.Name}': " +
                        $"TitleId={paths.TitleId}, save={(File.Exists(paths.SavePath) ? "found" : "missing")}, " +
                        $"xex={(File.Exists(paths.XexPath) ? "found" : "missing")}.");

                    var catalogPath = RecompXdbfExtractor.TryExtract(
                        paths.XexPath, paths.TitleId, _pluginUserDataPath, _logger);
                    if (!ReXGlueAchievementReader.TryReadCatalog(
                            catalogPath, out var catalog, out _))
                    {
                        _logger?.Warn("[UnleashedRecompiled] Native Xbox 360 catalog is " +
                            "unavailable; achievement display was skipped.");
                        return Task.FromResult(ProviderRefreshExecutor.ProviderGameResult.Skipped());
                    }

                    var data = LoadGame(game, paths, catalogPath, catalog);
                    if (data == null)
                    {
                        return Task.FromResult(ProviderRefreshExecutor.ProviderGameResult.Skipped());
                    }
                    return Task.FromResult(new ProviderRefreshExecutor.ProviderGameResult { Data = data });
                },
                onGameCompleted, null,
                (game, error, count) =>
                    _logger?.Warn(error,
                        $"[UnleashedRecompiled] Failed to read achievements for '{game?.Name}'."),
                null, null, cancel).ConfigureAwait(false);
        }

        private GameAchievementData LoadGame(
            Game game, UnleashedPaths paths, string catalogPath,
            IReadOnlyList<ReXGlueAchievement> catalog)
        {
            var hasSave = UnleashedAchievementReader.TryReadUnlocks(
                paths.SavePath, out var unlocked);
            if (!hasSave && File.Exists(paths.SavePath))
            {
                return null;
            }

            var rows = catalog.Select(entry =>
            {
                var earned = hasSave &&
                    unlocked.TryGetValue((ushort)entry.Id, out var date) &&
                    entry.Id <= ushort.MaxValue;
                var time = earned ? unlocked[(ushort)entry.Id] : null;
                return new AchievementDetail
                {
                    ApiName = AchievementKey(entry.Id),
                    DisplayName = entry.Name,
                    Description = entry.Description ?? entry.LockedDescription,
                    UnlockedIconPath = ReXGlueAchievementReader.ResolveIcon(
                        catalogPath, entry.IconPath),
                    Points = entry.Gamerscore,
                    Hidden = entry.Hidden,
                    Unlocked = earned,
                    UnlockTimeUtc = time,
                    Category = "Xbox 360"
                };
            }).ToList();

            _logger?.Info($"[UnleashedRecompiled] '{game.Name}': " +
                $"{rows.Count} native achievements, {rows.Count(row => row.Unlocked)} unlocked.");

            return new GameAchievementData
            {
                PlayniteGameId = game.Id,
                ProviderKey = ProviderKey,
                MetadataHints = new MetadataHints { PlatformSlug = "xbox-360" },
                ProviderGameKey = "unleashed:" + paths.TitleId,
                LibrarySourceName = game.Source?.Name,
                GameName = game.Name,
                HasAchievements = true,
                LastUpdatedUtc = DateTime.UtcNow,
                Achievements = rows
            };
        }

        InGameProgressRegistration IInGameProgressSource.TryRegister(
            Game game, GameAchievementData cachedSchema)
        {
            if (!ProviderSettings.IsEnabled || cachedSchema?.Achievements == null ||
                cachedSchema.Achievements.Count == 0 ||
                !string.Equals(cachedSchema.ProviderKey, ProviderKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var paths = Resolve(game);
            if (paths == null || !File.Exists(paths.SavePath))
            {
                return null;
            }
            return new InGameProgressRegistration
            {
                ProviderKey = ProviderKey,
                State = paths.SavePath,
                WatchTargets = new[] { paths.SavePath },
                PollInterval = InGameProgressRegistration.FileWatchSafetyPollInterval,
                UnlockAnchorPolicy = InGameUnlockAnchorPolicy.ProviderReported
            };
        }

        Task<IReadOnlyList<InGameProgressQueryResult>> IInGameProgressSource.QueryAsync(
            IReadOnlyList<InGameTrackingContext> games, CancellationToken cancellationToken)
        {
            var results = new List<InGameProgressQueryResult>();
            foreach (var context in games ?? Array.Empty<InGameTrackingContext>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var gameId = context?.Game?.Id ?? Guid.Empty;
                var path = context?.Registration?.State as string;
                if (!UnleashedAchievementReader.TryReadUnlocks(path, out var unlocked))
                {
                    results.Add(InGameProgressQueryResult.Failed(
                        gameId, "progress_file_unavailable"));
                    continue;
                }

                results.Add(InGameProgressQueryResult.Succeeded(
                    gameId, unlocked.Select(pair => new AchievementProgressObservation
                    {
                        ApiName = AchievementKey(pair.Key),
                        Unlocked = true,
                        UnlockTimeUtc = pair.Value
                    }).ToList()));
            }
            return Task.FromResult<IReadOnlyList<InGameProgressQueryResult>>(results);
        }

        private UnleashedPaths Resolve(Game game)
        {
            if (game == null ||
                !System.Text.RegularExpressions.Regex.IsMatch(game.Name ?? "",
                    @"\bSonic\s+Unleashed\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                return null;
            }

            var roots = ReXGluePaths.FindGameRoots(game, _api);
            if (roots.Count == 0)
            {
                _logger?.Debug($"[UnleashedRecompiled] '{game.Name}': no Playnite " +
                    $"installation or executable directory. InstallDirectory='{game.InstallDirectory}'.");
                return null;
            }

            var xex = ReXGluePaths.FindXexPath(roots);
            if (xex == null)
            {
                _logger?.Debug($"[UnleashedRecompiled] '{game.Name}': " +
                    "original default.xex not found under Playnite game paths.");
                return null;
            }

            var appdata = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            var save = roots.Select(root =>
                UnleashedAchievementReader.FindSavePath(root, appdata))
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            if (string.IsNullOrWhiteSpace(save))
            {
                _logger?.Debug($"[UnleashedRecompiled] '{game.Name}': " +
                    "ACH-DATA not found in portable, roaming or mod-save directories.");
                return null;
            }

            try
            {
                var titleInfo = XeniaTitleIDExtractor.GetFromXexFile(xex);
                if (titleInfo?.TitleId == 0)
                {
                    return null;
                }
                return new UnleashedPaths
                {
                    XexPath = xex,
                    TitleId = titleInfo.TitleIdHex,
                    SavePath = save
                };
            }
            catch (IOException ex)
            {
                _logger?.Warn(ex, "[UnleashedRecompiled] Unable to read Xbox 360 title metadata.");
                return null;
            }
            catch (InvalidDataException ex)
            {
                _logger?.Warn(ex, "[UnleashedRecompiled] Xbox 360 executable header is invalid.");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.Warn(ex, "[UnleashedRecompiled] Access denied while reading game data.");
                return null;
            }
        }

        private static string AchievementKey(uint id)
        {
            return $"unleashed_{id:D8}";
        }

        private sealed class UnleashedPaths
        {
            public string XexPath { get; set; }
            public string SavePath { get; set; }
            public string TitleId { get; set; }
        }

        public ProviderSettingsViewBase CreateSettingsView()
        {
            return new UnleashedSettingsView();
        }
    }
}

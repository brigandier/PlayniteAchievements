using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Common;
using PlayniteAchievements.Models;
using PlayniteAchievements.Models.Achievements;
using PlayniteAchievements.Models.Friends;
using PlayniteAchievements.Providers.Exophase;
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
        private readonly PlayniteAchievementsSettings _settings;
        private readonly string _pluginUserDataPath;

        public UnleashedDataProvider(ILogger logger, IPlayniteAPI api,
            PlayniteAchievementsSettings settings, string pluginUserDataPath)
        {
            _logger = logger;
            _api = api;
            _settings = settings;
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
            return ProviderSettings.IsEnabled && Resolve(game) != null;
        }

        public async Task<RebuildPayload> RefreshAsync(
            IReadOnlyList<Game> gamesToRefresh, Action<Game> onGameStarting,
            Func<Game, GameAchievementData, Task> onGameCompleted, CancellationToken cancel)
        {
            ExophaseMetadataEnricher enricher = null;
            if (ProviderSettings.UseExophaseForRarity && gamesToRefresh?.Count > 0)
            {
                enricher = new ExophaseMetadataEnricher(
                    _api, _logger, _settings, _pluginUserDataPath);
                await enricher.InitializeAsync(cancel).ConfigureAwait(false);
            }

            try
            {
                return await ProviderRefreshExecutor.RunProviderGamesAsync(
                    gamesToRefresh, onGameStarting,
                    async (game, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var paths = Resolve(game);
                        if (paths == null)
                        {
                            return ProviderRefreshExecutor.ProviderGameResult.Skipped();
                        }

                        var catalogPath = RecompXdbfExtractor.TryExtract(
                            paths.XexPath, paths.TitleId, _pluginUserDataPath, _logger);
                        if (!ReXGlueAchievementReader.TryReadCatalog(
                                catalogPath, out var catalog, out _))
                        {
                            return ProviderRefreshExecutor.ProviderGameResult.Skipped();
                        }

                        var data = LoadGame(game, paths, catalogPath, catalog);
                        if (data == null)
                        {
                            return ProviderRefreshExecutor.ProviderGameResult.Skipped();
                        }
                        if (enricher != null)
                        {
                            await enricher.EnrichAsync(
                                game, data.Achievements, "xbox-360", "Xbox", ct,
                                fields: ExophaseMetadataFields.Rarity).ConfigureAwait(false);
                        }
                        return new ProviderRefreshExecutor.ProviderGameResult { Data = data };
                    },
                    onGameCompleted, null,
                    (game, error, count) =>
                        _logger?.Warn(error,
                            $"[UnleashedRecompiled] Failed to read achievements for '{game?.Name}'."),
                    null, null, cancel).ConfigureAwait(false);
            }
            finally
            {
                enricher?.Dispose();
            }
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

            return new GameAchievementData
            {
                PlayniteGameId = game.Id,
                ProviderKey = ProviderKey,
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

            var install = ExpandGamePath(game, game.InstallDirectory);
            if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install))
            {
                return null;
            }

            var candidates = new[] { install, Path.Combine(install, "game") };
            var xex = candidates.Select(root => Path.Combine(root, "default.xex"))
                .FirstOrDefault(File.Exists);
            if (xex == null)
            {
                return null;
            }

            var appdata = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            var save = UnleashedAchievementReader.FindSavePath(install, appdata);
            if (string.IsNullOrWhiteSpace(save))
            {
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
            catch (IOException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private string ExpandGamePath(Game game, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            try
            {
                return Path.GetFullPath(PathExpansion.ExpandGamePath(_api, game, path));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
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

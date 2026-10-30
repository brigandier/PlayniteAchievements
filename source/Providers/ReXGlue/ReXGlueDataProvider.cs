using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Models;
using PlayniteAchievements.Models.Achievements;
using PlayniteAchievements.Models.Friends;
using PlayniteAchievements.Providers.Exophase;
using PlayniteAchievements.Providers.Settings;
using PlayniteAchievements.Services.Refresh;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievements.Providers.ReXGlue
{
    internal sealed class ReXGlueDataProvider :
        DataProviderBase<ReXGlueSettings>, IDataProvider, IInGameProgressSource
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _api;
        private readonly PlayniteAchievementsSettings _settings;
        private readonly string _pluginUserDataPath;

        public ReXGlueDataProvider(
            ILogger logger, IPlayniteAPI api, PlayniteAchievementsSettings settings,
            string pluginUserDataPath)
        {
            _logger = logger;
            _api = api;
            _settings = settings;
            _pluginUserDataPath = pluginUserDataPath;
        }

        public string ProviderName => "ReXGlue";
        public string ProviderKey => "ReXGlue";
        public string ProviderIconKey => "ProviderIconReXGlue";
        public string ProviderColorHex => "#16894E";
        public bool IsAuthenticated => true;
        public ISessionManager AuthSession => null;
        public IFriendsProvider Friends => null;

        public bool IsCapable(Game game)
        {
            return ProviderSettings.IsEnabled &&
                ReXGluePaths.Resolve(game, ProviderSettings, _api) != null;
        }

        public async Task<RebuildPayload> RefreshAsync(
            IReadOnlyList<Game> gamesToRefresh,
            Action<Game> onGameStarting,
            Func<Game, GameAchievementData, Task> onGameCompleted,
            CancellationToken cancel)
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
                    gamesToRefresh,
                    onGameStarting,
                    async (game, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var paths = ReXGluePaths.Resolve(game, ProviderSettings, _api);
                        if (paths == null)
                        {
                            return ProviderRefreshExecutor.ProviderGameResult.Skipped();
                        }

                        var catalogPath = paths.CatalogPath ??
                            RecompXdbfExtractor.TryExtract(paths.XexPath, paths.TitleId,
                                _pluginUserDataPath, _logger);
                        if (!ReXGlueAchievementReader.TryReadCatalog(
                                catalogPath, out var catalog, out _))
                        {
                            return ProviderRefreshExecutor.ProviderGameResult.Skipped();
                        }
                        if (ReblueExtraAchievements.IsReblue(paths.GameRoot))
                        {
                            ReblueExtraAchievements.AppendTo(catalog);
                        }

                        var data = LoadGame(game, paths, catalog, catalogPath);
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

                        return new ProviderRefreshExecutor.ProviderGameResult
                        {
                            Data = data
                        };
                    },
                    onGameCompleted,
                    null,
                    (game, error, count) =>
                        _logger?.Warn(error,
                            $"[ReXGlue] Failed to refresh local achievements for '{game?.Name}'."),
                    null, null, cancel).ConfigureAwait(false);
            }
            finally
            {
                enricher?.Dispose();
            }
        }

        private GameAchievementData LoadGame(
            Game game, ReXGluePaths paths, IReadOnlyList<ReXGlueAchievement> catalog,
            string catalogPath)
        {
            var read = ReXGlueAchievementReader.TryReadUnlocks(
                paths.SavePath, out var unlocked);
            if (!read && !string.IsNullOrWhiteSpace(paths.SavePath))
            {
                return null;
            }
            var details = new List<AchievementDetail>(catalog.Count);

            foreach (var entry in catalog)
            {
                DateTime? when = null;
                var wasUnlocked = read && unlocked.TryGetValue(entry.Id, out when);
                var icon = ReXGlueAchievementReader.ResolveIcon(
                    catalogPath, entry.IconPath);
                details.Add(new AchievementDetail
                {
                    ApiName = AchievementKey(entry.Id),
                    DisplayName = entry.Name,
                    Description = entry.Description ?? entry.LockedDescription,
                    UnlockedIconPath = icon,
                    Points = entry.Gamerscore,
                    Hidden = entry.Hidden,
                    Unlocked = wasUnlocked,
                    UnlockTimeUtc = wasUnlocked ? when : null,
                    Category = "Xbox 360"
                });
            }

            return new GameAchievementData
            {
                PlayniteGameId = game.Id,
                ProviderKey = ProviderKey,
                ProviderGameKey = "rexglue:" + (paths.TitleId ??
                    Path.GetFileNameWithoutExtension(catalogPath)),
                LibrarySourceName = game.Source?.Name,
                GameName = game.Name,
                HasAchievements = true,
                LastUpdatedUtc = DateTime.UtcNow,
                Achievements = details
            };
        }

        InGameProgressRegistration IInGameProgressSource.TryRegister(
            Game game, GameAchievementData cachedSchema)
        {
            if (!ProviderSettings.IsEnabled ||
                cachedSchema?.Achievements == null ||
                cachedSchema.Achievements.Count == 0 ||
                !string.Equals(cachedSchema.ProviderKey, ProviderKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var paths = ReXGluePaths.Resolve(game, ProviderSettings, _api);
            if (paths == null || string.IsNullOrWhiteSpace(paths.SavePath) ||
                !Directory.Exists(Path.GetDirectoryName(paths.SavePath)))
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
            IReadOnlyList<InGameTrackingContext> games,
            CancellationToken cancellationToken)
        {
            var result = new List<InGameProgressQueryResult>();
            foreach (var context in games ?? Array.Empty<InGameTrackingContext>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = context?.Game?.Id ?? Guid.Empty;
                var file = context?.Registration?.State as string;
                if (!ReXGlueAchievementReader.TryReadUnlocks(file, out var unlocked))
                {
                    result.Add(InGameProgressQueryResult.Failed(
                        id, "progress_file_unavailable"));
                    continue;
                }

                result.Add(InGameProgressQueryResult.Succeeded(
                    id,
                    unlocked.Select(pair => new AchievementProgressObservation
                    {
                        ApiName = AchievementKey(pair.Key),
                        Unlocked = true,
                        UnlockTimeUtc = pair.Value
                    }).ToList()));
            }
            return Task.FromResult<IReadOnlyList<InGameProgressQueryResult>>(result);
        }

        private static string AchievementKey(uint id)
        {
            return $"rexglue_{id:D8}";
        }

        public ProviderSettingsViewBase CreateSettingsView()
        {
            return new ReXGlueSettingsView(_api);
        }
    }
}

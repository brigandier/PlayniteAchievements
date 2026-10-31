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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievements.Providers.Local
{
    internal sealed class LocalAchievementDataProvider : DataProviderBase<LocalAchievementSettings>, IDataProvider, IInGameProgressSource, IInGameMonitoringPolicy
    {
        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger;
        private readonly PlayniteAchievementsSettings _settings;
        private readonly string _pluginUserDataPath;

        public LocalAchievementDataProvider(ILogger logger, IPlayniteAPI api, PlayniteAchievementsSettings settings, string pluginUserDataPath)
        {
            _logger = logger;
            _api = api;
            _settings = settings;
            _pluginUserDataPath = pluginUserDataPath;
        }

        public string ProviderName => "Conquistas Locais";
        public string ProviderKey => "LocalAchievements";
        public string ProviderIconKey => "ProviderIconReXGlue";
        public string ProviderColorHex => "#16894E";
        public bool IsAuthenticated => true;
        public ISessionManager AuthSession => null;
        public IFriendsProvider Friends => null;
        private LocalAchievementConfiguration ConfigurationFor(Game game)
        {
            if (game == null) return null;
            var configured = ProviderSettings.Find(game.Id);
            if (configured != null) return configured;
            return HollowAchievementReader.MatchesGameName(game.Name)
                ? HollowAchievementReader.Detect(game, _api) : null;
        }

        public bool IsCapable(Game game) => ProviderSettings.IsEnabled && ConfigurationFor(game) != null;
        public bool ShouldMonitor(Game game) => ProviderSettings.IsEnabled && ConfigurationFor(game)?.Monitor == true;

        public async Task<RebuildPayload> RefreshAsync(IReadOnlyList<Game> gamesToRefresh, Action<Game> onGameStarting,
            Func<Game, GameAchievementData, Task> onGameCompleted, CancellationToken cancel)
        {
            using (var enricher = ProviderSettings.UseExophaseForRarity
                ? new ExophaseMetadataEnricher(_api, _logger, _settings, _pluginUserDataPath) : null)
            {
                if (enricher != null) await enricher.InitializeAsync(cancel).ConfigureAwait(false);
                return await ProviderRefreshExecutor.RunProviderGamesAsync(gamesToRefresh, onGameStarting,
                    async (game, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        var configuration = ConfigurationFor(game);
                        if (configuration == null) return ProviderRefreshExecutor.ProviderGameResult.Skipped();
                        var data = LocalAchievementLoader.Load(game, configuration, _pluginUserDataPath, _logger);
                        if (enricher != null)
                            await enricher.EnrichAsync(game, data.Achievements,
                                configuration.Format == "SpartanPort" ? "ps4" : "xbox-360",
                                configuration.Format == "SpartanPort" ? "PSN" : "Xbox", ct,
                                fields: ExophaseMetadataFields.Rarity).ConfigureAwait(false);
                        return new ProviderRefreshExecutor.ProviderGameResult { Data = data };
                    }, onGameCompleted, null,
                    (game, error, count) => _logger?.Warn(error, $"[LocalAchievements] Falha ao carregar '{game?.Name}'."),
                    null, null, cancel).ConfigureAwait(false);
            }
        }

        public InGameProgressRegistration TryRegister(Game game, GameAchievementData cachedSchema)
        {
            var configuration = ConfigurationFor(game);
            if (!ProviderSettings.IsEnabled || configuration?.Monitor != true ||
                cachedSchema?.ProviderKey != ProviderKey || cachedSchema.Achievements == null || cachedSchema.Achievements.Count == 0) return null;
            var reader = LocalProgressReaders.Resolve(configuration.Format, configuration.ProgressPath);
            if (reader?.CanMonitor != true) return null;
            return new InGameProgressRegistration
            {
                ProviderKey = ProviderKey,
                State = new TrackingState { Reader = reader, Path = configuration.ProgressPath },
                WatchTargets = new[] { configuration.ProgressPath },
                PollInterval = InGameProgressRegistration.FileWatchSafetyPollInterval,
                UnlockAnchorPolicy = InGameUnlockAnchorPolicy.ProviderReported
            };
        }

        public Task<IReadOnlyList<InGameProgressQueryResult>> QueryAsync(IReadOnlyList<InGameTrackingContext> games, CancellationToken cancellationToken)
        {
            var results = new List<InGameProgressQueryResult>();
            foreach (var context in games ?? Array.Empty<InGameTrackingContext>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = context?.Registration?.State as TrackingState;
                if (state == null || !state.Reader.TryRead(state.Path, out var unlocks))
                    results.Add(InGameProgressQueryResult.Failed(context?.Game?.Id ?? Guid.Empty, "progress_file_unavailable"));
                else
                    results.Add(InGameProgressQueryResult.Succeeded(context.Game.Id, unlocks.Select(pair => new AchievementProgressObservation
                    {
                        ApiName = state.Reader.AchievementKey(pair.Key), Unlocked = true, UnlockTimeUtc = pair.Value
                    }).ToList()));
            }
            return Task.FromResult<IReadOnlyList<InGameProgressQueryResult>>(results);
        }

        private sealed class TrackingState
        {
            internal ILocalProgressReader Reader;
            internal string Path;
        }

        public ProviderSettingsViewBase CreateSettingsView() => new LocalAchievementSettingsView(_api, _pluginUserDataPath, _logger);
    }
}

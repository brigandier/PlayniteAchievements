using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Models;
using PlayniteAchievements.Models.Achievements;
using PlayniteAchievements.Models.Friends;
using PlayniteAchievements.Providers.Settings;
using PlayniteAchievements.Services.Refresh;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievements.Providers.SpartanPort
{
    internal sealed class SpartanPortDataProvider :
        DataProviderBase<SpartanPortSettings>, IDataProvider, IInGameProgressSource
    {
        private readonly ILogger _logger;
        private readonly IPlayniteAPI _api;
        private readonly string _pluginUserDataPath;

        public SpartanPortDataProvider(ILogger logger, IPlayniteAPI api, string pluginUserDataPath)
        {
            _logger = logger;
            _api = api;
            _pluginUserDataPath = pluginUserDataPath ?? string.Empty;
        }

        public string ProviderName => "SpartanPort";
        public string ProviderKey => "SpartanPort";
        public string ProviderIconKey => "ProviderIconShadPS4";
        public string ProviderColorHex => "#356CCB";
        public bool IsAuthenticated => true;
        public ISessionManager AuthSession => null;
        public IFriendsProvider Friends => null;

        public bool IsCapable(Game game)
        {
            return ProviderSettings.IsEnabled &&
                   SpartanPortPaths.Resolve(game, ProviderSettings, _api) != null;
        }

        public Task<RebuildPayload> RefreshAsync(
            IReadOnlyList<Game> gamesToRefresh,
            Action<Game> onGameStarting,
            Func<Game, GameAchievementData, Task> onGameCompleted,
            CancellationToken cancel)
        {
            return ProviderRefreshExecutor.RunProviderGamesAsync(
                gamesToRefresh,
                onGameStarting,
                (game, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var paths = SpartanPortPaths.Resolve(game, ProviderSettings, _api);
                    var result = paths == null
                        ? ProviderRefreshExecutor.ProviderGameResult.Skipped()
                        : new ProviderRefreshExecutor.ProviderGameResult
                        {
                            Data = LoadGame(game, paths)
                        };
                    return Task.FromResult(result);
                },
                onGameCompleted,
                null,
                (game, error, count) =>
                    _logger?.Warn(error, $"[SpartanPort] Failed to read trophies for '{game?.Name}'."),
                null,
                null,
                cancel);
        }

        private GameAchievementData LoadGame(Game game, SpartanPortPaths paths)
        {
            var texts = SpartanPortTrophyMetadata.Load(paths.UserDirectory);
            var hasSave = SpartanPortTrophyReader.TryReadUnlocks(paths.TrophyFile, out var unlocked);
            var icons = GetCachedIcons(paths.TrophyPackage);
            var rows = new List<AchievementDetail>(SpartanPortTrophyReader.TrophyCount);

            for (var id = 0; id < SpartanPortTrophyReader.TrophyCount; id++)
            {
                icons.TryGetValue(id, out var artwork);
                var trophy = texts[id];
                rows.Add(new AchievementDetail
                {
                    ApiName = ApiName(id),
                    DisplayName = trophy.Name,
                    Description = trophy.Description,
                    Unlocked = hasSave && unlocked.Contains(id),
                    UnlockTimeUtc = null,
                    TrophyType = SpartanPortTrophyMetadata.TrophyType(id),
                    IsCapstone = id == 0,
                    UnlockedIconPath = artwork?.Item1,
                    LockedIconPath = artwork?.Item2,
                    Category = "God of War III Remastered"
                });
            }

            return new GameAchievementData
            {
                PlayniteGameId = game.Id,
                ProviderKey = ProviderKey,
                ProviderGameKey = "gow3-remastered-spartanport",
                LibrarySourceName = game.Source?.Name,
                GameName = game.Name,
                HasAchievements = true,
                LastUpdatedUtc = DateTime.UtcNow,
                Achievements = rows
            };
        }

        private Dictionary<int, Tuple<string, string>> GetCachedIcons(string trpPath)
        {
            var result = new Dictionary<int, Tuple<string, string>>();
            if (string.IsNullOrWhiteSpace(trpPath) || string.IsNullOrWhiteSpace(_pluginUserDataPath))
            {
                return result;
            }

            var icons = SpartanPortTrophyReader.ReadIcons(trpPath);
            if (icons.Count == 0)
            {
                return result;
            }

            string identity;
            using (var sha = SHA256.Create())
            {
                identity = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    Path.GetFullPath(trpPath).ToLowerInvariant()))).Replace("-", "").Substring(0, 16);
            }

            var folder = Path.Combine(_pluginUserDataPath, "SpartanPort", identity);
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var icon in icons)
                {
                    var unlocked = Path.Combine(folder, $"trophy_{icon.Key:D3}.png");
                    var locked = Path.Combine(folder, $"trophy_{icon.Key:D3}_locked.png");
                    var imageChanged = !File.Exists(unlocked) ||
                        !File.ReadAllBytes(unlocked).SequenceEqual(icon.Value);
                    if (imageChanged)
                    {
                        File.WriteAllBytes(unlocked, icon.Value);
                    }

                    if (imageChanged || !File.Exists(locked))
                    {
                        try
                        {
                            CreateLockedIcon(icon.Value, locked);
                        }
                        catch (ArgumentException)
                        {
                            if (File.Exists(locked))
                            {
                                File.Delete(locked);
                            }
                        }
                        catch (System.Runtime.InteropServices.ExternalException)
                        {
                            if (File.Exists(locked))
                            {
                                File.Delete(locked);
                            }
                        }
                    }

                    result[icon.Key] = Tuple.Create(unlocked, File.Exists(locked) ? locked : unlocked);
                }
            }
            catch (IOException ex)
            {
                _logger?.Warn(ex, "[SpartanPort] Could not cache trophy icons.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.Warn(ex, "[SpartanPort] Could not cache trophy icons.");
            }

            return result;
        }

        private static void CreateLockedIcon(byte[] png, string destination)
        {
            using (var input = new MemoryStream(png))
            using (var original = Image.FromStream(input))
            using (var gray = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(gray))
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix(new[]
                {
                    new[] { 0.18f, 0.18f, 0.18f, 0f, 0f },
                    new[] { 0.35f, 0.35f, 0.35f, 0f, 0f },
                    new[] { 0.07f, 0.07f, 0.07f, 0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { 0f, 0f, 0f, 0f, 1f }
                });
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(original,
                    new Rectangle(0, 0, gray.Width, gray.Height),
                    0, 0, original.Width, original.Height,
                    GraphicsUnit.Pixel, attributes);
                gray.Save(destination, ImageFormat.Png);
            }
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

            var paths = SpartanPortPaths.Resolve(game, ProviderSettings, _api);
            if (paths == null || !Directory.Exists(paths.UserDirectory))
            {
                return null;
            }

            return new InGameProgressRegistration
            {
                ProviderKey = ProviderKey,
                State = paths.TrophyFile,
                WatchTargets = new[] { paths.TrophyFile },
                PollInterval = InGameProgressRegistration.FileWatchSafetyPollInterval,
                UnlockAnchorPolicy = InGameUnlockAnchorPolicy.SourceObservation
            };
        }

        Task<IReadOnlyList<InGameProgressQueryResult>> IInGameProgressSource.QueryAsync(
            IReadOnlyList<InGameTrackingContext> games,
            CancellationToken cancellationToken)
        {
            var results = new List<InGameProgressQueryResult>();
            foreach (var context in games ?? Array.Empty<InGameTrackingContext>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var gameId = context?.Game?.Id ?? Guid.Empty;
                var path = context?.Registration?.State as string;
                if (!SpartanPortTrophyReader.TryReadUnlocks(path, out var unlocked))
                {
                    results.Add(InGameProgressQueryResult.Failed(gameId, "file_missing_or_unstable"));
                    continue;
                }

                var achievements = unlocked.OrderBy(id => id)
                    .Select(id => new AchievementProgressObservation
                    {
                        ApiName = ApiName(id),
                        Unlocked = true
                    }).ToList();
                results.Add(InGameProgressQueryResult.Succeeded(gameId, achievements));
            }

            return Task.FromResult<IReadOnlyList<InGameProgressQueryResult>>(results);
        }

        private static string ApiName(int id)
        {
            return $"gow3_spartanport_{id:D3}";
        }

        public ProviderSettingsViewBase CreateSettingsView()
        {
            return new SpartanPortSettingsView(_api);
        }
    }
}

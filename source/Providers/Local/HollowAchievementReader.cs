using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Providers.ReXGlue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayniteAchievements.Providers.Local
{
    internal static class HollowAchievementReader
    {
        internal const string Format = "AchievementJson";
        internal const string HollowTitleId = "4D53082D";
        private const int MaxJsonBytes = 2 * 1024 * 1024;
        private static readonly Regex GameName = new Regex(
            @"\b(?:Gears?\s+(?:of\s+)?War\s*2|GOW\s*2)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static bool MatchesGameName(string name) =>
            !string.IsNullOrWhiteSpace(name) && GameName.IsMatch(name);

        internal static LocalAchievementConfiguration Detect(Game game, IPlayniteAPI api)
        {
            if (game == null) return null;
            foreach (var root in ReXGluePaths.FindGameRoots(game, api))
            {
                if (!MatchesGameName(game.Name) && !MatchesGameName(Path.GetFileName(root))) continue;
                var state = Path.Combine(root, "GearGame", "SaveData", "Achievements.json");
                if (!File.Exists(state)) continue;
                var executable = new[]
                {
                    Path.Combine(root, "Binaries", "GearGame.exe"),
                    Path.Combine(root, "Binaries", "Win32", "GearGame.exe"),
                    Path.Combine(root, "GearGame", "Binaries", "GearGame.exe")
                };
                if (!executable.Any(File.Exists)) continue;
                if (!TryRead(state, out _, out _, out var declaredTitleId)) continue;
                if (declaredTitleId != null &&
                    !string.Equals(declaredTitleId, HollowTitleId, StringComparison.OrdinalIgnoreCase)) continue;
                return new LocalAchievementConfiguration
                {
                    GameId = game.Id,
                    Format = Format,
                    ProgressPath = state,
                    TitleId = HollowTitleId,
                    Monitor = true
                };
            }
            return null;
        }

        internal static bool TryRead(string path, out List<ReXGlueAchievement> catalog,
            out Dictionary<uint, DateTime?> unlocks, out string titleId)
        {
            catalog = new List<ReXGlueAchievement>();
            unlocks = new Dictionary<uint, DateTime?>();
            titleId = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.Length > MaxJsonBytes) return false;

                JObject document;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var text = new StreamReader(stream, new UTF8Encoding(false, true), true))
                using (var reader = new JsonTextReader(text) { MaxDepth = 32 })
                {
                    document = JObject.Load(reader);
                    if (reader.Read()) return false;
                }

                if (!TryTitleId(document["title_id"] ?? document["titleId"], out var parsedTitleId))
                    return false;
                if (!(document["achievements"] is JArray rows) || rows.Count == 0 || rows.Count > 256)
                    return false;

                var seen = new HashSet<uint>();
                var definitions = new List<ReXGlueAchievement>(rows.Count);
                var earned = new Dictionary<uint, DateTime?>();
                foreach (var token in rows)
                {
                    if (!(token is JObject row) ||
                        !TryUnsigned(row["id"], out var id) || id == 0 || !seen.Add(id) ||
                        row["name"]?.Type != JTokenType.String ||
                        row["description"]?.Type != JTokenType.String ||
                        row["unlocked"]?.Type != JTokenType.Boolean ||
                        !TryUnsigned(row["gamerscore"], out var points) || points > 10000 ||
                        !TryHidden(row["hidden"], out var hidden))
                        return false;

                    var name = row.Value<string>("name");
                    if (string.IsNullOrWhiteSpace(name)) return false;
                    var icon = row["icon_path"] ?? row["icon"];
                    if (icon != null && icon.Type != JTokenType.String) return false;
                    var iconPath = icon?.Value<string>();
                    if (!IsSafeIconPath(iconPath)) return false;
                    definitions.Add(new ReXGlueAchievement
                    {
                        Id = id,
                        Name = name,
                        Description = row.Value<string>("description"),
                        LockedDescription = row.Value<string>("description"),
                        Gamerscore = (int)points,
                        Hidden = hidden,
                        IconPath = iconPath
                    });
                    if (row.Value<bool>("unlocked")) earned.Add(id, null);
                }

                titleId = parsedTitleId;
                catalog = definitions;
                unlocks = earned;
                return true;
            }
            catch (JsonException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (OverflowException) { return false; }
        }

        private static bool TryUnsigned(JToken token, out uint value)
        {
            value = 0;
            if (token?.Type != JTokenType.Integer) return false;
            var number = token.Value<long>();
            if (number < 0 || number > uint.MaxValue) return false;
            value = (uint)number;
            return true;
        }

        private static bool TryHidden(JToken token, out bool hidden)
        {
            hidden = false;
            if (token == null) return true;
            if (token.Type == JTokenType.Boolean)
            {
                hidden = token.Value<bool>();
                return true;
            }
            if (!TryUnsigned(token, out var flag) || flag > 1) return false;
            hidden = flag == 1;
            return true;
        }

        private static bool TryTitleId(JToken token, out string titleId)
        {
            titleId = null;
            if (token == null) return true;
            if (token.Type == JTokenType.Integer)
            {
                if (!TryUnsigned(token, out var numeric) || numeric == 0) return false;
                titleId = numeric.ToString("X8");
                return true;
            }
            if (token.Type != JTokenType.String) return false;
            var value = token.Value<string>()?.Trim();
            if (value?.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true)
                value = value.Substring(2);
            if (value == null || !Regex.IsMatch(value, "^[0-9a-fA-F]{8}$") || value == "00000000")
                return false;
            titleId = value.ToUpperInvariant();
            return true;
        }

        private static bool IsSafeIconPath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) return true;
            if (Path.IsPathRooted(relative) || relative.Contains(":")) return false;
            if (relative.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(segment => segment == ".." || segment == ".")) return false;
            var extension = Path.GetExtension(relative);
            return new[] { ".png", ".jpg", ".jpeg", ".webp" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
    }
}

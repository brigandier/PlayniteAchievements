using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayniteAchievements.Providers.ReXGlue
{
    internal sealed class ReXGlueAchievement
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string LockedDescription { get; set; }
        public string IconPath { get; set; }
        public int Gamerscore { get; set; }
        public bool Hidden { get; set; }
    }

    internal static class ReXGlueAchievementReader
    {
        private const int MaxCatalogBytes = 1024 * 1024 * 2;
        private const int MaxSaveBytes = 1024 * 1024;
        private static readonly Regex TitleIdHeader = new Regex(
            @"^\s*#\s*Title\s*ID\s*:\s*([0-9A-Fa-f]{8})\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex UnlockSection = new Regex(
            @"^\[unlocked\.(\d+)\]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool TryReadCatalog(
            string path, out List<ReXGlueAchievement> achievements, out string titleId)
        {
            achievements = new List<ReXGlueAchievement>();
            titleId = null;
            if (!CanRead(path, MaxCatalogBytes))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetExtension(path), ".tsv",
                    StringComparison.OrdinalIgnoreCase)
                    ? ReadTabSeparated(path, out achievements)
                    : ReadTomlCatalog(path, out achievements, out titleId);
            }
            catch (IOException)
            {
                achievements = new List<ReXGlueAchievement>();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                achievements = new List<ReXGlueAchievement>();
                return false;
            }
        }

        private static bool ReadTomlCatalog(
            string path, out List<ReXGlueAchievement> achievements, out string titleId)
        {
            achievements = new List<ReXGlueAchievement>();
            titleId = null;
            ReXGlueAchievement current = null;
            ReXGlueAchievement portuguese = null;
            var hasId = false;
            var table = "default";

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                var match = TitleIdHeader.Match(line);
                if (match.Success)
                {
                    titleId = match.Groups[1].Value.ToUpperInvariant();
                }
                if (line == "[[achievements]]")
                {
                    AppendLocalized(achievements, current, portuguese, hasId);
                    current = new ReXGlueAchievement();
                    portuguese = null;
                    hasId = false;
                    table = "default";
                    continue;
                }

                if (line.StartsWith("[achievements.", StringComparison.Ordinal) &&
                    line.EndsWith("]", StringComparison.Ordinal))
                {
                    var language = line.Substring("[achievements.".Length,
                        line.Length - "[achievements.".Length - 1).Trim('"');
                    var preferred = string.Equals(language, "Portuguese",
                        StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(language, "Brazilian Portuguese",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(language, "pt-BR",
                            StringComparison.OrdinalIgnoreCase);
                    table = preferred ? "pt" : "other";
                    if (preferred)
                    {
                        portuguese = new ReXGlueAchievement();
                    }
                    continue;
                }

                if (current == null || line.Length == 0 || line[0] == '#' || table == "other")
                {
                    continue;
                }

                var equals = line.IndexOf('=');
                if (equals < 1)
                {
                    continue;
                }
                var key = line.Substring(0, equals).Trim();
                var value = line.Substring(equals + 1).Trim();
                var target = table == "pt" ? portuguese : current;
                if (target == null)
                {
                    continue;
                }

                switch (key)
                {
                    case "id":
                        if (table != "default") break;
                        hasId = TryParseUInt(value, out var id) && id != 0;
                        if (hasId) current.Id = id;
                        break;
                    case "label":
                        target.Name = ParseString(value);
                        break;
                    case "description":
                        target.Description = ParseString(value);
                        break;
                    case "unachieved_description":
                        target.LockedDescription = ParseString(value);
                        break;
                    case "icon_path":
                        if (table == "default") target.IconPath = ParseString(value);
                        break;
                    case "gamerscore":
                        if (table == "default" &&
                            int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out var points) && points >= 0)
                        {
                            current.Gamerscore = points;
                        }
                        break;
                    case "image_id":
                        if (table == "default" &&
                            string.IsNullOrWhiteSpace(current.IconPath) &&
                            TryParseUInt(value, out var image))
                        {
                            current.IconPath = Path.Combine("icons", image + ".png");
                        }
                        break;
                    case "flags":
                        if (table == "default" && TryParseUInt(value, out var flags))
                        {
                            current.Hidden = (flags & 8) == 0;
                        }
                        break;
                }
            }

            AppendLocalized(achievements, current, portuguese, hasId);

            return Normalize(achievements);
        }

        private static void AppendLocalized(List<ReXGlueAchievement> result,
            ReXGlueAchievement original, ReXGlueAchievement localized, bool hasId)
        {
            if (original == null || !hasId || string.IsNullOrWhiteSpace(original.Name))
            {
                return;
            }

            if (localized != null)
            {
                if (!string.IsNullOrWhiteSpace(localized.Name)) original.Name = localized.Name;
                if (!string.IsNullOrWhiteSpace(localized.Description))
                    original.Description = localized.Description;
                if (!string.IsNullOrWhiteSpace(localized.LockedDescription))
                    original.LockedDescription = localized.LockedDescription;
            }
            result.Add(original);
        }

        private static bool ReadTabSeparated(string path, out List<ReXGlueAchievement> achievements)
        {
            achievements = new List<ReXGlueAchievement>();
            var byId = new Dictionary<uint, ReXGlueAchievement>();
            var texts = new Dictionary<uint, Dictionary<string, string[]>>();

            foreach (var raw in File.ReadLines(path))
            {
                var columns = raw.Split('\t');
                if (columns.Length < 2 || !TryParseUInt(columns[1], out var id) || id == 0)
                {
                    continue;
                }

                if (columns[0] == "A" && columns.Length >= 6)
                {
                    var item = new ReXGlueAchievement { Id = id, Hidden = columns[5] == "1" };
                    if (int.TryParse(columns[3], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var points) && points > 0)
                    {
                        item.Gamerscore = points;
                    }
                    if (TryParseUInt(columns[4], out var image))
                    {
                        item.IconPath = Path.Combine("icons", image + ".png");
                    }
                    byId[id] = item;
                }
                else if (columns[0] == "T" && columns.Length >= 6)
                {
                    if (!texts.TryGetValue(id, out var languages))
                    {
                        languages = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                        texts[id] = languages;
                    }
                    languages[columns[2]] = columns;
                }
            }

            foreach (var item in byId.Values.OrderBy(x => x.Id))
            {
                if (!texts.TryGetValue(item.Id, out var languageTexts))
                {
                    continue;
                }
                if (!languageTexts.TryGetValue("pt", out var row) &&
                    !languageTexts.TryGetValue("en", out row))
                {
                    continue;
                }
                item.Name = row[3];
                item.Description = row[4];
                item.LockedDescription = row[5];
                achievements.Add(item);
            }
            return Normalize(achievements);
        }

        public static bool TryReadUnlocks(string path, out Dictionary<uint, DateTime?> unlocks)
        {
            unlocks = new Dictionary<uint, DateTime?>();
            if (!CanRead(path, MaxSaveBytes))
            {
                return false;
            }

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    uint? currentId = null;
                    var readingLegacyList = false;
                    var legacy = new StringBuilder();
                    var recognized = false;

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine()?.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }
                        if (line.StartsWith("#"))
                        {
                            if (line.IndexOf("Achievement unlock state",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                recognized = true;
                            }
                            continue;
                        }

                        var section = UnlockSection.Match(line);
                        if (section.Success &&
                            uint.TryParse(section.Groups[1].Value, NumberStyles.None,
                                CultureInfo.InvariantCulture, out var id) && id != 0)
                        {
                            currentId = id;
                            unlocks[id] = null;
                            recognized = true;
                            readingLegacyList = false;
                            continue;
                        }

                        if (line.StartsWith("[") && line.EndsWith("]") && !line.StartsWith("[unlocked."))
                        {
                            currentId = null;
                            continue;
                        }

                        if (currentId.HasValue && line.StartsWith("filetime", StringComparison.Ordinal))
                        {
                            var equals = line.IndexOf('=');
                            if (equals > 0 && long.TryParse(
                                line.Substring(equals + 1).Trim(),
                                NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) && ticks > 0)
                            {
                                try
                                {
                                    unlocks[currentId.Value] = DateTime.FromFileTimeUtc(ticks);
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                }
                            }
                            continue;
                        }

                        if (!currentId.HasValue && (readingLegacyList ||
                            line.StartsWith("unlocked", StringComparison.Ordinal)))
                        {
                            var value = readingLegacyList ? line : line.Substring(line.IndexOf('=') + 1);
                            legacy.Append(value);
                            if (value.Contains("]"))
                            {
                                recognized = true;
                                readingLegacyList = false;
                                foreach (Match entry in Regex.Matches(legacy.ToString(), @"\d+"))
                                {
                                    if (uint.TryParse(entry.Value, NumberStyles.None,
                                        CultureInfo.InvariantCulture, out var oldId) && oldId != 0)
                                    {
                                        unlocks[oldId] = null;
                                    }
                                }
                                legacy.Clear();
                            }
                            else
                            {
                                readingLegacyList = true;
                            }
                        }
                    }
                    return recognized;
                }
            }
            catch (IOException)
            {
                unlocks = new Dictionary<uint, DateTime?>();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                unlocks = new Dictionary<uint, DateTime?>();
                return false;
            }
        }

        public static string ResolveIcon(string catalogPath, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(catalogPath) || string.IsNullOrWhiteSpace(iconPath))
            {
                return null;
            }

            try
            {
                var root = Path.GetFullPath(Path.GetDirectoryName(catalogPath));
                var candidate = Path.GetFullPath(Path.Combine(root, iconPath));
                var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                       File.Exists(candidate) ? candidate : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        private static bool Normalize(List<ReXGlueAchievement> items)
        {
            if (items.Count == 0 || items.Count > 1000)
            {
                return false;
            }
            var distinct = items.GroupBy(x => x.Id)
                .Select(group => group.First()).ToList();
            if (distinct.Count != items.Count)
            {
                items.Clear();
                items.AddRange(distinct);
            }
            return true;
        }

        private static bool CanRead(string path, long maxSize)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                var info = new FileInfo(path);
                return info.Exists && info.Length > 0 && info.Length <= maxSize;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryParseUInt(string value, out uint number)
        {
            number = 0;
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(value.Substring(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out number);
            }
            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }

        private static string ParseString(string raw)
        {
            if (raw.Length < 2 || raw[0] != '"')
            {
                return raw;
            }
            var result = new StringBuilder();
            for (var i = 1; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c == '"') break;
                if (c != '\\' || i + 1 >= raw.Length)
                {
                    result.Append(c);
                    continue;
                }
                var escaped = raw[++i];
                switch (escaped)
                {
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case 'u':
                        if (i + 4 < raw.Length &&
                            int.TryParse(raw.Substring(i + 1, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out var unicode))
                        {
                            result.Append((char)unicode);
                            i += 4;
                        }
                        break;
                    default: result.Append(escaped); break;
                }
            }
            return result.ToString();
        }
    }
}

using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Common;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PlayniteAchievements.Providers.SpartanPort
{
    internal sealed class SpartanPortPaths
    {
        public string Root { get; private set; }
        public string UserDirectory { get; private set; }
        public string TrophyFile => Path.Combine(UserDirectory, "trophies.dat");
        public string TrophyPackage { get; private set; }

        public static SpartanPortPaths Resolve(Game game, SpartanPortSettings settings, IPlayniteAPI api)
        {
            if (!IsGodOfWarThree(game?.Name))
            {
                return null;
            }

            var overrideRoot = Expand(settings?.GameRoot);
            var root = ResolvePortDirectory(game, api, overrideRoot);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var manifestPath = Path.Combine(root, "settings.json");
            JObject manifest = null;
            if (File.Exists(manifestPath))
            {
                try
                {
                    manifest = JObject.Parse(File.ReadAllText(manifestPath));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (Newtonsoft.Json.JsonException)
                {
                }
            }

            var explicitRoot = !string.IsNullOrWhiteSpace(overrideRoot);
            var nativeMarker = HasPortMarker(root);
            if (!explicitRoot && !nativeMarker)
            {
                return null;
            }

            var explicitUser = Expand(settings?.UserDirectory);
            var manifestUser = ResolveManifestPath(root, manifest, "user_dir");
            var environmentUser = Expand(Environment.GetEnvironmentVariable("SPARTAN_USER_DIR"));
            var user = FirstNonEmpty(explicitUser, manifestUser, environmentUser,
                Path.Combine(root, "user"));

            var update = ResolveManifestPath(root, manifest, "update_dir");
            var gameFolder = ResolveManifestPath(root, manifest, "game_dir");
            var trp = FindPackage(update) ?? FindPackage(gameFolder) ?? FindPackage(root);
            return new SpartanPortPaths
            {
                Root = root,
                UserDirectory = user,
                TrophyPackage = trp
            };
        }

        private static string ResolvePortDirectory(Game game, IPlayniteAPI api, string overrideRoot)
        {
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Directory.Exists(overrideRoot) ? overrideRoot : null;
            }

            var candidates = new System.Collections.Generic.List<string>
            {
                Expand(Environment.GetEnvironmentVariable("SPARTAN_DATA_DIR")),
                Expand(PathExpansion.ExpandGamePath(api, game, game.InstallDirectory))
            };
            if (game.GameActions != null)
            {
                foreach (var action in game.GameActions)
                {
                    var executable = Expand(PathExpansion.ExpandGamePath(api, game, action?.Path));
                    if (string.IsNullOrWhiteSpace(executable))
                    {
                        continue;
                    }

                    var directory = Path.GetDirectoryName(executable);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        candidates.Add(directory);
                    }
                }
            }

            foreach (var path in candidates)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                try
                {
                    var root = Path.GetFullPath(path);
                    if (Directory.Exists(root) && HasPortMarker(root))
                    {
                        return root;
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (PathTooLongException)
                {
                }
            }

            return null;
        }

        private static bool HasPortMarker(string root)
        {
            return File.Exists(Path.Combine(root, "settings.json")) ||
                   File.Exists(Path.Combine(root, "spartanport.ini")) ||
                   File.Exists(Path.Combine(root, "SpartanPort.exe")) ||
                   File.Exists(Path.Combine(root, "Play SpartanPort.exe")) ||
                   File.Exists(Path.Combine(root, "user", "trophies.dat")) ||
                   File.Exists(Path.Combine(root, "launcher", "spartanport_trophies.py"));
        }

        private static string FirstNonEmpty(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static bool IsGodOfWarThree(string gameName)
        {
            return !string.IsNullOrWhiteSpace(gameName) &&
                Regex.IsMatch(gameName, @"\bgod\s+of\s+war\s*(?:iii|3)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string FindPackage(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return null;
            }

            var path = Path.Combine(folder, "sce_sys", "trophy", "trophy00.trp");
            return File.Exists(path) ? path : null;
        }

        private static string ResolveManifestPath(string root, JObject manifest, string key)
        {
            var value = manifest?.Value<string>(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var expanded = Expand(value);
            return Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(root, expanded));
        }

        private static string FirstExistingDirectory(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static string Expand(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null :
                Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        }
    }
}

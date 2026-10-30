using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlayniteAchievements.Providers.ReXGlue
{
    internal sealed class ReXGluePaths
    {
        public string CatalogPath { get; private set; }
        public string SavePath { get; private set; }
        public string TitleId { get; private set; }

        public static ReXGluePaths Resolve(Game game, ReXGlueSettings settings, IPlayniteAPI api)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.Name))
            {
                return null;
            }

            var roots = FindGameRoots(game, api);
            if (roots.Count == 0)
            {
                return null;
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var catalog = FindCatalog(roots, game.Name, documents);
            if (catalog == null ||
                !ReXGlueAchievementReader.TryReadCatalog(catalog, out var achievements, out var titleId) ||
                achievements.Count == 0)
            {
                return null;
            }

            if (string.Equals(Path.GetExtension(catalog), ".tsv", StringComparison.OrdinalIgnoreCase) &&
                IsArmyOfTwo(game.Name))
            {
                titleId = "4541084C";
            }

            var rootsForSaves = new List<string>();
            var userOverride = NormalizePath(settings?.UserDataRoot);
            if (userOverride != null)
            {
                rootsForSaves.Add(userOverride);
            }
            else
            {
                foreach (var root in roots)
                {
                    rootsForSaves.Add(Path.Combine(root, "user_data"));
                    rootsForSaves.Add(root);
                }

                if (IsArmyOfTwo(game.Name))
                {
                    rootsForSaves.Add(Path.Combine(documents, "armyoftworecomp_tu2"));
                }

                foreach (var root in roots)
                {
                    var stem = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));
                    if (!string.IsNullOrWhiteSpace(stem))
                    {
                        rootsForSaves.Add(Path.Combine(documents, stem));
                    }
                }
                rootsForSaves.Add(Path.Combine(documents,
                    Regex.Replace(game.Name, @"[^a-zA-Z0-9]", "").ToLowerInvariant()));
            }

            var savePath = FindSavePath(rootsForSaves, titleId);
            if (!string.IsNullOrWhiteSpace(savePath) && string.IsNullOrWhiteSpace(titleId))
            {
                titleId = Path.GetFileNameWithoutExtension(savePath).ToUpperInvariant();
            }

            return new ReXGluePaths
            {
                CatalogPath = catalog,
                TitleId = titleId,
                SavePath = savePath
            };
        }

        internal static string FindSavePath(IEnumerable<string> userRoots, string titleId)
        {
            foreach (var root in userRoots.Where(x => !string.IsNullOrWhiteSpace(x))
                                          .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var achievementDir = Path.Combine(root, "achievements");
                try
                {
                    if (!Directory.Exists(achievementDir))
                    {
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(titleId))
                    {
                        var expected = Path.Combine(achievementDir, titleId + ".toml");
                        if (File.Exists(expected))
                        {
                            return expected;
                        }

                        var match = Directory.EnumerateFiles(achievementDir, "*.toml",
                                SearchOption.TopDirectoryOnly)
                            .FirstOrDefault(path => string.Equals(
                                Path.GetFileNameWithoutExtension(path), titleId,
                                StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            return match;
                        }
                    }
                    else
                    {
                        var files = Directory.EnumerateFiles(achievementDir, "*.toml",
                                SearchOption.TopDirectoryOnly)
                            .Take(2).ToList();
                        if (files.Count == 1 &&
                            Regex.IsMatch(Path.GetFileNameWithoutExtension(files[0]),
                                "^[0-9a-fA-F]{8}$"))
                        {
                            return files[0];
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            return null;
        }

        private static string FindCatalog(List<string> roots, string gameName, string documents)
        {
            foreach (var root in roots)
            {
                var candidates = new[]
                {
                    Path.Combine(root, "metadata", "achievements.toml"),
                    Path.Combine(root, "achievements.toml"),
                    Path.Combine(root, "cache", "achievements", "catalog.tsv"),
                    Path.Combine(root, "user_data", "cache", "achievements", "catalog.tsv")
                };
                foreach (var file in candidates)
                {
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }
            }

            if (IsArmyOfTwo(gameName) && !string.IsNullOrWhiteSpace(documents))
            {
                var armyCatalog = Path.Combine(documents, "armyoftworecomp_tu2",
                    "cache", "achievements", "catalog.tsv");
                if (File.Exists(armyCatalog))
                {
                    return armyCatalog;
                }
            }

            return null;
        }

        private static List<string> FindGameRoots(Game game, IPlayniteAPI api)
        {
            var list = new List<string>();
            var installed = NormalizePath(ExpandGamePath(api, game, game.InstallDirectory));
            AddFolder(list, installed);
            if (game.GameActions != null)
            {
                foreach (var action in game.GameActions)
                {
                    var value = ExpandGamePath(api, game, action?.Path);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }
                    try
                    {
                        var executable = NormalizePath(value);
                        if (executable != null)
                        {
                            var directory = Path.GetDirectoryName(executable);
                            AddFolder(list, directory);
                            if (!Path.IsPathRooted(value) && installed != null)
                            {
                                AddFolder(list, Path.GetDirectoryName(Path.Combine(installed, value)));
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            foreach (var root in list.ToArray())
            {
                try
                {
                    AddFolder(list, Directory.GetParent(root)?.FullName);
                }
                catch (ArgumentException)
                {
                }
            }
            return list;
        }

        private static string ExpandGamePath(IPlayniteAPI api, Game game, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return PathExpansion.ExpandGamePath(api, game, path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void AddFolder(List<string> result, string candidate)
        {
            if (candidate == null || !Directory.Exists(candidate))
            {
                return;
            }
            var path = Path.GetFullPath(candidate);
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(path);
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
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

        private static bool IsArmyOfTwo(string name)
        {
            return Regex.IsMatch(name ?? "", @"^Army\s+of\s+Two(?:\s*\(2008\))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}

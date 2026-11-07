using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Models.Achievements;
using PlayniteAchievements.Providers.ReXGlue;
using PlayniteAchievements.Providers.SpartanPort;
using PlayniteAchievements.Providers.Unleashed;
using PlayniteAchievements.Providers.Xenia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlayniteAchievements.Providers.Local
{
    internal static class LocalAchievementLoader
    {
        internal static GameAchievementData Load(Game game, LocalAchievementConfiguration configuration,
            string dataPath, ILogger logger)
        {
            if (game == null || configuration == null || configuration.GameId != game.Id)
                throw new InvalidDataException("Selecione o jogo associado à configuração.");

            var reader = LocalProgressReaders.Resolve(configuration.Format, configuration.ProgressPath);
            if (reader == null || !reader.TryRead(configuration.ProgressPath, out var unlocks))
                throw new InvalidDataException("O arquivo de progresso está indisponível ou não é válido para o formato selecionado.");

            var catalogPath = configuration.CatalogPath;
            var titleId = string.IsNullOrWhiteSpace(configuration.TitleId) ? null : configuration.TitleId.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(titleId) && !Regex.IsMatch(titleId, "^[0-9A-F]{8}$"))
                throw new InvalidDataException("O Title ID deve conter oito dígitos hexadecimais.");
            if (reader.Format == "ReXGlue")
            {
                var fileTitle = Path.GetFileNameWithoutExtension(configuration.ProgressPath);
                if (Regex.IsMatch(fileTitle ?? "", "^[0-9A-Fa-f]{8}$"))
                {
                    RequireSameTitle(titleId, fileTitle);
                    titleId = fileTitle.ToUpperInvariant();
                }
            }

            List<ReXGlueAchievement> catalog;
            if (reader.Format == HollowAchievementReader.Format)
            {
                if (!string.IsNullOrWhiteSpace(catalogPath) &&
                    !string.Equals(Path.GetFullPath(catalogPath), Path.GetFullPath(configuration.ProgressPath),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Este JSON contém o catálogo e o progresso; selecione o mesmo arquivo ou deixe o catálogo em branco.");
                if (!HollowAchievementReader.TryRead(configuration.ProgressPath, out catalog, out unlocks, out var jsonTitleId))
                    throw new InvalidDataException("O JSON de conquistas está incompleto ou é incompatível.");
                RequireSameTitle(titleId, jsonTitleId);
                titleId = titleId ?? jsonTitleId;
                catalogPath = configuration.ProgressPath;
            }
            else if (reader.Format == "SpartanPort" && string.IsNullOrWhiteSpace(catalogPath))
            {
                var texts = SpartanPortTrophyMetadata.Load(Path.GetDirectoryName(configuration.ProgressPath));
                catalog = texts.Select((text, id) => new ReXGlueAchievement
                {
                    Id = (uint)id, Name = text.Name, Description = text.Description
                }).ToList();
            }
            else
            {
                if (string.Equals(Path.GetExtension(catalogPath), ".xex", StringComparison.OrdinalIgnoreCase))
                {
                    var originalTitle = XeniaTitleIDExtractor.GetFromXexFile(catalogPath)?.TitleIdHex;
                    RequireSameTitle(titleId, originalTitle);
                    titleId = originalTitle;
                    catalogPath = RecompXdbfExtractor.TryExtract(catalogPath, titleId, dataPath, logger);
                }
                if (!ReXGlueAchievementReader.TryReadCatalog(catalogPath, out catalog, out var catalogTitle))
                    throw new InvalidDataException("Selecione um catálogo TOML/TSV válido ou o default.xex original.");
                RequireSameTitle(titleId, catalogTitle);
                titleId = titleId ?? catalogTitle;
            }

            if (configuration.IncludeReblueExtras)
            {
                if (reader.Format != "ReXGlue" || !string.Equals(titleId, "4D5307DF", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("As conquistas adicionais do re:Blue exigem o Title ID 4D5307DF e o formato ReXGlue.");
                ReblueExtraAchievements.AppendTo(catalog);
            }
            if (reader.Format == "UnleashedRecompiled" && catalog.Any(item => item.Id > ushort.MaxValue))
                throw new InvalidDataException("O catálogo contém IDs incompatíveis com ACH-DATA.");
            if (reader.Format == "SpartanPort" && catalog.Any(item => item.Id >= SpartanPortTrophyReader.TrophyCount))
                throw new InvalidDataException("O catálogo contém IDs incompatíveis com SpartanPort.");
            var known = new HashSet<uint>(catalog.Select(item => item.Id));
            var unknown = unlocks.Keys.Where(id => !known.Contains(id)).ToArray();
            if (unknown.Length > 0)
                throw new InvalidDataException("O catálogo não contém os IDs desbloqueados: " + string.Join(", ", unknown));
            if (!string.IsNullOrWhiteSpace(configuration.IconDirectory) && !Directory.Exists(configuration.IconDirectory))
                throw new InvalidDataException("O diretório de ícones não existe.");

            return new GameAchievementData
            {
                PlayniteGameId = game.Id,
                ProviderKey = "LocalAchievements",
                MetadataHints = new MetadataHints { PlatformSlug = reader.Format == "SpartanPort" ? "ps4" : "xbox-360" },
                ProviderGameKey = reader.Format + ":" + (titleId ?? game.Id.ToString()),
                LibrarySourceName = game.Source?.Name,
                GameName = game.Name,
                HasAchievements = catalog.Count > 0,
                LastUpdatedUtc = DateTime.UtcNow,
                Achievements = catalog.Select(entry =>
                {
                    var earned = unlocks.TryGetValue(entry.Id, out var time);
                    return new AchievementDetail
                    {
                        ApiName = reader.AchievementKey(entry.Id),
                        DisplayName = entry.Name,
                        Description = entry.Description ?? entry.LockedDescription,
                        Points = entry.Gamerscore,
                        Hidden = entry.Hidden,
                        Unlocked = earned,
                        UnlockTimeUtc = earned ? time : null,
                        UnlockedIconPath = ResolveIcon(configuration, catalogPath, entry),
                        TrophyType = reader.Format == "SpartanPort" ? SpartanPortTrophyMetadata.TrophyType((int)entry.Id) : null,
                        IsCapstone = reader.Format == "SpartanPort" && entry.Id == 0,
                        Category = reader.Format == "SpartanPort" ? "God of War III Remastered" : "Xbox 360"
                    };
                }).ToList()
            };
        }

        private static void RequireSameTitle(string expected, string actual)
        {
            if (!string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(actual) &&
                !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Title ID incompatível: {expected} / {actual}.");
        }

        private static string ResolveIcon(LocalAchievementConfiguration configuration, string catalogPath, ReXGlueAchievement entry)
        {
            if (!string.IsNullOrWhiteSpace(configuration.IconDirectory))
            {
                var name = Path.GetFileName(entry.IconPath ?? entry.Id + ".png");
                var candidate = Path.Combine(configuration.IconDirectory, name);
                if (File.Exists(candidate)) return candidate;
            }
            return ReXGlueAchievementReader.ResolveIcon(catalogPath, entry.IconPath) ??
                (configuration.IncludeReblueExtras ? ReblueExtraAchievements.FindIcon(entry.Id) : null);
        }

        internal static LocalAchievementConfiguration Detect(Game game, IPlayniteAPI api)
        {
            var hollow = HollowAchievementReader.Detect(game, api);
            if (hollow != null) return hollow;

            var roots = ReXGluePaths.FindGameRoots(game, api);
            if (roots.Any(root => File.Exists(Path.Combine(root, "UnleashedRecomp.exe"))))
            {
                var save = roots.Select(root => UnleashedAchievementReader.FindSavePath(root,
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))).FirstOrDefault(File.Exists);
                if (save != null)
                    return new LocalAchievementConfiguration { GameId = game.Id, Format = "UnleashedRecompiled",
                        ProgressPath = save, CatalogPath = ReXGluePaths.FindXexPath(roots), TitleId = "53450812" };
            }
            var rex = ReXGluePaths.Resolve(game, ProviderRegistry.Settings<ReXGlueSettings>(), api);
            if (rex != null && !string.IsNullOrWhiteSpace(rex.SavePath))
                return new LocalAchievementConfiguration { GameId = game.Id, Format = "ReXGlue",
                    ProgressPath = rex.SavePath, CatalogPath = rex.CatalogPath ?? rex.XexPath,
                    TitleId = rex.TitleId, IncludeReblueExtras = ReblueExtraAchievements.IsReblue(rex.GameRoot) };
            var spartan = SpartanPortPaths.Resolve(game, ProviderRegistry.Settings<SpartanPortSettings>(), api);
            return spartan == null ? null : new LocalAchievementConfiguration { GameId = game.Id,
                Format = "SpartanPort", ProgressPath = spartan.TrophyFile };
        }
    }
}

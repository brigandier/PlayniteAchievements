using Playnite.SDK;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PlayniteAchievements.Providers.ReXGlue
{
    internal static class RecompXdbfExtractor
    {
        private const int TimeoutMilliseconds = 90000;

        internal static string TryExtract(
            string xexPath, string expectedTitleId, string pluginDataRoot,
            ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(xexPath) ||
                !File.Exists(xexPath) ||
                string.IsNullOrWhiteSpace(pluginDataRoot))
            {
                return null;
            }

            try
            {
                var assemblyFolder = Path.GetDirectoryName(
                    typeof(RecompXdbfExtractor).Assembly.Location);
                var tool = Path.Combine(assemblyFolder, "Tools", "ReXGlue", "rexglue.exe");
                if (!File.Exists(tool))
                {
                    logger?.Warn("[Recomps] XDBF extractor is not present in the extension package.");
                    return null;
                }

                var fileInfo = new FileInfo(xexPath);
                var identity = Path.GetFullPath(xexPath).ToLowerInvariant() + "|" +
                    fileInfo.Length + "|" + fileInfo.LastWriteTimeUtc.Ticks;
                string hash;
                using (var sha = SHA256.Create())
                {
                    hash = BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(identity)))
                        .Replace("-", "").Substring(0, 24);
                }

                var outDir = Path.Combine(pluginDataRoot, "Recomps", "XDBF", hash);
                var catalog = Path.Combine(outDir, "achievements.toml");
                if (IsValidCatalog(catalog, expectedTitleId))
                {
                    return catalog;
                }

                Directory.CreateDirectory(outDir);
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = tool,
                        Arguments = "init achievements " + Quote(xexPath) +
                            " " + Quote(outDir) + " --language 1",
                        WorkingDirectory = Path.GetDirectoryName(tool),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                })
                {
                    if (!process.Start())
                    {
                        return null;
                    }
                    if (!process.WaitForExit(TimeoutMilliseconds))
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        logger?.Warn("[Recomps] XDBF extraction timed out.");
                        return null;
                    }
                    if (process.ExitCode != 0)
                    {
                        logger?.Warn($"[Recomps] XDBF extraction failed with code {process.ExitCode}.");
                        return null;
                    }
                }

                return IsValidCatalog(catalog, expectedTitleId) ? catalog : null;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                logger?.Warn(ex, "[Recomps] XDBF extractor could not start.");
                return null;
            }
            catch (IOException ex)
            {
                logger?.Warn(ex, "[Recomps] XDBF extraction I/O failed.");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                logger?.Warn(ex, "[Recomps] XDBF extraction access denied.");
                return null;
            }
            catch (ArgumentException ex)
            {
                logger?.Warn(ex, "[Recomps] Invalid XDBF extraction path.");
                return null;
            }
        }

        private static string Quote(string path)
        {
            return "\"" + path.Replace("\"", "") + "\"";
        }

        private static bool IsValidCatalog(string path, string expectedTitleId)
        {
            if (!ReXGlueAchievementReader.TryReadCatalog(
                path, out var catalog, out var titleId) ||
                catalog.Count == 0)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(expectedTitleId) ||
                string.Equals(titleId, expectedTitleId,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}

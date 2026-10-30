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
                logger?.Warn("[Recomps] XDBF extraction skipped: original XEX or " +
                    "extension data directory not found.");
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
                    logger?.Debug("[Recomps] Existing XDBF metadata cache is valid.");
                    return catalog;
                }

                logger?.Info("[Recomps] Extracting native Xbox 360 achievements from: " +
                    xexPath);
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
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                })
                {
                    var output = new StringBuilder();
                    var outputSync = new object();
                    Action<string> append = line =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;
                        lock (outputSync)
                        {
                            output.AppendLine(line);
                            if (output.Length > 12000)
                            {
                                output.Remove(0, output.Length - 12000);
                            }
                        }
                    };
                    process.OutputDataReceived += (sender, args) => append(args.Data);
                    process.ErrorDataReceived += (sender, args) => append(args.Data);
                    if (!process.Start())
                    {
                        logger?.Warn("[Recomps] XDBF process could not start.");
                        return null;
                    }
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(TimeoutMilliseconds))
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        logger?.Warn("[Recomps] XDBF extraction timed out.");
                        return null;
                    }
                    process.WaitForExit();
                    var details = output.ToString().Trim();
                    if (process.ExitCode != 0)
                    {
                        logger?.Warn($"[Recomps] XDBF extraction failed (exit {process.ExitCode}): " +
                            details);
                        return null;
                    }
                    logger?.Debug("[Recomps] XDBF extractor output: " + details);
                }

                if (!IsValidCatalog(catalog, expectedTitleId))
                {
                    logger?.Warn("[Recomps] The XDBF extractor exited successfully " +
                        "but the catalog was empty, unreadable or had a different Title ID.");
                    return null;
                }
                logger?.Info("[Recomps] Native Xbox 360 achievements extracted successfully.");
                return catalog;
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

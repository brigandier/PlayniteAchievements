using PlayniteAchievements.Providers.ReXGlue;
using PlayniteAchievements.Providers.SpartanPort;
using PlayniteAchievements.Providers.Unleashed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlayniteAchievements.Providers.Local
{
    internal interface ILocalProgressReader
    {
        string Format { get; }
        string FileFormat { get; }
        string Validation { get; }
        bool CanRead { get; }
        bool CanMonitor { get; }
        bool HasUnlockDates { get; }
        string Limitations { get; }
        string AchievementKey(uint id);
        bool TryRead(string path, out Dictionary<uint, DateTime?> unlocks);
    }

    internal static class LocalProgressReaders
    {
        internal static readonly IReadOnlyList<ILocalProgressReader> All = new ILocalProgressReader[]
        {
            new RexReader(), new UnleashedReader(), new SpartanReader()
        };

        internal static ILocalProgressReader Resolve(string format, string path)
        {
            if (!string.IsNullOrWhiteSpace(format))
            {
                return All.FirstOrDefault(reader => reader.Format == format);
            }

            // The two binary formats require their canonical filename as well as structural validation.
            var matches = All.Where(reader =>
                (reader.Format == "ReXGlue" ||
                 string.Equals(Path.GetFileName(path), reader.FileFormat, StringComparison.OrdinalIgnoreCase)) &&
                reader.TryRead(path, out _)).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        private sealed class RexReader : ILocalProgressReader
        {
            public string Format => "ReXGlue";
            public string FileFormat => "TOML [unlocked.<id>]";
            public string Validation => "Tabelas por ID e datas FILETIME";
            public bool CanRead => true;
            public bool CanMonitor => true;
            public bool HasUnlockDates => true;
            public string Limitations => "Os desbloqueios aparecem quando o runtime grava o TOML. O perfil selecionado deve ser o utilizado pelo jogo.";
            public string AchievementKey(uint id) => $"rexglue_{id:D8}";
            public bool TryRead(string path, out Dictionary<uint, DateTime?> unlocks) =>
                ReXGlueAchievementReader.TryReadUnlocks(path, out unlocks);
        }

        private sealed class UnleashedReader : ILocalProgressReader
        {
            public string Format => "UnleashedRecompiled";
            public string FileFormat => "ACH-DATA";
            public string Validation => "816 bytes, assinatura ACH, versão até 1 e checksum XOR";
            public bool CanRead => true;
            public bool CanMonitor => true;
            public bool HasUnlockDates => true;
            public string Limitations => "O jogo mantém conquistas em memória. As notificações locais dependem da gravação de ACH-DATA durante o salvamento; não são necessariamente imediatas.";
            public string AchievementKey(uint id) => $"unleashed_{id:D8}";
            public bool TryRead(string path, out Dictionary<uint, DateTime?> unlocks)
            {
                var success = UnleashedAchievementReader.TryReadUnlocks(path, out var values);
                unlocks = values.ToDictionary(pair => (uint)pair.Key, pair => pair.Value);
                return success;
            }
        }

        private sealed class SpartanReader : ILocalProgressReader
        {
            public string Format => "SpartanPort";
            public string FileFormat => "trophies.dat";
            public string Validation => "Bitset de 128 bits; IDs 0–34 do SpartanPort";
            public bool CanRead => true;
            public bool CanMonitor => true;
            public bool HasUnlockDates => false;
            public string Limitations => "Formato específico do SpartanPort de God of War III; não contém datas originais.";
            public string AchievementKey(uint id) => $"gow3_spartanport_{id:D3}";
            public bool TryRead(string path, out Dictionary<uint, DateTime?> unlocks)
            {
                var success = SpartanPortTrophyReader.TryReadUnlocks(path, out var values);
                unlocks = values.ToDictionary(id => (uint)id, id => (DateTime?)null);
                return success;
            }
        }
    }
}

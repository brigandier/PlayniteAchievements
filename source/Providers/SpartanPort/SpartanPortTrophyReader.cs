using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PlayniteAchievements.Providers.SpartanPort
{
    internal static class SpartanPortTrophyReader
    {
        internal const int TrophyCount = 35;
        private static readonly byte[] TrpMagic = { 0xDC, 0xA2, 0x4D, 0x00 };
        private static readonly byte[] PngMagic = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static bool TryReadUnlocks(string path, out HashSet<int> unlocked)
        {
            unlocked = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var bytes = new byte[16];
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length < bytes.Length || stream.Length > 4096)
                    {
                        return false;
                    }

                    var read = 0;
                    while (read < bytes.Length)
                    {
                        var next = stream.Read(bytes, read, bytes.Length - read);
                        if (next == 0)
                        {
                            return false;
                        }

                        read += next;
                    }
                }

                for (var id = 0; id < TrophyCount; id++)
                {
                    if ((bytes[id / 8] & (1 << (id % 8))) != 0)
                    {
                        unlocked.Add(id);
                    }
                }

                return true;
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

        public static Dictionary<int, byte[]> ReadIcons(string trpPath)
        {
            var result = new Dictionary<int, byte[]>();
            if (string.IsNullOrWhiteSpace(trpPath) || !File.Exists(trpPath))
            {
                return result;
            }

            byte[] bytes;
            try
            {
                var info = new FileInfo(trpPath);
                if (info.Length < 0x60 || info.Length > 64L * 1024 * 1024)
                {
                    return result;
                }

                bytes = File.ReadAllBytes(trpPath);
            }
            catch (IOException)
            {
                return result;
            }
            catch (UnauthorizedAccessException)
            {
                return result;
            }

            if (!TrpMagic.SequenceEqual(bytes.Take(4)))
            {
                return result;
            }

            var count = ReadBigEndianUInt32(bytes, 0x10);
            var entrySize = ReadBigEndianUInt32(bytes, 0x14);
            if (count == 0 || count > 4096 || entrySize < 0x30 || entrySize > 0x100)
            {
                return result;
            }

            for (uint index = 0; index < count; index++)
            {
                var entryOffset = 0x60L + index * entrySize;
                if (entryOffset + 0x30 > bytes.Length)
                {
                    break;
                }

                var start = (int)entryOffset;
                var end = Array.IndexOf(bytes, (byte)0, start, 0x20);
                if (end < 0)
                {
                    end = start + 0x20;
                }

                var name = Encoding.ASCII.GetString(bytes, start, end - start);
                if (!name.StartsWith("TROP", StringComparison.OrdinalIgnoreCase) ||
                    !name.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase) ||
                    name.Length != 11 ||
                    !int.TryParse(name.Substring(4, 3), out var id) ||
                    id < 0 || id >= TrophyCount)
                {
                    continue;
                }

                var imageOffset = ReadBigEndianUInt64(bytes, start + 0x20);
                var imageLength = ReadBigEndianUInt64(bytes, start + 0x28);
                if (imageLength < (ulong)PngMagic.Length || imageLength > 4UL * 1024 * 1024 ||
                    imageOffset > (ulong)bytes.Length ||
                    imageLength > (ulong)bytes.Length - imageOffset)
                {
                    continue;
                }

                var offset = (int)imageOffset;
                if (!PngMagic.SequenceEqual(bytes.Skip(offset).Take(PngMagic.Length)))
                {
                    continue;
                }

                var png = new byte[(int)imageLength];
                Buffer.BlockCopy(bytes, offset, png, 0, png.Length);
                result[id] = png;
            }

            return result;
        }

        private static uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static ulong ReadBigEndianUInt64(byte[] data, int offset)
        {
            return ((ulong)ReadBigEndianUInt32(data, offset) << 32) |
                   ReadBigEndianUInt32(data, offset + 4);
        }
    }

    internal static class SpartanPortTrophyMetadata
    {
        internal sealed class Text
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }

        private static readonly string[] Portuguese = {
            "Rei da colina|Desbloqueie todos os troféus.",
            "Abrindo as comportas|Derrote Poseidon.",
            "Reduzido a cinzas|Obtenha o Arco de Apolo.",
            "Sr. Mão|Descubra a sala secreta do Braço de Hades.",
            "Senhor do brilho|Derrote Hélios.",
            "Entrega de calçados|Derrote Hermes e obtenha suas botas.",
            "Mulherengo|Entretanha Afrodite.",
            "Dando uma mãozinha|Arranque a mão de Gaia.",
            "Abre-te, sésamo|Abra os Portões de Tisífone.",
            "Salve-me|Salve Pandora.",
            "Os três sábios|Resolva o enigma dos Três Juízes.",
            "Golpeador|Execute um combo de 1.000 golpes.",
            "Despedaçado|Despedace uma Sentinela do Olimpo.",
            "Belo bronzeado|Cegue 100 inimigos com a Cabeça de Hélios.",
            "Adestramento|Acerte 50 chutes em cães.",
            "Está quente aqui...|Queime 100 inimigos com o Arco de Apolo.",
            "Desalmado|Invoque todas as almas com as Garras de Hades.",
            "Inferno sangrento|Cubra Kratos com 500 baldes de sangue ao eliminar inimigos.",
            "Sem entranhas, sem glória|Estripar três Generais Centauros.",
            "Um passo de cada vez|Melhore uma arma para o próximo nível.",
            "Rivalidade entre irmãos|Derrote Hércules.",
            "Matador de Titã|Derrote Cronos.",
            "Retribuição|Derrote Zeus.",
            "Nas profundezas|Derrote Hades.",
            "Queimadura glacial|Obtenha a Tempestade de Gelo de Bóreas.",
            "Outro ponto de vista|Resolva o enigma dos Jardins de Hera.",
            "Com bons olhos|Colete todos os Olhos de Górgona.",
            "Depenando|Colete todas as Penas de Fênix.",
            "Chifre da plenitude|Colete todos os Chifres de Minotauro.",
            "Ao máximo!|Melhore completamente todas as armas.",
            "Vingança concluída|Termine o jogo.",
            "À altura do desafio|Conclua o Desafio do Olimpo.",
            "Desumano|Termine o Modo Titã.",
            "Não tem preço|Colete todos os Despojos dos Deuses.",
            "Labirintite|Conclua o Labirinto sem morrer nem falhar."
        };

        public static IReadOnlyList<Text> Load(string userDirectory)
        {
            var result = Portuguese.Select(item =>
            {
                var fields = item.Split(new[] { '|' }, 2);
                return new Text { Name = fields[0], Description = fields[1] };
            }).ToArray();

            if (string.IsNullOrWhiteSpace(userDirectory))
            {
                return result;
            }

            var paths = new[]
            {
                Path.Combine(userDirectory, "trophy_texts", "pt.txt"),
                Path.Combine(userDirectory, "trophy_texts.txt")
            };

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        var separator = line.IndexOf('=');
                        if (separator <= 0 ||
                            !int.TryParse(line.Substring(0, separator).Trim(), out var id) ||
                            id < 0 || id >= result.Length)
                        {
                            continue;
                        }

                        var content = line.Substring(separator + 1).Trim();
                        var delimiter = content.IndexOf(" | ", StringComparison.Ordinal);
                        if (delimiter <= 0)
                        {
                            continue;
                        }

                        var name = content.Substring(0, delimiter).Trim();
                        var description = content.Substring(delimiter + 3).Trim();
                        if (name.Length > 0 && description.Length > 0)
                        {
                            result[id] = new Text { Name = name, Description = description };
                        }
                    }

                    break;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return result;
        }

        public static string TrophyType(int id)
        {
            if (id == 0) return "platinum";
            if (id <= 18) return "bronze";
            if (id <= 29) return "silver";
            return "gold";
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlayniteAchievements.Providers.ReXGlue
{
    internal static class ReblueExtraAchievements
    {
        internal static bool IsReblue(string root)
        {
            return !string.IsNullOrWhiteSpace(root) &&
                File.Exists(Path.Combine(root, "profiles", "default", "reblue.toml"));
        }

        internal static void AppendTo(List<ReXGlueAchievement> catalog)
        {
            if (catalog == null)
            {
                return;
            }

            var rows = new[]
            {
                Entry(0x10001, "Where is the bathroom?", "Where does Zola sh**? (Dedicated to InfernoZotza)", 69),
                Entry(0x10002, "Le WOooAH", "Use a Corporeal attack. (Dedicated to wolfaeterni)", 50),
                Entry(0x10003, "Shuffle Baus", "Enter the shuffle dungeon. (Dedicated to baus.98)", 50),
                Entry(0x10004, "The Fungus Collector", "Collect 27 Nothings. (Dedicated to Fungus, the king of Nothings)", 50),
                Entry(0x10005, "The Cashman", "Obtain 666 gold. (Dedicated to griever666)", 50),
                Entry(0x10006, "Cloaca Maxima", "Slay a Poo Snake and purge the Great Sewer. (Dedicated to rcold)", 420),
                Entry(0x10007, "y u do dis", "Landshark... y u do dis (Dedicated to Graine25)", 50),
                Entry(0x10008, "Please Stop Shaving Me", "Where did the bald people use to be? (Dedicated to crack - by rcold)", 7)
            };

            foreach (var row in rows)
            {
                if (catalog.All(existing => existing.Id != row.Id))
                {
                    catalog.Add(row);
                }
            }
        }

        private static ReXGlueAchievement Entry(uint id, string name,
            string description, int points)
        {
            return new ReXGlueAchievement
            {
                Id = id,
                Name = name,
                Description = description,
                LockedDescription = "???",
                Gamerscore = points,
                Hidden = true
            };
        }
    }
}

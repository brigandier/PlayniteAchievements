using PlayniteAchievements.Providers.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteAchievements.Providers.Local
{
    public sealed class LocalAchievementSettings : ProviderSettingsBase
    {
        public override string ProviderKey => "LocalAchievements";
        public List<LocalAchievementConfiguration> Games { get; set; } = new List<LocalAchievementConfiguration>();
        public bool UseExophaseForRarity { get; set; }

        public LocalAchievementConfiguration Find(Guid gameId) =>
            Games?.FirstOrDefault(item => item.GameId == gameId);
    }

    public sealed class LocalAchievementConfiguration
    {
        public Guid GameId { get; set; }
        public string Format { get; set; }
        public string ProgressPath { get; set; }
        public string CatalogPath { get; set; }
        public string IconDirectory { get; set; }
        public string TitleId { get; set; }
        public bool IncludeReblueExtras { get; set; }
        public bool Monitor { get; set; } = true;
    }
}

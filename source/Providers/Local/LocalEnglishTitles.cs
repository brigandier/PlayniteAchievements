using PlayniteAchievements.Models.Achievements;
using PlayniteAchievements.Providers.SpartanPort;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteAchievements.Providers.Local
{
    internal static class LocalEnglishTitles
    {
        internal static IList<AchievementDetail> LoadSpartanPort(string userDirectory, GameAchievementData data)
        {
            var texts = SpartanPortTrophyMetadata.LoadEnglish(userDirectory);
            if (texts == null || data?.Achievements?.Count != texts.Count) return null;
            return texts.Select((text, id) => new AchievementDetail
            {
                ApiName = $"gow3_spartanport_{id:D3}",
                DisplayName = text.Name,
                Description = text.Description,
                TrophyType = SpartanPortTrophyMetadata.TrophyType(id)
            }).ToList();
        }
    }
}

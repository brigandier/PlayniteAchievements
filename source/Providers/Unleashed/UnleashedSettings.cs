using PlayniteAchievements.Providers.Settings;

namespace PlayniteAchievements.Providers.Unleashed
{
    public sealed class UnleashedSettings : ProviderSettingsBase
    {
        private bool _useExophaseForRarity;

        public override string ProviderKey => "UnleashedRecompiled";

        public bool UseExophaseForRarity
        {
            get => _useExophaseForRarity;
            set => SetValue(ref _useExophaseForRarity, value);
        }
    }
}

using PlayniteAchievements.Providers.Settings;

namespace PlayniteAchievements.Providers.ReXGlue
{
    public sealed class ReXGlueSettings : ProviderSettingsBase
    {
        private string _userDataRoot;
        private bool _useExophaseForRarity;

        public override string ProviderKey => "ReXGlue";

        public string UserDataRoot
        {
            get => _userDataRoot;
            set => SetValue(ref _userDataRoot, value);
        }

        public bool UseExophaseForRarity
        {
            get => _useExophaseForRarity;
            set => SetValue(ref _useExophaseForRarity, value);
        }
    }
}
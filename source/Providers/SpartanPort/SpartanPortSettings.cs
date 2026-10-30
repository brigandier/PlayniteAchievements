using PlayniteAchievements.Providers.Settings;

namespace PlayniteAchievements.Providers.SpartanPort
{
    public sealed class SpartanPortSettings : ProviderSettingsBase
    {
        private string _gameRoot;
        private string _userDirectory;
        private bool _useExophaseForRarity;

        public override string ProviderKey => "SpartanPort";

        public string GameRoot
        {
            get => _gameRoot;
            set => SetValue(ref _gameRoot, value);
        }

        public string UserDirectory
        {
            get => _userDirectory;
            set => SetValue(ref _userDirectory, value);
        }

        public bool UseExophaseForRarity
        {
            get => _useExophaseForRarity;
            set => SetValue(ref _useExophaseForRarity, value);
        }
    }
}
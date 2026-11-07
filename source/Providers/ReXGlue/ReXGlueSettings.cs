using PlayniteAchievements.Providers.Settings;

namespace PlayniteAchievements.Providers.ReXGlue
{
    public sealed class ReXGlueSettings : ProviderSettingsBase
    {
        private string _userDataRoot;

        public override string ProviderKey => "ReXGlue";

        public string UserDataRoot
        {
            get => _userDataRoot;
            set => SetValue(ref _userDataRoot, value);
        }

    }
}
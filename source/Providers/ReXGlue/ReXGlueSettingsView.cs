using Playnite.SDK;
using PlayniteAchievements.Providers.Settings;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteAchievements.Providers.ReXGlue
{
    public partial class ReXGlueSettingsView : ProviderSettingsViewBase, IAuthRefreshable
    {
        private readonly IPlayniteAPI _api;
        private ReXGlueSettings _reXGlueSettings;

        public static readonly DependencyProperty AuthStatusProperty =
            DependencyProperty.Register(nameof(AuthStatus), typeof(string),
                typeof(ReXGlueSettingsView), new PropertyMetadata(string.Empty));

        public string AuthStatus
        {
            get => (string)GetValue(AuthStatusProperty);
            set => SetValue(AuthStatusProperty, value);
        }

        public new ReXGlueSettings Settings => _reXGlueSettings;

        public ReXGlueSettingsView(IPlayniteAPI api)
        {
            _api = api;
            InitializeComponent();
            ConnectionLabel.Text = string.Format(
                ResourceProvider.GetString("LOCPlayAch_Settings_ProviderConnection"),
                ResourceProvider.GetString("LOCPlayAch_Provider_ReXGlue"));
        }

        public override void Initialize(IProviderSettings settings)
        {
            _reXGlueSettings = settings as ReXGlueSettings;
            base.Initialize(settings);
            UpdateStatus();
        }

        public Task RefreshAuthStatusAsync()
        {
            UpdateStatus();
            return Task.CompletedTask;
        }

        private void Path_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStatus();
        }

        private void BrowseUserRoot_Click(object sender, RoutedEventArgs e)
        {
            var folder = _api?.Dialogs?.SelectFolder();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                UserRootBox.Text = folder;
            }
        }

        private void UpdateStatus()
        {
            if (_reXGlueSettings == null)
            {
                return;
            }

            var root = UserRootBox?.Text?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(root))
            {
                AuthStatus = ResourceProvider.GetString(
                    "LOCPlayAch_Settings_ReXGlue_AutoDetect");
                return;
            }

            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(root);
                AuthStatus = Directory.Exists(expanded)
                    ? ResourceProvider.GetString("LOCPlayAch_Status_Succeeded")
                    : ResourceProvider.GetString("LOCPlayAch_InvalidPath");
            }
            catch (ArgumentException)
            {
                AuthStatus = ResourceProvider.GetString("LOCPlayAch_InvalidPath");
            }
        }
    }
}

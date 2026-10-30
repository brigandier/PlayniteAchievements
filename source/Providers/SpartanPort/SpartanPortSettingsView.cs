using Playnite.SDK;
using PlayniteAchievements.Providers.Settings;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteAchievements.Providers.SpartanPort
{
    public partial class SpartanPortSettingsView : ProviderSettingsViewBase, IAuthRefreshable
    {
        private readonly IPlayniteAPI _api;
        private SpartanPortSettings _spartanPortSettings;

        public static readonly DependencyProperty IsAuthenticatedProperty =
            DependencyProperty.Register(nameof(IsAuthenticated), typeof(bool), typeof(SpartanPortSettingsView), new PropertyMetadata(false));

        public bool IsAuthenticated
        {
            get => (bool)GetValue(IsAuthenticatedProperty);
            set => SetValue(IsAuthenticatedProperty, value);
        }

        public static readonly DependencyProperty AuthStatusProperty =
            DependencyProperty.Register(nameof(AuthStatus), typeof(string), typeof(SpartanPortSettingsView), new PropertyMetadata(string.Empty));

        public string AuthStatus
        {
            get => (string)GetValue(AuthStatusProperty);
            set => SetValue(AuthStatusProperty, value);
        }

        public new SpartanPortSettings Settings => _spartanPortSettings;

        public SpartanPortSettingsView(IPlayniteAPI api)
        {
            _api = api;
            InitializeComponent();
            ConnectionLabel.Text = string.Format(
                ResourceProvider.GetString("LOCPlayAch_Settings_ProviderConnection"),
                ResourceProvider.GetString("LOCPlayAch_Provider_SpartanPort"));
        }

        public override void Initialize(IProviderSettings settings)
        {
            _spartanPortSettings = settings as SpartanPortSettings;
            base.Initialize(settings);
            UpdatePathStatus();
        }

        public Task RefreshAuthStatusAsync()
        {
            UpdatePathStatus();
            return Task.CompletedTask;
        }

        private void Path_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePathStatus();
        }

        private void BrowseRoot_Click(object sender, RoutedEventArgs e)
        {
            var path = _api?.Dialogs?.SelectFolder();
            if (!string.IsNullOrWhiteSpace(path))
            {
                GameRootBox.Text = path;
            }
        }

        private void BrowseSaves_Click(object sender, RoutedEventArgs e)
        {
            var path = _api?.Dialogs?.SelectFolder();
            if (!string.IsNullOrWhiteSpace(path))
            {
                SaveFolderBox.Text = path;
            }
        }

        private void UpdatePathStatus()
        {
            if (_spartanPortSettings == null)
            {
                return;
            }

            var root = ExpandPath(GameRootBox?.Text);
            var saves = ExpandPath(SaveFolderBox?.Text);

            if (!string.IsNullOrWhiteSpace(root) &&
                (!Directory.Exists(root) || !SpartanPortPaths.HasPortMarker(root)))
            {
                IsAuthenticated = false;
                AuthStatus = ResourceProvider.GetString("LOCPlayAch_InvalidPath");
                return;
            }

            if (!string.IsNullOrWhiteSpace(saves) && !Directory.Exists(saves))
            {
                IsAuthenticated = false;
                AuthStatus = ResourceProvider.GetString("LOCPlayAch_InvalidPath");
                return;
            }

            IsAuthenticated = true;
            AuthStatus = string.IsNullOrWhiteSpace(root)
                ? ResourceProvider.GetString("LOCPlayAch_Settings_SpartanPort_AutoDetect")
                : ResourceProvider.GetString("LOCPlayAch_Status_Succeeded");
        }

        private static string ExpandPath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        }
    }
}

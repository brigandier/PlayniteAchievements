using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteAchievements.Providers.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteAchievements.Providers.Local
{
    public partial class LocalAchievementSettingsView : ProviderSettingsViewBase
    {
        private readonly IPlayniteAPI _api;
        private readonly string _dataPath;
        private readonly ILogger _logger;
        private string _validatedConfiguration;
        private bool _loading;
        public new LocalAchievementSettings Settings { get; private set; }

        public LocalAchievementSettingsView(IPlayniteAPI api, string dataPath, ILogger logger)
        {
            _api = api;
            _dataPath = dataPath;
            _logger = logger;
            InitializeComponent();
        }

        public override void Initialize(IProviderSettings settings)
        {
            Settings = (LocalAchievementSettings)settings;
            base.Initialize(settings);
            GameBox.ItemsSource = _api.Database.Games.OrderBy(game => game.Name).ToList();
            FormatBox.SelectedIndex = 0;
        }

        private LocalAchievementConfiguration ReadForm() => new LocalAchievementConfiguration
        {
            GameId = (GameBox.SelectedItem as Game)?.Id ?? Guid.Empty,
            Format = FormatBox.SelectedValue as string,
            ProgressPath = Expand(ProgressBox.Text),
            CatalogPath = Expand(CatalogBox.Text),
            IconDirectory = Expand(IconsBox.Text),
            TitleId = TitleBox.Text.Trim(),
            IncludeReblueExtras = ExtrasBox.IsChecked == true,
            Monitor = MonitorBox.IsChecked == true
        };

        private static string Expand(string path) => string.IsNullOrWhiteSpace(path) ? null :
            Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

        private void ShowConfiguration(LocalAchievementConfiguration configuration)
        {
            _loading = true;
            FormatBox.SelectedValue = configuration?.Format ?? "";
            ProgressBox.Text = configuration?.ProgressPath ?? "";
            CatalogBox.Text = configuration?.CatalogPath ?? "";
            IconsBox.Text = configuration?.IconDirectory ?? "";
            TitleBox.Text = configuration?.TitleId ?? "";
            ExtrasBox.IsChecked = configuration?.IncludeReblueExtras == true;
            MonitorBox.IsChecked = configuration?.Monitor ?? true;
            _loading = false;
            Input_Changed(this, new RoutedEventArgs());
        }

        private void Game_Changed(object sender, SelectionChangedEventArgs e)
        {
            var game = GameBox.SelectedItem as Game;
            ShowConfiguration(game == null ? null : Settings?.Find(game.Id));
        }

        private void Input_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading || SaveButton == null) return;
            SaveButton.IsEnabled = false;
            _validatedConfiguration = null;
            PreviewGrid.ItemsSource = null;
            StatusText.Text = "";
            var reader = LocalProgressReaders.All.FirstOrDefault(item => item.Format == (FormatBox.SelectedValue as string));
            CapabilitiesText.Text = reader == null ? "" : reader.FileFormat + " — " + reader.Validation + ". " + reader.Limitations;
        }

        private void BrowseProgress_Click(object sender, RoutedEventArgs e)
        {
            var path = _api.Dialogs.SelectFile("Arquivos de progresso|*.toml;ACH-DATA;trophies.dat|Todos os arquivos|*.*");
            if (!string.IsNullOrWhiteSpace(path)) ProgressBox.Text = path;
        }

        private void BrowseCatalog_Click(object sender, RoutedEventArgs e)
        {
            var path = _api.Dialogs.SelectFile("Catálogos e recursos originais|*.toml;*.tsv;*.xex|Todos os arquivos|*.*");
            if (!string.IsNullOrWhiteSpace(path)) CatalogBox.Text = path;
        }

        private void BrowseIcons_Click(object sender, RoutedEventArgs e)
        {
            var path = _api.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(path)) IconsBox.Text = path;
        }

        private void Detect_Click(object sender, RoutedEventArgs e)
        {
            if (!(GameBox.SelectedItem is Game game)) return;
            try
            {
                var configuration = LocalAchievementLoader.Detect(game, _api);
                if (configuration == null) StatusText.Text = ResourceProvider.GetString("LOCPlayAch_Local_NotDetected");
                else ShowConfiguration(configuration);
            }
            catch (Exception ex) when (IsFileError(ex)) { StatusText.Text = ex.Message; }
        }

        private async void Validate_Click(object sender, RoutedEventArgs e)
        {
            if (!(GameBox.SelectedItem is Game game)) return;
            var configuration = ReadForm();
            var signature = JsonConvert.SerializeObject(configuration);
            ValidateButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            _validatedConfiguration = null;
            try
            {
                var data = await Task.Run(() => LocalAchievementLoader.Load(game, configuration, _dataPath, _logger));
                if (signature != JsonConvert.SerializeObject(ReadForm())) return;
                PreviewGrid.ItemsSource = data.Achievements;
                _validatedConfiguration = signature;
                SaveButton.IsEnabled = true;
                var reader = LocalProgressReaders.Resolve(configuration.Format, configuration.ProgressPath);
                CapabilitiesText.Text = reader.FileFormat + " — " + reader.Validation + ". " + reader.Limitations;
                StatusText.Text = string.Format(ResourceProvider.GetString("LOCPlayAch_Local_Validated"),
                    data.Achievements.Count, data.Achievements.Count(item => item.Unlocked));
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                if (signature == JsonConvert.SerializeObject(ReadForm()))
                {
                    PreviewGrid.ItemsSource = null;
                    StatusText.Text = ex.Message;
                }
            }
            finally { ValidateButton.IsEnabled = true; }
        }

        private static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
            ex is ArgumentException || ex is NotSupportedException;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var configuration = ReadForm();
            if (configuration.GameId == Guid.Empty || JsonConvert.SerializeObject(configuration) != _validatedConfiguration) return;
            configuration.Format = LocalProgressReaders.Resolve(configuration.Format, configuration.ProgressPath)?.Format;
            Settings.Games = (Settings.Games ?? new List<LocalAchievementConfiguration>())
                .Where(item => item.GameId != configuration.GameId).Concat(new[] { configuration }).ToList();
            StatusText.Text = ResourceProvider.GetString("LOCPlayAch_Local_Saved");
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (!(GameBox.SelectedItem is Game game)) return;
            Settings.Games = Settings.Games?.Where(item => item.GameId != game.Id).ToList();
            ShowConfiguration(null);
            StatusText.Text = ResourceProvider.GetString("LOCPlayAch_Local_Removed");
        }
    }
}

using Playnite.SDK;
using PlayniteAchievements.Providers.Settings;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteAchievements.Providers.SpartanPort
{
    internal sealed class SpartanPortSettingsView : ProviderSettingsViewBase
    {
        private readonly IPlayniteAPI _api;
        private readonly TextBox _root = new TextBox { MinWidth = 340, Margin = new Thickness(0, 4, 8, 12) };
        private readonly TextBox _user = new TextBox { MinWidth = 340, Margin = new Thickness(0, 4, 8, 12) };
        private SpartanPortSettings _data;

        public SpartanPortSettingsView(IPlayniteAPI api)
        {
            _api = api;
            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock
            {
                Text = "God of War III Remastered (SpartanPort)",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Pasta do SpartanPort (deixe em branco para usar a instalação registrada no Playnite):",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(MakePathRow(_root, () => _data.GameRoot = _root.Text));
            panel.Children.Add(new TextBlock
            {
                Text = "Pasta dos salvamentos que contém trophies.dat (opcional):",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(MakePathRow(_user, () => _data.UserDirectory = _user.Text));
            panel.Children.Add(new TextBlock
            {
                Text = "As conquistas são identificadas a partir dos arquivos locais do SpartanPort.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            Content = new ScrollViewer { Content = panel };
            _root.TextChanged += (sender, args) =>
            {
                if (_data != null) _data.GameRoot = _root.Text;
            };
            _user.TextChanged += (sender, args) =>
            {
                if (_data != null) _data.UserDirectory = _user.Text;
            };
        }

        public override void Initialize(IProviderSettings settings)
        {
            _data = null;
            base.Initialize(settings);
            _root.Text = (settings as SpartanPortSettings)?.GameRoot ?? string.Empty;
            _user.Text = (settings as SpartanPortSettings)?.UserDirectory ?? string.Empty;
            _data = settings as SpartanPortSettings;
        }

        private StackPanel MakePathRow(TextBox box, System.Action updated)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(box);
            var browse = new Button
            {
                Content = "Procurar...",
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 90,
                Margin = new Thickness(0, 4, 0, 0)
            };
            browse.Click += (sender, args) =>
            {
                var folder = _api?.Dialogs?.SelectFolder();
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    box.Text = folder;
                    updated();
                }
            };
            line.Children.Add(browse);
            return line;
        }
    }
}
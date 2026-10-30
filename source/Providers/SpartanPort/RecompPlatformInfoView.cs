using Playnite.SDK;
using PlayniteAchievements.Providers.Settings;
using System.Windows;
using System.Windows.Controls;

namespace PlayniteAchievements.Providers.SpartanPort
{
    internal sealed class RecompPlatformInfoView : ProviderSettingsViewBase
    {
        public RecompPlatformInfoView(string platformResourceKey)
        {
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = ResourceProvider.GetString(platformResourceKey),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 16)
            });
            panel.Children.Add(new TextBlock
            {
                Text = ResourceProvider.GetString("LOCPlayAch_Settings_Recomp_NoReader"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            panel.Children.Add(new TextBlock
            {
                Text = ResourceProvider.GetString("LOCPlayAch_Settings_Recomp_Info"),
                TextWrapping = TextWrapping.Wrap
            });
            Content = new ScrollViewer { Content = panel };
        }
    }
}

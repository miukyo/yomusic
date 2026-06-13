using System;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Services;

namespace yomusic.Views
{
    public sealed partial class Settings : Page
    {
        public Settings()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = SettingsService.Instance;

            AudioQualityCombo.Items.Add(new ComboBoxItem { Content = "Low", Tag = 0 });
            AudioQualityCombo.Items.Add(new ComboBoxItem { Content = "Normal", Tag = 1 });
            AudioQualityCombo.Items.Add(new ComboBoxItem { Content = "High", Tag = 2 });
            AudioQualityCombo.SelectedIndex = s.AudioQuality;
            AudioQualityCombo.SelectionChanged += (_, _) =>
            {
                if (AudioQualityCombo.SelectedItem is ComboBoxItem item)
                    s.AudioQuality = (int)item.Tag;
            };

            ThemeCombo.Items.Add(new ComboBoxItem { Content = "System", Tag = ElementTheme.Default });
            ThemeCombo.Items.Add(new ComboBoxItem { Content = "Light", Tag = ElementTheme.Light });
            ThemeCombo.Items.Add(new ComboBoxItem { Content = "Dark", Tag = ElementTheme.Dark });
            for (int i = 0; i < ThemeCombo.Items.Count; i++)
            {
                if ((ElementTheme)((ComboBoxItem)ThemeCombo.Items[i]).Tag == s.Theme)
                {
                    ThemeCombo.SelectedIndex = i;
                    break;
                }
            }
            ThemeCombo.SelectionChanged += (_, _) =>
            {
                if (ThemeCombo.SelectedItem is ComboBoxItem item)
                    ApplyTheme((ElementTheme)item.Tag);
            };

            AnimToggle.IsOn = s.EnableAnimations;
            AnimToggle.Toggled += (_, _) => s.EnableAnimations = AnimToggle.IsOn;

            LoadAccount();
            LoadAbout();
            LoadCache();

            SignOutBtn.Click += async (_, _) =>
            {
                await AccountService.Instance.LogoutAsync();
                LoadAccount();
            };

            ClearCacheBtn.Click += async (_, _) =>
            {
                ClearCacheBtn.IsEnabled = false;
                try
                {
                    await CacheService.ClearAllAsync();
                    CacheSizeText.Text = "Cleared";
                }
                catch
                {
                    CacheSizeText.Text = "Failed";
                }
                finally
                {
                    ClearCacheBtn.IsEnabled = true;
                    LoadCache();
                }
            };
        }

        private static void ApplyTheme(ElementTheme theme)
        {
            SettingsService.Instance.Theme = theme;
            if (Window.Current.Content is FrameworkElement root)
                root.RequestedTheme = theme;
        }

        private void LoadAccount()
        {
            AccountInfoPanel.Children.Clear();
            var a = AccountService.Instance;
            if (a.IsLoggedIn && a.AccountInfo != null)
            {
                var info = a.AccountInfo;
                //AccountInfoPanel.Children.Add(new TextBlock
                //{
                //    Text = info.AccountName ?? "Unknown",
                //    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                //    VerticalAlignment = VerticalAlignment.Center
                //});
                //if (!string.IsNullOrEmpty(info.ChannelHandle))
                //    AccountInfoPanel.Children.Add(new TextBlock
                //    {
                //        Text = info.ChannelHandle,
                //        FontSize = 12,
                //        Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"]
                //    });
                if (!string.IsNullOrEmpty(info.AccountPhotoUrl))
                    AccountInfoPanel.Children.Add(new Border
                    {
                        Width = 32,
                        Height = 32,
                        CornerRadius = new CornerRadius(16),
                        Child = new Image
                        {
                            Width = 32,
                            Height = 32,
                            Stretch = Stretch.UniformToFill,
                            Source = new BitmapImage(new Uri(info.AccountPhotoUrl))
                        }
                    });

                SignOutBtn.Visibility = Visibility.Visible;
            }
            else
            {
                AccountInfoPanel.Children.Add(new TextBlock
                {
                    Text = "Not signed in",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                });
                SignOutBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadAbout()
        {
            var version = Package.Current.Id.Version;
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        private async void LoadCache()
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var size = await GetFolderSizeAsync(folder);
                CacheSizeText.Text = FormatSize(size);
            }
            catch
            {
                CacheSizeText.Text = "Unknown";
            }
        }

        private static async System.Threading.Tasks.Task<long> GetFolderSizeAsync(StorageFolder folder)
        {
            long total = 0;
            foreach (var file in await folder.GetFilesAsync())
                total += (long)(await file.GetBasicPropertiesAsync()).Size;
            foreach (var sub in await folder.GetFoldersAsync())
                total += await GetFolderSizeAsync(sub);
            return total;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}

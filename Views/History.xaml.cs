using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Models;
using yomusic.Services;
using ytmusic_net;

namespace yomusic.Views
{
    public sealed partial class History : Page
    {
        public History()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            try
            {
                var client = await YTMusicClient.Client;
                var songs = await client.GetHistoryAsync();

                if (songs.Count > 0)
                {
                    var fg = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
                    var fg2 = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];

                    foreach (var song in songs.Take(50))
                    {
                        var item = new Grid
                        {
                            Padding = new Thickness(8),
                            ColumnSpacing = 12
                        };
                        item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        var thumbnail = new Border
                        {
                            Width = 48,
                            Height = 48,
                            CornerRadius = new CornerRadius(4),
                            Child = new Image
                            {
                                Width = 48,
                                Height = 48,
                                Stretch = Stretch.UniformToFill,
                                Source = ThumbnailHelper.GetThumbnail(song.Thumbnails)
                            }
                        };
                        item.Children.Add(thumbnail);

                        var textStack = new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        textStack.Children.Add(new TextBlock
                        {
                            Text = song.Title,
                            FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            TextWrapping = TextWrapping.NoWrap,
                            Foreground = fg
                        });
                        textStack.Children.Add(new TextBlock
                        {
                            Text = song.Artists?.FirstOrDefault()?.Name ?? "",
                            FontSize = 12,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            TextWrapping = TextWrapping.NoWrap,
                            Foreground = fg2
                        });
                        Grid.SetColumn(textStack, 1);
                        item.Children.Add(textStack);

                        var videoId = song.VideoId;
                        item.Tapped += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(videoId))
                                Frame.Navigate(typeof(TrackView), videoId);
                        };

                        HistoryStack.Children.Add(item);
                    }
                }
                else
                {
                    EmptyText.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                EmptyText.Text = "Something went wrong";
                EmptyText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

    }
}

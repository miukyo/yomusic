using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Controls;
using yomusic.Models;
using yomusic.Services;
using ytmusic_net;

namespace yomusic.Views
{
    public sealed partial class Playlist : Page
    {
        private string? _browseId;
        private string? _contentType;

        public Playlist()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            (string id, string type) = e.Parameter switch
            {
                string s => (s, "PLAYLIST"),
                (string t, string i) => (i, t),
                _ => (null, null)
            };
            if (id != null && id != _browseId)
            {
                _browseId = id;
                _contentType = type;
                TrackTitle.Visibility = Visibility.Collapsed;
                EmptyText.Visibility = Visibility.Collapsed;
                TrackList.Children.Clear();
                SectionList.Children.Clear();
                await LoadContentAsync();
            }
        }

        private async Task LoadContentAsync()
        {
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            try
            {
                var client = await YTMusicClient.Client;

                if (_contentType == "ALBUM")
                    await LoadAlbumAsync(client);
                else
                    await LoadPlaylistAsync(client);
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

        private async Task LoadAlbumAsync(YTMusic client)
        {
            var album = await client.GetAlbumAsync(_browseId!);

            PlaylistTitle.Text = album.Name;
            ArtistName.Text = album.Artist?.Name ?? "";
            if (!string.IsNullOrEmpty(album.Year?.ToString()))
                DescriptionText.Text = album.Year.ToString();
            if (album.Thumbnails?.Count > 0)
                PlaylistThumbnail.Source = new BitmapImage(new Uri(ThumbnailHelper.GetBestThumbnailUrl(album.Thumbnails)));

            if (album.Songs?.Count > 0)
            {
                TrackTitle.Visibility = Visibility.Visible;
                var fg = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
                var fg2 = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];

                foreach (var song in album.Songs)
                {
                    var item = new Grid { Padding = new Thickness(8), ColumnSpacing = 12 };
                    item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    item.Children.Add(new Border
                    {
                        Width = 48,
                        Height = 48,
                        CornerRadius = new CornerRadius(4),
                        Child = new Image
                        {
                            Width = 48,
                            Height = 48,
                            Stretch = Stretch.UniformToFill,
                            Source = GetThumbnail(song.Thumbnails)
                        }
                    });

                    var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    textStack.Children.Add(new TextBlock
                    {
                        Text = song.Name,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = fg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap
                    });
                    textStack.Children.Add(new TextBlock
                    {
                        Text = song.Artist?.Name ?? "",
                        FontSize = 12,
                        Foreground = fg2,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap
                    });
                    Grid.SetColumn(textStack, 1);
                    item.Children.Add(textStack);

                    var videoId = song.VideoId;
                    item.Tapped += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(videoId))
                            Frame.Navigate(typeof(TrackView), videoId);
                    };

                    TrackList.Children.Add(item);
                }
            }
            else
            {
                EmptyText.Text = "No tracks";
                EmptyText.Visibility = Visibility.Visible;
            }
        }

        private async Task LoadPlaylistAsync(YTMusic client)
        {
            var playlist = await client.GetPlaylistAsync(_browseId!);

            PlaylistTitle.Text = playlist.Name;
            if (!string.IsNullOrEmpty(playlist.Artist?.Name))
                ArtistName.Text = playlist.Artist.Name;
            if (playlist.VideoCount > 0)
                DescriptionText.Text = $"{playlist.VideoCount} tracks";
            if (playlist.Thumbnails?.Count > 0)
                PlaylistThumbnail.Source = new BitmapImage(new Uri(ThumbnailHelper.GetBestThumbnailUrl(playlist.Thumbnails)));

            var tracks = await client.GetPlaylistVideosAsync(_browseId!);

            if (tracks.Count > 0)
            {
                TrackTitle.Visibility = Visibility.Visible;
                var fg = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
                var fg2 = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];

                foreach (var track in tracks)
                {
                    var item = new Grid { Padding = new Thickness(8), ColumnSpacing = 12 };
                    item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    item.Children.Add(new Border
                    {
                        Width = 48,
                        Height = 48,
                        CornerRadius = new CornerRadius(4),
                        Child = new Image
                        {
                            Width = 48,
                            Height = 48,
                            Stretch = Stretch.UniformToFill,
                            Source = GetThumbnail(track.Thumbnails)
                        }
                    });

                    var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    textStack.Children.Add(new TextBlock
                    {
                        Text = track.Name,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = fg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap
                    });
                    textStack.Children.Add(new TextBlock
                    {
                        Text = track.Artist?.Name ?? "",
                        FontSize = 12,
                        Foreground = fg2,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap
                    });
                    Grid.SetColumn(textStack, 1);
                    item.Children.Add(textStack);

                    var videoId = track.VideoId;
                    item.Tapped += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(videoId))
                            Frame.Navigate(typeof(TrackView), videoId);
                    };

                    TrackList.Children.Add(item);
                }
            }
            else
            {
                EmptyText.Text = "No tracks";
                EmptyText.Visibility = Visibility.Visible;
            }
        }

        private static BitmapImage? GetThumbnail(List<Thumbnail>? thumbnails) => ThumbnailHelper.GetThumbnail(thumbnails);
    }
}

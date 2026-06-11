using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Controls;
using yomusic.Models;
using yomusic.Services;
using ytmusic_net;

namespace yomusic.Views
{
    public sealed partial class Library : Page
    {
        private readonly Dictionary<string, List<ContentItem>?> _cache = new()
        {
            ["SONG"] = null,
            ["ALBUM"] = null,
            ["ARTIST"] = null,
            ["PLAYLIST"] = null,
        };

        public Library()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ShowSelectedFilterAsync();
        }

        private async void OnFilterChecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.IsChecked == true)
            {
                foreach (var child in FilterBar.Children)
                {
                    if (child is ToggleButton other && other != btn)
                        other.IsChecked = false;
                }
                await ShowSelectedFilterAsync();
            }
        }

        private async Task ShowSelectedFilterAsync()
        {
            var activeTag = FilterBar.Children
                .OfType<ToggleButton>()
                .FirstOrDefault(b => b.IsChecked == true)
                ?.Tag?.ToString();

            if (activeTag == null) return;

            SectionList.Children.Clear();
            EmptyText.Visibility = Visibility.Collapsed;

            if (!_cache.TryGetValue(activeTag, out var contents) || contents == null)
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;

                try
                {
                    contents = await FetchFilteredDataAsync(activeTag);
                    _cache[activeTag] = contents;
                }
                catch
                {
                    EmptyText.Text = "Something went wrong";
                    EmptyText.Visibility = Visibility.Visible;
                    return;
                }
                finally
                {
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                }
            }

            if (contents.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            var title = activeTag switch
            {
                "SONG" => "Songs",
                "ALBUM" => "Albums",
                "ARTIST" => "Artists",
                "PLAYLIST" => "Playlists",
                _ => ""
            };

            SectionList.Children.Add(new SectionGrid
            {
                DataContext = new SectionModel
                {
                    Title = title,
                    Contents = contents
                }
            });
        }

        private async Task<List<ContentItem>> FetchFilteredDataAsync(string tag)
        {
            var client = await YTMusicClient.Client;

            return tag switch
            {
                "SONG" => (await client.GetLibrarySongsAsync(limit: 50)).Select(song => new ContentItem
                {
                    Type = "SONG",
                    Name = song.Name,
                    Thumbnail = GetThumbnail(song.Thumbnails),
                    ArtistName = song.Artist?.Name ?? "",
                    VideoId = song.VideoId,
                    Duration = DurationHelper.FormatDuration(song.Duration),
                }).ToList(),

                "ALBUM" => (await client.GetLibraryAlbumsAsync(limit: 50)).Select(album => new ContentItem
                {
                    Type = "ALBUM",
                    Name = album.Name,
                    Thumbnail = GetThumbnail(album.Thumbnails),
                    ArtistName = album.Artist?.Name ?? "",
                    BrowseId = album.AlbumId,
                }).ToList(),

                "ARTIST" => (await client.GetLibraryArtistsAsync(limit: 50)).Select(artist => new ContentItem
                {
                    Type = "ARTIST",
                    Name = artist.Name,
                    Thumbnail = GetThumbnail(artist.Thumbnails),
                    BrowseId = artist.ArtistId,
                }).ToList(),

                "PLAYLIST" => (await client.GetLibraryPlaylistsAsync(limit: 50)).Select(playlist => new ContentItem
                {
                    Type = "PLAYLIST",
                    Name = playlist.Name,
                    Thumbnail = GetThumbnail(playlist.Thumbnails),
                    ArtistName = playlist.Artist?.Name ?? "",
                    BrowseId = playlist.PlaylistId,
                }).ToList(),

                _ => []
            };
        }

        private static BitmapImage? GetThumbnail(List<Thumbnail>? thumbnails) => ThumbnailHelper.GetThumbnail(thumbnails);
    }
}

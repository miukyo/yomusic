using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Controls;
using yomusic.Helpers;
using yomusic.Models;
using yomusic.Services;
using ytmusic_net;

namespace yomusic.Views
{
    public sealed partial class Search : Page
    {
        private CancellationTokenSource? _cts;
        private string _lastQuery = string.Empty;

        public Search()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            AnimationHelper.SetEntranceTransition(RootPanel);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string query && !string.IsNullOrWhiteSpace(query))
            {
                QueryText.Text = "Search result for \"" + query + "\""; ;
                _ = PerformSearchAsync(query);
            }
        }

        private async Task PerformSearchAsync(string query)
        {
            if (query == _lastQuery) return;
            _lastQuery = query;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            EmptyText.Visibility = Visibility.Collapsed;
            SectionList.Children.Clear();

            try
            {
                var client = await YTMusicClient.Client;

                var songsTask = client.SearchSongsAsync(query);
                var videosTask = client.SearchVideosAsync(query);
                var artistsTask = client.SearchArtistsAsync(query);
                var albumsTask = client.SearchAlbumsAsync(query);
                var playlistsTask = client.SearchPlaylistsAsync(query);

                await Task.WhenAll(songsTask, videosTask, artistsTask, albumsTask, playlistsTask);

                if (token.IsCancellationRequested) return;

                QueryText.Text = "Search result for \"" + query + "\"";

                var any = false;

                void AddSection<T>(List<T> items, string title, Func<T, ContentItem> map)
                {
                    if (items.Count == 0) return;
                    any = true;
                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = title,
                            Contents = items.Select(map).ToList()
                        }
                    });
                }

                AddSection(songsTask.Result, "Songs", (SongDetailed s) => new ContentItem
                {
                    Type = "SONG", Name = s.Name, Thumbnail = ThumbnailHelper.GetThumbnail(s.Thumbnails),
                    ArtistName = s.Artist?.Name ?? "", VideoId = s.VideoId,
                    Duration = DurationHelper.FormatDuration(s.Duration)
                });

                AddSection(videosTask.Result, "Videos", (VideoDetailed v) => new ContentItem
                {
                    Type = "VIDEO", Name = v.Name, Thumbnail = ThumbnailHelper.GetThumbnail(v.Thumbnails),
                    ArtistName = v.Artist?.Name ?? "", VideoId = v.VideoId,
                    Duration = DurationHelper.FormatDuration(v.Duration)
                });

                AddSection(artistsTask.Result, "Artists", (ArtistDetailed a) => new ContentItem
                {
                    Type = "ARTIST", Name = a.Name, Thumbnail = ThumbnailHelper.GetThumbnail(a.Thumbnails),
                    BrowseId = a.ArtistId
                });

                AddSection(albumsTask.Result, "Albums", (AlbumDetailed a) => new ContentItem
                {
                    Type = "ALBUM", Name = a.Name, Thumbnail = ThumbnailHelper.GetThumbnail(a.Thumbnails),
                    ArtistName = a.Artist?.Name ?? "", BrowseId = a.AlbumId
                });

                AddSection(playlistsTask.Result, "Playlists", (PlaylistDetailed p) => new ContentItem
                {
                    Type = "PLAYLIST", Name = p.Name, Thumbnail = ThumbnailHelper.GetThumbnail(p.Thumbnails),
                    ArtistName = p.Artist?.Name ?? "", BrowseId = p.PlaylistId
                });

                if (!any)
                    EmptyText.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) { }
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

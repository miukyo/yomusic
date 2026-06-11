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
                var results = await client.SearchAsync(query, null, null, 20);

                if (token.IsCancellationRequested) return;

                QueryText.Text = "Search result for \"" + query + "\"";

                if (results.Count > 0)
                {
                    var groups = results.GroupBy(r => r.Category ?? r.Type);
                    foreach (var group in groups)
                    {
                        var contents = new List<ContentItem>();
                        foreach (var item in group)
                        {
                            var ci = new ContentItem
                            {
                                Name = item.Name,
                                Thumbnail = ThumbnailHelper.GetThumbnail(item.Thumbnails),
                            };
                            switch (item)
                            {
                                case SongDetailed s:
                                    ci.Type = "SONG";
                                    ci.ArtistName = s.Artist?.Name ?? "";
                                    ci.VideoId = s.VideoId;
                                    ci.Duration = DurationHelper.FormatDuration(s.Duration);
                                    break;
                                case VideoDetailed v:
                                    ci.Type = "VIDEO";
                                    ci.ArtistName = v.Artist?.Name ?? "";
                                    ci.VideoId = v.VideoId;
                                    ci.Duration = DurationHelper.FormatDuration(v.Duration);
                                    break;
                                case AlbumDetailed a:
                                    ci.Type = "ALBUM";
                                    ci.ArtistName = a.Artist?.Name ?? "";
                                    ci.BrowseId = a.AlbumId;
                                    break;
                                case PlaylistDetailed p:
                                    ci.Type = "PLAYLIST";
                                    ci.ArtistName = p.Artist?.Name ?? "";
                                    ci.BrowseId = p.PlaylistId;
                                    break;
                                case ArtistDetailed ar:
                                    ci.Type = "ARTIST";
                                    ci.ArtistName = ar.Name;
                                    ci.BrowseId = ar.ArtistId;
                                    break;
                            }
                            contents.Add(ci);
                        }

                        SectionList.Children.Add(new SectionRow
                        {
                            Margin = new Thickness(0, 28, 0, 0),
                            DataContext = new SectionModel
                            {
                                Title = group.Key ?? group.First().Type.ToLowerInvariant(),
                                Contents = contents
                            }
                        });
                    }
                }
                else
                {
                    EmptyText.Visibility = Visibility.Visible;
                }
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

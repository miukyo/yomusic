using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed partial class Explore : Page
    {
        private bool _fetched;

        public Explore()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            _ = FetchExploreAsync();
        }

        private async Task FetchExploreAsync()
        {
            if (_fetched) return;
            LoadingRing.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            try
            {
                var client = await YTMusicClient.Client;
                var data = await client.GetExploreAsync();

                if (data.NewReleases.Count > 0)
                {
                    var contents = data.NewReleases.Select(a => new ContentItem
                    {
                        Type = a.Type,
                        Name = a.Name,
                        Thumbnail = GetThumbnail(a.Thumbnails),
                        ArtistName = a.Artist?.Name ?? "",
                        BrowseId = a.AlbumId
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "New Releases",
                            Contents = contents
                        }
                    });
                }

                if (data.Trending.Count > 0)
                {
                    var contents = data.Trending.Select(MapTrendingItem).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Trending",
                            Contents = contents
                        }
                    });
                }

                if (data.NewMusicVideos.Count > 0)
                {
                    var contents = data.NewMusicVideos.Select(v => new ContentItem
                    {
                        Type = "VIDEO",
                        Name = v.Name,
                        Thumbnail = GetThumbnail(v.Thumbnails),
                        ArtistName = v.Artist?.Name ?? "",
                        VideoId = v.VideoId,
                        Duration = DurationHelper.FormatDuration(v.Duration)
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "New Music Videos",
                            Contents = contents
                        }
                    });
                }

                _fetched = true;
            }
            catch
            {
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private static ContentItem MapTrendingItem(SearchResult item)
        {
            var ci = new ContentItem
            {
                Name = item.Name,
                Thumbnail = GetThumbnail(item.Thumbnails),
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
                default:
                    ci.Type = "SONG";
                    ci.VideoId = GetItemVideoId(item);
                    break;
            }
            return ci;
        }

        private static string? GetItemVideoId(SearchResult item)
        {
            return item switch
            {
                SongDetailed s => s.VideoId,
                VideoDetailed v => v.VideoId,
                _ => null
            };
        }

        private static BitmapImage? GetThumbnail(List<Thumbnail>? thumbnails) => ThumbnailHelper.GetThumbnail(thumbnails);
    }
}

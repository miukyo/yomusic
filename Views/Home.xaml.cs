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
    public sealed partial class Home : Page
    {
        private bool _fetched;

        public Home()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            _ = FetchAsync();
        }

        private async Task FetchAsync()
        {
            if (_fetched) return;
            LoadingRing.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            try
            {
                var client = await YTMusicClient.Client;
                var data = await client.GetHomeSectionsAsync();

                foreach (var section in data)
                {
                    if (string.IsNullOrWhiteSpace(section.Title) || section.Contents == null || !section.Contents.Any())
                        continue;

                    var contents = new List<ContentItem>();
                    foreach (var item in section.Contents)
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
                            default:
                                ci.Type = "SONG";
                                ci.ArtistName = GetArtistName(item);
                                break;
                        }
                        contents.Add(ci);
                    }

                    var row = new SectionRow
                    {
                        Margin = new Thickness(0, 28,0,0),
                        DataContext = new SectionModel
                        {
                            Title = section.Title,
                            Contents = contents
                        }
                    };
                    SectionList.Children.Add(row);
                }

                _fetched = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Home.FetchAsync: {ex}");
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            _fetched = false;
            SectionList.Children.Clear();
            await FetchAsync();
        }

        private static string GetArtistName(SearchResult item)
        {
            return item switch
            {
                SongDetailed s => s.Artist?.Name ?? "",
                VideoDetailed v => v.Artist?.Name ?? "",
                AlbumDetailed a => a.Artist?.Name ?? "",
                PlaylistDetailed p => p.Artist?.Name ?? "",
                _ => ""
            };
        }
    }
}

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
    public sealed partial class MoodsGenres : Page
    {
        private readonly List<MoodCategory> _moods = [];
        private string? _selectedParams;
        private bool _moodsLoaded;

        public MoodsGenres()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            MoodsLeftBtn.Click += OnMoodsLeftClick;
            MoodsRightBtn.Click += OnMoodsRightClick;
            MoodsScrollViewer.ViewChanged += OnMoodsScrollChanged;
            MoodsScrollViewer.SizeChanged += (s, e) => UpdateMoodsScrollButtons();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!_moodsLoaded)
                await LoadMoodsAsync();

            var moodParams = e.Parameter as string;
            if (moodParams == null || !_moods.Any(m => m.Params == moodParams))
                moodParams = _moods.Count > 0 ? _moods[0].Params : null;

            if (moodParams != null)
            {
                foreach (var child in MoodsGrid.Children)
                {
                    if (child is ToggleButton btn && (btn.Tag as string) == moodParams)
                    {
                        btn.IsChecked = true;
                        break;
                    }
                }
            }
        }

        private async Task LoadMoodsAsync()
        {
            try
            {
                var client = await YTMusicClient.Client;
                var moods = await client.GetMoodCategoriesAsync();
                if (moods.Count == 0) return;

                _moods.Clear();
                _moods.AddRange(moods.DistinctBy(m => m.Params));

                int count = _moods.Count;
                int cols = (count + 2) / 3;

                MoodsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MoodsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                MoodsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                for (int c = 0; c < cols; c++)
                    MoodsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                for (int i = 0; i < count; i++)
                {
                    var mood = _moods[i];
                    var btn = new ToggleButton
                    {
                        Content = mood.Title,
                        Width = 140,
                        Height = 60,
                        Margin = new Thickness(0, 0, 8, 8),
                        CornerRadius = new CornerRadius(8),
                        FontSize = 14,
                        Tag = mood.Params,

                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    btn.Checked += OnMoodChecked;
                    Grid.SetRow(btn, i % 3);
                    Grid.SetColumn(btn, i / 3);
                    MoodsGrid.Children.Add(btn);
                }

                UpdateMoodsScrollButtons();
                _moodsLoaded = true;
            }
            catch { }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void OnMoodChecked(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string moodParams && btn.IsChecked == true)
            {
                foreach (var child in MoodsGrid.Children)
                {
                    if (child is ToggleButton other && other != btn)
                        other.IsChecked = false;
                }
                SelectMood(moodParams);
            }
        }

        private async void SelectMood(string moodParams)
        {
            if (moodParams == _selectedParams) return;
            _selectedParams = moodParams;

            SectionList.Visibility = Visibility.Collapsed;
            EmptyText.Visibility = Visibility.Collapsed;

            LoadingRing.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            try
            {
                var client = await YTMusicClient.Client;
                var sections = await client.GetMoodSectionsAsync(moodParams);

                SectionList.Children.Clear();

                foreach (var section in sections)
                {
                    if (string.IsNullOrWhiteSpace(section.Title) || section.Contents == null || section.Contents.Count == 0)
                        continue;

                    var contents = section.Contents.Select(MapContentItem).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = section.Title,
                            Contents = contents
                        }
                    });
                }

                if (SectionList.Children.Count > 0)
                    SectionList.Visibility = Visibility.Visible;
                else
                {
                    EmptyText.Text = "No content found";
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

        private void OnMoodsLeftClick(object sender, RoutedEventArgs e)
        {
            var newOffset = Math.Max(0, MoodsScrollViewer.HorizontalOffset - 460);
            MoodsScrollViewer.ChangeView(newOffset, null, null);
        }

        private void OnMoodsRightClick(object sender, RoutedEventArgs e)
        {
            var newOffset = Math.Min(MoodsScrollViewer.ScrollableWidth, MoodsScrollViewer.HorizontalOffset + 460);
            MoodsScrollViewer.ChangeView(newOffset, null, null);
        }

        private void OnMoodsScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateMoodsScrollButtons();
        }

        private void UpdateMoodsScrollButtons()
        {
            MoodsLeftBtn.IsEnabled = MoodsScrollViewer.HorizontalOffset > 0;
            MoodsRightBtn.IsEnabled = MoodsScrollViewer.HorizontalOffset < MoodsScrollViewer.ScrollableWidth;
        }

        private static ContentItem MapContentItem(SearchResult item)
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
                    break;
            }
            return ci;
        }

        private static BitmapImage? GetThumbnail(List<Thumbnail>? thumbnails) => ThumbnailHelper.GetThumbnail(thumbnails);
    }
}

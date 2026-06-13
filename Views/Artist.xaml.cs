using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Controls;
using yomusic.Helpers;
using yomusic.Models;
using yomusic.Services;
using ytmusic_net;

namespace yomusic.Views
{
    public sealed partial class Artist : Page
    {
        private string? _artistId;

        public Artist()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            AnimationHelper.SetEntranceTransition(RootPanel);
            AnimationHelper.SetEntranceTransition(SongList);
            AnimationHelper.SetEntranceTransition(SectionList);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string id && id != _artistId)
            {
                _artistId = id;
                SongsTitle.Visibility = Visibility.Collapsed;
                EmptyText.Visibility = Visibility.Collapsed;
                SongList.Children.Clear();
                SectionList.Children.Clear();
                ElementCompositionPreview.SetElementChildVisual(HeroContainer, null);
                await LoadArtistAsync();
            }
        }

        private async Task LoadArtistAsync()
        {
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            try
            {
                var client = await YTMusicClient.Client;
                var artist = await client.GetArtistAsync(_artistId!);

                if (artist == null) return;

                ArtistName.Text = artist.Name;
                if (artist.Thumbnails?.Count > 0)
                {
                    var url = ThumbnailHelper.GetBestThumbnailUrl(artist.Thumbnails);
                    ApplyHeroMask(new Uri(url));
                }

                if (artist.TopSongs?.Count > 0)
                {
                    SongsTitle.Visibility = Visibility.Visible;
                    var fg = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
                    var fg2 = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];

                    foreach (var song in artist.TopSongs)
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

                        SongList.Children.Add(item);
                    }
                }

                if (artist.TopAlbums?.Count > 0)
                {
                    var contents = artist.TopAlbums.Select(album => new ContentItem
                    {
                        Type = "ALBUM",
                        Name = album.Name,
                        Thumbnail = GetThumbnail(album.Thumbnails),
                        ArtistName = album.Artist?.Name ?? "",
                        BrowseId = album.AlbumId,
                        Year = album.Year?.ToString(),
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Albums",
                            Contents = contents
                        }
                    });
                }

                if (artist.TopSingles?.Count > 0)
                {
                    var contents = artist.TopSingles.Select(single => new ContentItem
                    {
                        Type = "ALBUM",
                        Name = single.Name,
                        Thumbnail = GetThumbnail(single.Thumbnails),
                        ArtistName = single.Artist?.Name ?? "",
                        BrowseId = single.AlbumId,
                        Year = single.Year?.ToString(),
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Singles",
                            Contents = contents
                        }
                    });
                }

                if (artist.TopVideos?.Count > 0)
                {
                    var contents = artist.TopVideos.Select(video => new ContentItem
                    {
                        Type = "VIDEO",
                        Name = video.Name,
                        Thumbnail = GetThumbnail(video.Thumbnails),
                        ArtistName = video.Artist?.Name ?? "",
                        VideoId = video.VideoId,
                        Duration = DurationHelper.FormatDuration(video.Duration),
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Videos",
                            Contents = contents
                        }
                    });
                }

                if (artist.Playlists?.Count > 0)
                {
                    var contents = artist.Playlists.Select(pl => new ContentItem
                    {
                        Type = "PLAYLIST",
                        Name = pl.Name,
                        Thumbnail = GetThumbnail(pl.Thumbnails),
                        ArtistName = pl.Artist?.Name ?? "",
                        BrowseId = pl.PlaylistId,
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Playlists",
                            Contents = contents
                        }
                    });
                }

                if (artist.FeaturedOn?.Count > 0)
                {
                    var contents = artist.FeaturedOn.Select(fo => new ContentItem
                    {
                        Type = "PLAYLIST",
                        Name = fo.Name,
                        Thumbnail = GetThumbnail(fo.Thumbnails),
                        ArtistName = fo.Artist?.Name ?? "",
                        BrowseId = fo.PlaylistId,
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Featured on",
                            Contents = contents
                        }
                    });
                }

                if (artist.SimilarArtists?.Count > 0)
                {
                    var contents = artist.SimilarArtists.Select(sa => new ContentItem
                    {
                        Type = "ARTIST",
                        Name = sa.Name,
                        Thumbnail = GetThumbnail(sa.Thumbnails),
                        BrowseId = sa.ArtistId,
                    }).ToList();

                    SectionList.Children.Add(new SectionRow
                    {
                        Margin = new Thickness(0, 28, 0, 0),
                        DataContext = new SectionModel
                        {
                            Title = "Fans might also like",
                            Contents = contents
                        }
                    });
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

        private void ApplyHeroMask(Uri imageUri)
        {
            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            var surface = LoadedImageSurface.StartLoadFromUri(imageUri, Size.Empty);

            var surfaceBrush = compositor.CreateSurfaceBrush();
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.UniformToFill;

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(0, 1);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(255, 255, 255, 255)));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0, 255, 255, 255)));

            var maskBrush = compositor.CreateMaskBrush();
            maskBrush.Source = surfaceBrush;
            maskBrush.Mask = gradient;

            var spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Size = new Vector2((float)HeroContainer.ActualWidth, (float)HeroContainer.ActualHeight);
            spriteVisual.Brush = maskBrush;

            HeroContainer.SizeChanged += (s, e) =>
            {
                spriteVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            };

            ElementCompositionPreview.SetElementChildVisual(HeroContainer, spriteVisual);
        }

        private static BitmapImage? GetThumbnail(List<Thumbnail>? thumbnails) => ThumbnailHelper.GetThumbnail(thumbnails);
    }
}

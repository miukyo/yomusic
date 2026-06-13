using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Models;
using yomusic.Services;

namespace yomusic.Views
{
    public sealed partial class TrackView : Page
    {
        private LyricsResult? _lyrics;
        private int _currentLyricIndex = -1;

        public TrackView()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var item = TrackService.Instance.CurrentItem;
            if (item != null)
                UpdateTrackInfo(item);

            TrackService.Instance.CurrentItemChanged += OnCurrentItemChanged;
            TrackService.Instance.PositionChanged += OnPositionChanged;
            TrackService.Instance.QueueChanged += OnQueueChanged;

            RefreshQueue();
            _ = LoadLyricsAsync();

            QueueTabBtn.Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            TrackService.Instance.CurrentItemChanged -= OnCurrentItemChanged;
            TrackService.Instance.PositionChanged -= OnPositionChanged;
            TrackService.Instance.QueueChanged -= OnQueueChanged;
        }

        private void OnCurrentItemChanged(QueueItem? item)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (item != null)
                {
                    UpdateTrackInfo(item);
                    _lyrics = null;
                    _currentLyricIndex = -1;
                    NoLyricsText.Visibility = Visibility.Collapsed;
                    LyricsStack.Children.Clear();
                    await LoadLyricsAsync();
                }
            });
        }

        private void OnPositionChanged(TimeSpan position, TimeSpan duration)
        {
            if (_lyrics == null || _lyrics.Lines.Count == 0 || _lyrics.Type != LyricsType.Synced) return;
            var seconds = position.TotalSeconds + 0.3;
            var index = -1;
            for (int i = _lyrics.Lines.Count - 1; i >= 0; i--)
            {
                if (_lyrics.Lines[i].Time <= seconds)
                {
                    index = i;
                    break;
                }
            }
            if (index != _currentLyricIndex)
            {
                _currentLyricIndex = index;
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => HighlightLyric(index));
            }
        }

        private void OnQueueChanged(List<QueueItem> queue, int currentIndex)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => RefreshQueue());
        }

        private void UpdateTrackInfo(QueueItem item)
        {
            SongTitle.Text = item.Title;
            ArtistName.Text = item.Artist;
            if (item.Thumbnail != null)
            {
                AlbumArt.Source = item.Thumbnail;
                AlbumArt.Visibility = Visibility.Visible;
            }
            else if (!string.IsNullOrEmpty(item.ThumbnailUrl))
            {
                try
                {
                    AlbumArt.Source = new BitmapImage(new Uri(item.ThumbnailUrl));
                    AlbumArt.Visibility = Visibility.Visible;
                }
                catch { }
            }
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }

        private async Task LoadLyricsAsync()
        {
            var item = TrackService.Instance.CurrentItem;
            if (item == null) return;

            NoLyricsText.Visibility = Visibility.Collapsed;
            LyricsStack.Children.Clear();
            LyricsLoading.Visibility = Visibility.Visible;

            var result = await LyricsService.FetchAsync(item.Title, item.Artist, item.VideoId);

            LyricsLoading.Visibility = Visibility.Collapsed;

            if (result.Type == LyricsType.None)
            {
                NoLyricsText.Visibility = Visibility.Visible;
                return;
            }

            NoLyricsText.Visibility = Visibility.Collapsed;
            _lyrics = result;

            var dimColor = ((SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumLowBrush"]).Color;

            for (int i = 0; i < result.Lines.Count; i++)
            {
                var hasGap = i < result.Lines.Count - 1 && result.Lines[i + 1].Time - result.Lines[i].Time > 5;
                var tb = new TextBlock
                {
                    Text = result.Lines[i].Text,
                    FontSize = 28,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(dimColor),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.4,
                    Padding = new Thickness(0, 4, 0, 4),
                    Margin = new Thickness(0, 0, 0, hasGap ? 24 : 0)
                };
                LyricsStack.Children.Add(tb);
            }
        }

        private void HighlightLyric(int index)
        {
            var brightColor = ((SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"]).Color;
            var dimColor = ((SolidColorBrush)Application.Current.Resources["SystemControlForegroundBaseMediumLowBrush"]).Color;

            for (int i = 0; i < LyricsStack.Children.Count; i++)
            {
                if (LyricsStack.Children[i] is TextBlock tb)
                {
                    if (i == index)
                    {
                        //tb.FontWeight = FontWeights.SemiBold;
                        AnimateBrushColor(tb, brightColor);
                        AnimateOpacity(tb, 1.0f);
                    }
                    else
                    {
                        //tb.FontWeight = FontWeights.Normal;
                        AnimateBrushColor(tb, dimColor);
                        var dist = Math.Abs(i - index);
                        AnimateOpacity(tb, (float)Math.Max(0.15, 1.0 - dist * 0.15));
                    }
                }
            }

            if (index >= 0 && index < LyricsStack.Children.Count)
            {
                var target = LyricsStack.Children[index] as FrameworkElement;
                if (target != null)
                {
                    var transform = target.TransformToVisual(LyricsStack);
                    var point = transform.TransformPoint(new Point(0, 0));
                    var viewportHeight = LyricsPanel.ViewportHeight - 140;
                    if (viewportHeight > 0)
                    {
                        double offsetY = point.Y - viewportHeight * 0.3;
                        offsetY = Math.Max(0, Math.Min(offsetY, LyricsStack.ActualHeight - viewportHeight));
                        LyricsPanel.ChangeView(null, offsetY, null, false);
                    }
                }
            }
        }

        private void AnimateBrushColor(TextBlock tb, Color targetColor)
        {
            if (tb.Foreground is SolidColorBrush brush)
            {
                if (!SettingsService.Instance.EnableAnimations)
                {
                    brush.Color = targetColor;
                    return;
                }
                var storyboard = new Storyboard();
                var anim = new ColorAnimation
                {
                    To = targetColor,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(anim, brush);
                Storyboard.SetTargetProperty(anim, "Color");
                storyboard.Children.Add(anim);
                storyboard.Begin();
            }
        }

        private void AnimateOpacity(UIElement element, float target)
        {
            if (!SettingsService.Instance.EnableAnimations)
            {
                element.Opacity = target;
                return;
            }
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var anim = visual.Compositor.CreateScalarKeyFrameAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(300);
            anim.InsertKeyFrame(1, target);
            visual.StartAnimation("Opacity", anim);
        }

        private void RefreshQueue()
        {
            var queue = TrackService.Instance.Queue;
            var currentIndex = TrackService.Instance.CurrentIndex;

            bool same = queue.Count == QueueList.Children.Count;
            if (same)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    if (QueueList.Children[i] is Grid grid && grid.Tag is string vid && vid == queue[i].VideoId)
                        continue;
                    same = false;
                    break;
                }
            }

            if (same)
            {
                UpdateQueueIndicators(currentIndex);
                return;
            }

            QueueList.Children.Clear();

            var fg = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
            var fg2 = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"];
            var accent = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];

            for (int i = 0; i < queue.Count; i++)
            {
                var item = queue[i];
                var idx = i;

                var grid = new Grid
                {
                    Padding = new Thickness(8),
                    ColumnSpacing = 12,
                    Tag = item.VideoId
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });


                grid.Children.Add(new Grid
                {
                    Width = 4,
                    Height = 40,
                    CornerRadius = new CornerRadius(2),
                    Background = i == currentIndex ? accent : null
                });


                var thumb = item.Thumbnail;
                if (thumb == null && !string.IsNullOrEmpty(item.ThumbnailUrl))
                {
                    try { thumb = new BitmapImage(new Uri(item.ThumbnailUrl)); }
                    catch { }
                }
                if (thumb != null)
                {
                    var thumbBorder = new Border
                    {
                        Width = 48,
                        Height = 48,
                        CornerRadius = new CornerRadius(4),
                        Child = new Image
                        {
                            Width = 48,
                            Height = 48,
                            Stretch = Stretch.UniformToFill,
                            Source = thumb
                        }
                    };
                    grid.Children.Add(thumbBorder);
                    Grid.SetColumn(thumbBorder, 1);
                }

                var textStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                textStack.Children.Add(new TextBlock
                {
                    Text = item.Title,
                    FontWeight = idx == currentIndex ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = fg,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = item.Artist,
                    FontSize = 12,
                    Foreground = fg2,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                });
                Grid.SetColumn(textStack, 2);
                grid.Children.Add(textStack);

                var videoId = item.VideoId;
                grid.Tapped += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(videoId))
                        _ = TrackService.Instance.PlayVideoId(videoId);
                };

                QueueList.Children.Add(grid);
            }
        }

        private void UpdateQueueIndicators(int currentIndex)
        {
            var accent = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];

            for (int i = 0; i < QueueList.Children.Count; i++)
            {
                if (QueueList.Children[i] is Grid grid)
                {
                    var bar = grid.Children.FirstOrDefault(c => c is Grid g && Math.Abs(g.Width - 4) < 0.5) as Grid;
                    if (bar != null)
                        grid.Children.Remove(bar);

                    if (grid.Children[grid.Children.Count - 1] is StackPanel sp && sp.Children[0] is TextBlock title)
                        title.FontWeight = i == currentIndex ? FontWeights.SemiBold : FontWeights.Normal;

                    grid.Children.Insert(0, new Grid
                    {
                        Width = 4,
                        Height = 40,
                        CornerRadius = new CornerRadius(2),
                        Background = i == currentIndex ? accent : null
                    });
                }
            }

            if (currentIndex >= 0 && currentIndex < QueueList.Children.Count)
            {
                var element = QueueList.Children[currentIndex] as FrameworkElement;
                element?.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.3 });
            }
        }

        private void OnQueueTabClick(object sender, RoutedEventArgs e)
        {
            QueuePanel.Visibility = Visibility.Visible;
            LyricsPanel.Visibility = Visibility.Collapsed;
            LyricsTabBtn.Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"];
            QueueTabBtn.Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
        }

        private void OnLyricsTabClick(object sender, RoutedEventArgs e)
        {
            QueuePanel.Visibility = Visibility.Collapsed;
            LyricsPanel.Visibility = Visibility.Visible;
            LyricsTabBtn.Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
            QueueTabBtn.Foreground = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"];
        }

    }
}

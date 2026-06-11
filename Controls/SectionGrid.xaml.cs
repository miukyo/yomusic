using System;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using yomusic.Models;
using yomusic.Services;
using yomusic.Views;

namespace yomusic.Controls
{
    public sealed partial class SectionGrid : UserControl
    {
        public SectionGrid()
        {
            this.InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (DataContext is SectionModel section)
            {
                TitleText.Text = section.Title;
                ContentGrid.Children.Clear();
                foreach (var item in section.Contents)
                {
                    var card = BuildCard(item);
                    card.Tapped += (s, args) => NavigateToItem(item);
                    ContentGrid.Children.Add(card);
                }
            }
        }

        private static Border BuildCard(ContentItem item)
        {
            return item.Type switch
            {
                "VIDEO" => BuildVideoCard(item),
                "ARTIST" => BuildArtistCard(item),
                _ => BuildSquareCard(item),
            };
        }

        private static Border BuildSquareCard(ContentItem item)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imageContainer = new Border
            {
                Width = 180,
                Height = 180,
                CornerRadius = new CornerRadius(8),
                Child = new Image
                {
                    Width = 180,
                    Height = 180,
                    Stretch = Stretch.UniformToFill,
                    Source = item.Thumbnail
                }
            };
            root.Children.Add(imageContainer);

            var textStack = BuildTextStack(item);
            Grid.SetRow(textStack, 1);
            root.Children.Add(textStack);

            return new Border
            {
                Width = 180,
                Margin = new Thickness(0, 0, 12, 0),
                Child = root
            };
        }

        private static Border BuildVideoCard(ContentItem item)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imageContainer = new Border
            {
                Width = 320,
                Height = 180,
                CornerRadius = new CornerRadius(8),
                Child = new Image
                {
                    Width = 320,
                    Height = 180,
                    Stretch = Stretch.UniformToFill,
                    Source = item.Thumbnail
                }
            };
            root.Children.Add(imageContainer);

            var textStack = BuildTextStack(item);
            Grid.SetRow(textStack, 1);
            root.Children.Add(textStack);

            return new Border
            {
                Width = 320,
                Margin = new Thickness(0, 0, 12, 0),
                Child = root
            };
        }

        private static Border BuildArtistCard(ContentItem item)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var circle = new Border
            {
                Width = 180,
                Height = 180,
                CornerRadius = new CornerRadius(200),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Image
                {
                    Width = 180,
                    Height = 180,
                    Source = item.Thumbnail,
                    Stretch = Stretch.UniformToFill
                }
            };
            Grid.SetRow(circle, 0);
            root.Children.Add(circle);

            var textStack = BuildTextStack(item, true);
            Grid.SetRow(textStack, 1);
            root.Children.Add(textStack);

            return new Border
            {
                Width = 180,
                Margin = new Thickness(0, 0, 12, 0),
                Child = root
            };
        }

        private void NavigateToItem(ContentItem item)
        {
            var page = FindParent<Page>(this);
            if (page?.Frame == null) return;

            switch (item.Type)
            {
                case "PLAYLIST":
                case "ALBUM":
                    if (string.IsNullOrEmpty(item.BrowseId)) return;
                    page.Frame.Navigate(typeof(Playlist), (item.Type, item.BrowseId));
                    break;
                case "SONG":
                case "VIDEO":
                    if (string.IsNullOrEmpty(item.VideoId)) return;
                    var qi = new QueueItem
                    {
                        VideoId = item.VideoId,
                        Title = item.Name,
                        Artist = item.ArtistName ?? "",
                        Duration = item.Duration,
                        Thumbnail = item.Thumbnail,
                        ThumbnailUrl = ThumbnailHelper.ExtractUrl(item.Thumbnail)
                    };
                    _ = TrackService.Instance.PlayWithUpNextAsync(qi);
                    page.Frame.Navigate(typeof(TrackView), qi.VideoId);
                    break;
                case "ARTIST":
                    if (string.IsNullOrEmpty(item.BrowseId)) return;
                    page.Frame.Navigate(typeof(Artist), item.BrowseId);
                    break;
            }
        }
        private static T? FindParent<T>(DependencyObject element) where T : DependencyObject => ThumbnailHelper.FindParent<T>(element);

        private static StackPanel BuildTextStack(ContentItem item, bool center = false)
        {
            var foregroundColor = (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
            var foreground2Color = (Brush)Application.Current.Resources["SystemControlForegroundBaseMediumLowBrush"];
            var stack = new StackPanel { Padding = new Thickness(0, 10, 0, 0) };
            stack.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
                Foreground = foregroundColor
            });
            if (!string.IsNullOrEmpty(item.ArtistName))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = item.ArtistName,
                    FontSize = 12,
                    Foreground = foreground2Color,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = center ? TextAlignment.Center : TextAlignment.Left,
                    TextWrapping = TextWrapping.NoWrap
                });
            }
            return stack;
        }

    }
}

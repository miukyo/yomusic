using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using yomusic.Models;
using yomusic.Services;
using yomusic.Views;
using ytmusic_net;

using muxc = Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace yomusic
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            SetupCustomTitleBar();
            SearchBox.SearchSubmitted += (s, query) =>
                ContentFrame.Navigate(typeof(Search), query, new EntranceNavigationTransitionInfo());

            WirePlayerControls();
            TrackService.Instance.CurrentItemChanged += OnCurrentItemChanged;
            TrackService.Instance.PlayStateChanged += OnPlayStateChanged;
            TrackService.Instance.PositionChanged += OnPositionChanged;

            AccountService.Instance.LoginStateChanged += OnLoginStateChanged;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await AccountService.Instance.TryRestoreSessionAsync();
            OnLoginStateChanged(AccountService.Instance.IsLoggedIn);
            await TrackService.Instance.RestoreSessionAsync();

            VolumeSlider.Value = TrackService.Instance.Volume;
            var pos = TrackService.Instance.Position;
            var dur = TrackService.Instance.Duration;
            if (dur.TotalSeconds > 0)
            {
                ProgressSlider.Maximum = 100;
                ProgressSlider.Value = pos.TotalSeconds / dur.TotalSeconds * 100;
                CurrentTime.Text = FormatDuration(pos);
                TotalTime.Text = FormatDuration(dur);
            }
        }

        private int _loginGeneration;

        private async void OnLoginStateChanged(bool loggedIn)
        {
            if (loggedIn)
            {
                var gen = ++_loginGeneration;
                await AccountService.Instance.FetchAccountInfoAsync();
                if (gen != _loginGeneration) return;

                var info = AccountService.Instance.AccountInfo;
                if (info != null && !string.IsNullOrEmpty(info.AccountPhotoUrl))
                {
                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        ProfilePicture.ProfilePicture = new BitmapImage(new Uri(info.AccountPhotoUrl));
                    });
                }
                else
                {
                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        ProfilePicture.Initials = "U";
                    });
                }

                if (gen != _loginGeneration) return;
            }
            else
            {
                ++_loginGeneration;
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ProfilePicture.ProfilePicture = null;
                    ProfilePicture.Initials = "?";
                });
            }
        }

        private void OnProfileButtonClick(object sender, RoutedEventArgs e)
        {
            if (AccountService.Instance.IsLoggedIn)
            {
                var flyout = new MenuFlyout();
                var logoutItem = new MenuFlyoutItem { Text = "Logout" };
                logoutItem.Icon = new FontIcon { Glyph = (string)Application.Current.Resources["MgcExitLine"], FontFamily = (FontFamily)Application.Current.Resources["MingCute"] };
                logoutItem.Click += async (s, args) =>
                {
                    await AccountService.Instance.LogoutAsync();
                    OnLoginStateChanged(false);
                };
                flyout.Placement = Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom;
                flyout.Items.Add(logoutItem);
                flyout.ShowAt(ProfileButton);
            }
            else
            {
                ShowLoginOverlay();
            }
        }

        private async void ShowLoginOverlay()
        {
            if (LoginWebView2 == null)
            {
                LoginWebView2 = new muxc.WebView2();
                LoginWebView2.SetValue(Grid.RowProperty, 1);
                LoginWebViewGrid.Children.Insert(1, LoginWebView2);
            }
            LoginOverlay.Visibility = Visibility.Visible;
            LoginWebView2.NavigationStarting += OnLoginNavigationStarting;
            LoginWebView2.NavigationCompleted += OnLoginNavigationCompleted;

            await LoginWebView2.EnsureCoreWebView2Async();
            //LoginWebView2.CoreWebView2.CookieManager.DeleteAllCookies();
            LoginWebView2.Source = new Uri("https://accounts.google.com/ServiceLogin?service=youtube&continue=https://music.youtube.com/&hl=en");
        }

        private void HideLoginOverlay()
        {
            LoginWebView2.Visibility = Visibility.Visible;
            LoginProgressRing.IsActive = false;
            LoginProgressPanel.Visibility = Visibility.Collapsed;
            LoginWebView2.NavigationStarting -= OnLoginNavigationStarting;
            LoginWebView2.NavigationCompleted -= OnLoginNavigationCompleted;
            LoginWebView2.CoreWebView2?.Stop();
            LoginWebViewGrid.Children.Remove(LoginWebView2);
            LoginWebView2.Close();
            LoginWebView2 = null;
            LoginOverlay.Visibility = Visibility.Collapsed;
        }

        private async void OnLoginNavigationStarting(muxc.WebView2 sender, CoreWebView2NavigationStartingEventArgs e)
        {
            try
            {
                var source = LoginWebView2.Source;
                if (source == null) return;

                if (source.Host == "consent.youtube.com")
                {
                    await LoginWebView2.ExecuteScriptAsync("document.querySelector('form').submit();");
                }
            }
            catch { }
        }

        private async void OnLoginNavigationCompleted(muxc.WebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                var source = LoginWebView2.Source;
                if (source?.Host == "music.youtube.com" && !source.AbsolutePath.StartsWith("/signin"))
                {
                    LoginWebView2.Visibility = Visibility.Collapsed;
                    LoginProgressRing.IsActive = true;
                    LoginProgressPanel.Visibility = Visibility.Visible;
                    LoginStatusText.Text = "Logging in...";
                    for (int i = 0; i < 20; i++)
                    {

                        var result = await LoginWebView2.CoreWebView2.ExecuteScriptAsync(
                                "document.querySelector('ytmusic-nav-bar ytmusic-settings-button') ? 'found' : ''");
                        if (result == "\"found\"")
                        {
                            for (int attempt = 0; attempt < 5; attempt++)
                            {
                                var musicCookies = await LoginWebView2.CoreWebView2.CookieManager.GetCookiesAsync("https://music.youtube.com");

                                var pairs = new List<string>();
                                foreach (var c in musicCookies)
                                    pairs.Add($"{c.Name}={c.Value}");

                                var raw = string.Join("; ", pairs);
                                if (!string.IsNullOrEmpty(raw))
                                {
                                    try
                                    {
                                        using var testClient = await new YTMusic().InitializeAsync(raw);
                                        var info = await testClient.GetAccountInfoAsync();
                                        if (info != null && !string.IsNullOrEmpty(info.AccountName))
                                        {
                                            HideLoginOverlay();
                                            await AccountService.Instance.LoginAsync(raw);
                                            return;
                                        }
                                    }
                                    catch { }
                                }
                                await Task.Delay(1000);
                            }
                            return;
                        }
                    }
                }
            }
            catch { }
            // On failure (element not found or validation failed), restore WebView2 visibility
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                LoginWebView2.Visibility = Visibility.Visible;
                LoginProgressRing.IsActive = false;
                LoginProgressPanel.Visibility = Visibility.Collapsed;
            });
        }

        private void OnLoginCloseClick(object sender, RoutedEventArgs e)
        {
            HideLoginOverlay();
        }

        private void OnLoginOverlayTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (e.OriginalSource == LoginOverlay)
                HideLoginOverlay();
        }

        private void WirePlayerControls()
        {
            PlayPauseButton.Click += (s, e) => TrackService.Instance.PlayPause();
            PreviousButton.Click += (s, e) => TrackService.Instance.Previous();
            NextButton.Click += (s, e) => TrackService.Instance.Next();
            ShuffleButton.Click += (s, e) =>
            {
                TrackService.Instance.ToggleShuffle();
            };
            RepeatButton.Click += (s, e) =>
            {
                TrackService.Instance.CycleRepeatMode();
            };
            QueueButton.Click += (s, e) =>
            {
                if (TrackService.Instance.CurrentItem != null)
                    ContentFrame.Navigate(typeof(TrackView), TrackService.Instance.CurrentItem.VideoId,
                        new EntranceNavigationTransitionInfo());
            };

            TrackInfo.Tapped += (s, e) =>
            {
                if (TrackService.Instance.CurrentItem != null)
                    ContentFrame.Navigate(typeof(TrackView), TrackService.Instance.CurrentItem.VideoId,
                        new EntranceNavigationTransitionInfo());
            };

            ProgressSlider.AddHandler(PointerPressedEvent, new PointerEventHandler((s, e) => _draggingProgress = true), true);
            ProgressSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler((s, e) =>
            {
                _draggingProgress = false;
                if (TrackService.Instance.Duration.TotalSeconds > 0)
                {
                    var pos = TimeSpan.FromSeconds(ProgressSlider.Value * TrackService.Instance.Duration.TotalSeconds / 100);
                    TrackService.Instance.Seek(pos);
                }
            }), true);

            VolumeSlider.ValueChanged += (s, e) =>
            {
                TrackService.Instance.Volume = e.NewValue;
            };
        }

        private bool _draggingProgress;

        private void OnCurrentItemChanged(QueueItem? item)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (item != null)
                {
                    TrackTitle.Text = item.Title;
                    ArtistName.Text = item.Artist;
                    ProgressSlider.Value = 0;
                    CurrentTime.Text = FormatDuration(TimeSpan.Zero);
                    AlbumArt.Source = null;
                    if (item.Thumbnail != null)
                        AlbumArt.Source = item.Thumbnail;
                    else if (!string.IsNullOrEmpty(item.ThumbnailUrl))
                    {
                        try { AlbumArt.Source = new BitmapImage(new Uri(item.ThumbnailUrl)); }
                        catch { }
                    }
                }
            });
        }

        private void OnPlayStateChanged(bool isPlaying)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayPauseButton.Content = isPlaying
                    ? Application.Current.Resources["MgcPauseFill"]
                    : Application.Current.Resources["MgcPlayFill"];
            });
        }

        private void OnPositionChanged(TimeSpan position, TimeSpan duration)
        {
            if (_draggingProgress) return;
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (duration.TotalSeconds > 0)
                {
                    ProgressSlider.Maximum = 100;
                    ProgressSlider.Value = position.TotalSeconds / duration.TotalSeconds * 100;
                }
                else
                {
                    ProgressSlider.Value = 0;
                }
                CurrentTime.Text = FormatDuration(position);
                TotalTime.Text = FormatDuration(duration);
            });
        }

        private static string FormatDuration(TimeSpan ts) => DurationHelper.FormatDuration(ts);

        private readonly Dictionary<string, Type> _pages = new Dictionary<string, Type>
        {
            { "Home", typeof(Home) },
            { "Explore", typeof(Explore) },
            { "Library", typeof(Library) },
            { "Moods", typeof(MoodsGenres) },
            { "History", typeof(Views.History) }
        };

        private void SetupCustomTitleBar()
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = (Color)Application.Current.Resources["SystemBaseHighColor"];
            titleBar.ButtonHoverBackgroundColor = (Color)Application.Current.Resources["SystemBaseLowColor"];
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args) { }

        private void NavView_ItemInvoked(muxc.NavigationView sender, muxc.NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                var tag = args.InvokedItemContainer.Tag.ToString();
                NavigateToPage(tag);
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            NavigateToPage("Home");
        }


        private void NavigateToPage(string tag)
        {
            if (_pages.TryGetValue(tag, out Type pageType))
            {
                ContentFrame.Navigate(pageType, tag, new EntranceNavigationTransitionInfo());
                ContentFrame.BackStack.Clear();
            }
            else if (!string.IsNullOrEmpty(tag))
            {
                ContentFrame.Navigate(typeof(Playlist), ("PLAYLIST", tag), new EntranceNavigationTransitionInfo());
                ContentFrame.BackStack.Clear();
            }
        }

        private void appTitleBar_BackButtonClick(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();

                Type currentPageType = ContentFrame.CurrentSourcePageType;

                foreach (muxc.NavigationViewItem item in NavView.MenuItems)
                {
                    if (item.Tag != null && _pages.TryGetValue(item.Tag.ToString(), out Type pageType))
                    {
                        if (pageType == currentPageType)
                        {
                            NavView.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void NavView_SelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = args.SelectedItem as muxc.NavigationViewItem;
            if (selectedItem == null || selectedItem.Tag == null) return;

            string activeTag = selectedItem.Tag.ToString();

            AnimateNavItem(HomePanel, HomeIconContainer, HomeIconOutline, HomeIconFilled, HomeText, activeTag == "Home");
            AnimateNavItem(ExplorePanel, ExploreIconContainer, ExploreIconOutline, ExploreIconFilled, ExploreText, activeTag == "Explore");
            AnimateNavItem(LibraryPanel, LibraryIconContainer, LibraryIconOutline, LibraryIconFilled, LibraryText, activeTag == "Library");
            AnimateNavItem(MoodsPanel, MoodsIconContainer, MoodsIconOutline, MoodsIconFilled, MoodsText, activeTag == "Moods");
            AnimateNavItem(HistoryPanel, HistoryIconContainer, HistoryIconOutline, HistoryIconFilled, HistoryText, activeTag == "History");
        }

        private void AnimateNavItem(StackPanel panel, Grid iconContainer, IconElement outlineIcon, IconElement filledIcon, TextBlock text, bool isSelected)
        {
            var storyboard = new Storyboard();
            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var textFade = new DoubleAnimation
            {
                To = isSelected ? 0.0 : 1.0,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(textFade, text);
            Storyboard.SetTargetProperty(textFade, "(UIElement.Opacity)");
            storyboard.Children.Add(textFade);

            var outlineFade = new DoubleAnimation
            {
                To = isSelected ? 0.0 : 1.0,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(outlineFade, outlineIcon);
            Storyboard.SetTargetProperty(outlineFade, "(UIElement.Opacity)");
            storyboard.Children.Add(outlineFade);

            var filledFade = new DoubleAnimation
            {
                To = isSelected ? 1.0 : 0.0,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(filledFade, filledIcon);
            Storyboard.SetTargetProperty(filledFade, "(UIElement.Opacity)");
            storyboard.Children.Add(filledFade);

            var translation = iconContainer.RenderTransform as TranslateTransform;
            if (translation == null)
            {
                translation = new TranslateTransform();
                iconContainer.RenderTransform = translation;
            }

            var containerMove = new DoubleAnimation
            {
                To = isSelected ? 9.0 : 0.0,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(containerMove, translation);
            Storyboard.SetTargetProperty(containerMove, "(TranslateTransform.Y)");
            storyboard.Children.Add(containerMove);

            if (isSelected)
            {
                var scale = panel.RenderTransform as ScaleTransform;
                if (scale == null)
                {
                    scale = new ScaleTransform();
                    panel.RenderTransform = scale;
                }

                void AddBouncyAnimation(string property)
                {
                    var frames = new DoubleAnimationUsingKeyFrames();
                    Storyboard.SetTarget(frames, scale);
                    Storyboard.SetTargetProperty(frames, property);

                    frames.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0)), Value = 1.0 });
                    frames.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)), Value = 0.85 });
                    frames.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), Value = 1.15 });
                    frames.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), Value = 0.95 });
                    frames.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350)), Value = 1.0 });

                    storyboard.Children.Add(frames);
                }

                AddBouncyAnimation("(ScaleTransform.ScaleX)");
                AddBouncyAnimation("(ScaleTransform.ScaleY)");
            }

            storyboard.Begin();
        }
    }
}

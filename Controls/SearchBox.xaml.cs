using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using yomusic.Services;

namespace yomusic.Controls
{
    public sealed partial class SearchBox : UserControl
    {
        public event EventHandler<string>? SearchSubmitted;

        private DispatcherTimer _debounceTimer;
        private Flyout _suggestionsFlyout;
        private Border _suggestionsBorder;
        private ListView _suggestionsList;
        private string _lastQuery = string.Empty;
        private bool _suppressClose;
        private bool _pendingSubmitFocus;
        private bool _suppressSuggestions;
        private bool _submitting;
        private DateTime _lastSubmitTime = DateTime.MinValue;

        public SearchBox()
        {
            this.InitializeComponent();

            _suggestionsList = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                IsItemClickEnabled = true
            };
            _suggestionsList.ItemClick += OnSuggestionClick;

            _suggestionsBorder = new Border
            {
                Child = _suggestionsList
            };

            _suggestionsFlyout = new Flyout
            {
                Content = _suggestionsBorder,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter))
                {
                    Setters =
                    {
                        new Setter(FlyoutPresenter.PaddingProperty, new Thickness(0)),
                        new Setter(FlyoutPresenter.CornerRadiusProperty, new CornerRadius(8)),
                        new Setter(FlyoutPresenter.MinWidthProperty, 0),
                        new Setter(FlyoutPresenter.MaxWidthProperty, 1000),
                    }
                }
            };
            _suggestionsFlyout.Opened += (s, e) =>
            {
                _suppressClose = true;
                InputBox.Focus(FocusState.Programmatic);
            };
            _suggestionsFlyout.Closing += (s, e) =>
            {
                if (_suppressClose)
                {
                    e.Cancel = true;
                    _suppressClose = false;
                }
            };
            _suggestionsFlyout.Closed += (s, e) =>
            {
                _suggestionsList.SelectedIndex = -1;
                if (_pendingSubmitFocus)
                {
                    _pendingSubmitFocus = false;
                    FocusTarget.Focus(FocusState.Programmatic);
                }
            };

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += async (s, e) =>
            {
                _debounceTimer.Stop();
                await FetchSuggestionsAsync();
            };
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            if (_suppressSuggestions) return;
            _debounceTimer.Start();
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Down:
                    if (_suggestionsFlyout.IsOpen && _suggestionsList.Items.Count > 0)
                    {
                        var idx = _suggestionsList.SelectedIndex;
                        if (idx < _suggestionsList.Items.Count - 1)
                            _suggestionsList.SelectedIndex = idx + 1;
                        else
                            _suggestionsList.SelectedIndex = 0;
                        _suggestionsList.ScrollIntoView(_suggestionsList.SelectedItem);
                        e.Handled = true;
                    }
                    break;

                case VirtualKey.Up:
                    if (_suggestionsFlyout.IsOpen && _suggestionsList.Items.Count > 0)
                    {
                        var idx = _suggestionsList.SelectedIndex;
                        if (idx > 0)
                            _suggestionsList.SelectedIndex = idx - 1;
                        else
                            _suggestionsList.SelectedIndex = _suggestionsList.Items.Count - 1;
                        _suggestionsList.ScrollIntoView(_suggestionsList.SelectedItem);
                        e.Handled = true;
                    }
                    break;

                case VirtualKey.Enter:
                    if (_suggestionsFlyout.IsOpen && _suggestionsList.SelectedItem is string selected)
                    {
                        _suppressSuggestions = true;
                        InputBox.Text = selected;
                        _suppressSuggestions = false;
                        Submit(selected);
                    }
                    else
                        Submit(InputBox.Text);
                    e.Handled = true;
                    break;

                case VirtualKey.Escape:
                    if (_suggestionsFlyout.IsOpen)
                    {
                        _suggestionsFlyout.Hide();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnSuggestionClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string suggestion)
            {
                _suppressSuggestions = true;
                InputBox.Text = suggestion;
                _suppressSuggestions = false;
                Submit(suggestion);
            }
        }

        private async Task FetchSuggestionsAsync()
        {
            if (_submitting) return;
            if ((DateTime.Now - _lastSubmitTime).TotalMilliseconds < 500) return;
            if (InputBox.FocusState == FocusState.Unfocused) return;
            var query = InputBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                if (_suggestionsFlyout.IsOpen)
                    _suggestionsFlyout.Hide();
                return;
            }

            if (query == _lastQuery)
                return;
            _lastQuery = query;

            try
            {
                var client = await YTMusicClient.Client;
                var suggestions = await client.GetSearchSuggestionsAsync(query);
                _suggestionsList.Items.Clear();
                foreach (var s in suggestions)
                    _suggestionsList.Items.Add(s);

                if (_suggestionsList.Items.Count > 0)
                {
                    _suggestionsList.SelectedIndex = -1;
                    if (!_suggestionsFlyout.IsOpen)
                    {
                        _suggestionsBorder.Width = InputBox.ActualWidth;
                        _suggestionsFlyout.ShowAt(InputBox);
                    }
                }
                else if (_suggestionsFlyout.IsOpen)
                {
                    _suggestionsFlyout.Hide();
                }
            }
            catch
            {
                if (_suggestionsFlyout.IsOpen)
                    _suggestionsFlyout.Hide();
            }
        }

        private void Submit(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            _lastSubmitTime = DateTime.Now;
            _submitting = true;
            _lastQuery = string.Empty;
            _debounceTimer.Stop();
            _pendingSubmitFocus = true;
            if (_suggestionsFlyout.IsOpen)
                _suggestionsFlyout.Hide();
            else
                FocusTarget.Focus(FocusState.Programmatic);
            SearchSubmitted?.Invoke(this, query);
            _submitting = false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FFmpegInteropX;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using yomusic.Models;
using ytmusic_net;
using ytmusic_net.streams;

namespace yomusic.Services
{
    public enum RepeatMode
    {
        Off,
        All,
        One
    }

    public sealed class TrackService
    {
        private static readonly Lazy<TrackService> _instance = new(() => new TrackService());
        public static TrackService Instance => _instance.Value;

        private readonly MediaPlayer _player = new();
        private DispatcherTimer _progressTimer;
        private List<QueueItem> _queue = new();
        private int _currentIndex = -1;
        private bool _isShuffled;
        private RepeatMode _repeatMode;
        private FFmpegMediaSource? _ffmpegSource;

        public event Action<QueueItem?>? CurrentItemChanged;
        public event Action<bool>? PlayStateChanged;
        public event Action<TimeSpan, TimeSpan>? PositionChanged;
        public event Action<List<QueueItem>, int>? QueueChanged;

        public QueueItem? CurrentItem => _currentIndex >= 0 && _currentIndex < _queue.Count ? _queue[_currentIndex] : null;
        public bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
        public TimeSpan Position => _player.PlaybackSession.Position;
        public TimeSpan Duration => _player.PlaybackSession.NaturalDuration;
        public IReadOnlyList<QueueItem> Queue => _queue.AsReadOnly();
        public int CurrentIndex => _currentIndex;
        public bool IsShuffled => _isShuffled;
        public RepeatMode Repeat => _repeatMode;

        private SystemMediaTransportControls _smtc;
        private bool _uiInitialized;

        private TrackService()
        {
            _player.MediaEnded += OnMediaEnded;
            _player.CurrentStateChanged += OnStateChanged;
            _player.AudioCategory = MediaPlayerAudioCategory.Media;
        }

        public async Task RestoreSessionAsync()
        {
            EnsureUiInitialized();
            var session = await CacheService.LoadSessionAsync();
            if (session == null || session.Queue.Count == 0)
                return;

            _queue = session.Queue;
            _currentIndex = Math.Clamp(session.CurrentIndex, 0, _queue.Count - 1);
            _isShuffled = session.IsShuffled;
            _repeatMode = (RepeatMode)session.RepeatMode;
            Volume = 0;

            QueueChanged?.Invoke(new List<QueueItem>(_queue), _currentIndex);

            var item = CurrentItem;
            if (item != null)
            {
                CurrentItemChanged?.Invoke(item);
                await PlayCurrent();
                _player.PlaybackSession.Position = TimeSpan.FromSeconds(session.PositionSeconds);
                _player.Pause();
                Volume = session.Volume;
            }
        }

        public async Task SaveSessionAsync()
        {
            var session = new SessionData
            {
                Queue = new List<QueueItem>(_queue),
                CurrentIndex = _currentIndex,
                PositionSeconds = Position.TotalSeconds,
                IsShuffled = _isShuffled,
                RepeatMode = (int)_repeatMode,
                Volume = Volume
            };
            await CacheService.SaveSessionAsync(session);
        }

        private void EnsureUiInitialized()
        {
            if (_uiInitialized) return;
            _uiInitialized = true;

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _progressTimer.Tick += OnProgressTick;

            _player.CommandManager.IsEnabled = true;
            _smtc = SystemMediaTransportControls.GetForCurrentView();
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.ButtonPressed += OnSmtcButtonPressed;
        }

        public double Volume
        {
            get => _player.Volume * 100;
            set
            {
                _player.Volume = Math.Clamp(value / 100.0, 0, 1);
                _ = SaveSessionAsync();
            }
        }

        public void PlayQueue(List<QueueItem> items, int startIndex = 0)
        {
            EnsureUiInitialized();
            if (items == null || items.Count == 0) return;
            _queue = new List<QueueItem>(items);
            _currentIndex = Math.Clamp(startIndex, 0, _queue.Count - 1);
            QueueChanged?.Invoke(new List<QueueItem>(_queue), _currentIndex);
            _ = SaveSessionAsync();
            PlayCurrent();
        }

        public void PlayItem(QueueItem item)
        {
            PlayQueue(new List<QueueItem> { item }, 0);
        }

        public async Task PlayWithUpNextAsync(QueueItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.VideoId)) return;

            var queue = new List<QueueItem> { item };

            try
            {
                var client = await YTMusicClient.Client;
                var upNext = await client.GetUpNextsAsync(item.VideoId);
                foreach (var u in upNext)
                {
                    queue.Add(new QueueItem
                    {
                        VideoId = u.VideoId,
                        Title = u.Title,
                        Artist = u.Artists,
                        Duration = u.Duration,
                        ThumbnailUrl = u.Thumbnail
                    });
                }
            }
            catch
            {
            }

            PlayQueue(queue, 0);
        }

        public void PlayPause()
        {
            EnsureUiInitialized();
            if (CurrentItem == null) return;
            if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                _player.Pause();
            else
                _player.Play();
        }

        public void Play()
        {
            EnsureUiInitialized();
            if (CurrentItem == null) return;
            _player.Play();
        }

        public void Pause()
        {
            EnsureUiInitialized();
            _player.Pause();
        }

        public void Next()
        {
            EnsureUiInitialized();
            if (_queue.Count == 0) return;

            if (_repeatMode == RepeatMode.One)
            {
                _ = PlayCurrent();
                return;
            }

            int nextIndex;
            if (_isShuffled)
            {
                var remaining = Enumerable.Range(0, _queue.Count)
                    .Where(i => i != _currentIndex)
                    .OrderBy(_ => Guid.NewGuid())
                    .ToList();
                nextIndex = remaining.Count > 0 ? remaining[0] : _currentIndex;
            }
            else
            {
                nextIndex = _currentIndex + 1;
            }

            if (nextIndex >= _queue.Count)
            {
                if (_repeatMode == RepeatMode.All)
                {
                    nextIndex = 0;
                }
                else
                {
                    _player.Pause();
                    _currentIndex = _queue.Count;
                    CurrentItemChanged?.Invoke(null);
                    return;
                }
            }

            _currentIndex = nextIndex;
            QueueChanged?.Invoke(new List<QueueItem>(_queue), _currentIndex);
            _ = SaveSessionAsync();
            _ = PlayCurrent();
        }

        public void Previous()
        {
            EnsureUiInitialized();
            if (_queue.Count == 0) return;

            if (_repeatMode == RepeatMode.One)
            {
                _ = PlayCurrent();
                return;
            }

            int prevIndex;
            if (_isShuffled)
            {
                var remaining = Enumerable.Range(0, _queue.Count)
                    .Where(i => i != _currentIndex)
                    .OrderBy(_ => Guid.NewGuid())
                    .ToList();
                prevIndex = remaining.Count > 0 ? remaining[0] : _currentIndex;
            }
            else
            {
                prevIndex = _currentIndex - 1;
            }

            if (prevIndex < 0)
            {
                if (_repeatMode == RepeatMode.All)
                    prevIndex = _queue.Count - 1;
                else
                    return;
            }

            _currentIndex = prevIndex;
            QueueChanged?.Invoke(new List<QueueItem>(_queue), _currentIndex);
            _ = SaveSessionAsync();
            _ = PlayCurrent();
        }

        public void Seek(TimeSpan position)
        {
            _player.PlaybackSession.Position = position;
        }

        public void ToggleShuffle()
        {
            _isShuffled = !_isShuffled;
            _ = SaveSessionAsync();
        }

        public void SetRepeatMode(RepeatMode mode)
        {
            _repeatMode = mode;
            _ = SaveSessionAsync();
        }

        public void CycleRepeatMode()
        {
            _repeatMode = _repeatMode switch
            {
                RepeatMode.Off => RepeatMode.All,
                RepeatMode.All => RepeatMode.One,
                RepeatMode.One => RepeatMode.Off,
                _ => RepeatMode.Off
            };
            _ = SaveSessionAsync();
        }

        public void ClearQueue()
        {
            EnsureUiInitialized();
            _player.Pause();
            if (_ffmpegSource != null)
            {
                _ffmpegSource.Dispose();
                _ffmpegSource = null;
            }
            _player.Source = null;
            _queue.Clear();
            _currentIndex = -1;
            _ = DispatcherRun(() => _progressTimer.Stop());
            QueueChanged?.Invoke(new List<QueueItem>(), -1);
            CurrentItemChanged?.Invoke(null);
            _ = SaveSessionAsync();
        }

        public async Task PlayAt(int index)
        {
            if (index < 0 || index >= _queue.Count) return;
            _currentIndex = index;
            QueueChanged?.Invoke(new List<QueueItem>(_queue), _currentIndex);
            _ = SaveSessionAsync();
            await PlayCurrent();
        }

        public async Task PlayVideoId(string videoId)
        {
            var item = _queue.FirstOrDefault(q => q.VideoId == videoId);
            if (item != null)
            {
                int idx = _queue.IndexOf(item);
                await PlayAt(idx);
                return;
            }
            var newItem = new QueueItem { VideoId = videoId };
            PlayQueue(new List<QueueItem> { newItem }, 0);
        }

        public async Task<string?> GetStreamUrlAsync(string videoId)
        {
            try
            {
                var client = await YTMusicClient.Client;
                var manifest = await client.GetStreamManifestAsync(videoId);

                var all = manifest.Streams
                    .Where(s => s is IAudioStreamInfo)
                    .OrderByDescending(s => s.Bitrate.BitsPerSecond)
                    .ToList();
                if (all.Count > 0)
                    return all[0].Url;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task PlayCurrent()
        {
            var item = CurrentItem;
            if (item == null) return;

            CurrentItemChanged?.Invoke(item);
            UpdateSystemTransportControls(item);
            await SaveSessionAsync();
            _ = HistoryService.Instance.RecordPlayAsync(item);

            try
            {
                _player.Pause();
                if (_ffmpegSource != null)
                {
                    _ffmpegSource.Dispose();
                    _ffmpegSource = null;
                }
                _player.Source = null;

                var url = await GetStreamUrlAsync(item.VideoId);
                if (url == null) return;

                var config = new MediaSourceConfig();
                config.FFmpegOptions = new PropertySet
                {
                    { "reconnect", 1 },
                    { "reconnect_streamed", 1 },
                    { "reconnect_on_network_error", 1 },
                };
                _ffmpegSource = await FFmpegMediaSource.CreateFromUriAsync(url, config);
                _player.AutoPlay = true;
                await _ffmpegSource.OpenWithMediaPlayerAsync(_player);
            }
            catch
            {
                _ = DispatcherRun(() => _progressTimer.Stop());
                var idx = _currentIndex;
                Next();
                if (idx == _currentIndex) return;
            }
        }

        private void UpdateSystemTransportControls(QueueItem item)
        {
            try
            {
                var updater = _smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Title = item.Title;
                updater.MusicProperties.Artist = item.Artist;
                if (!string.IsNullOrEmpty(item.ThumbnailUrl))
                {
                    updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(item.ThumbnailUrl));
                }
                updater.Update();
            }
            catch
            {
            }
        }

        private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    Previous();
                    break;
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            _ = DispatcherRun(() => Next());
        }

        private void OnStateChanged(MediaPlayer sender, object args)
        {
            bool playing = IsPlaying;
            PlayStateChanged?.Invoke(playing);
            try
            {
                _smtc.PlaybackStatus = playing
                    ? MediaPlaybackStatus.Playing
                    : MediaPlaybackStatus.Paused;
            }
            catch { }

            _ = DispatcherRun(() =>
            {
                if (playing)
                    _progressTimer.Start();
                else
                    _progressTimer.Stop();
            });
            _ = SaveSessionAsync();
        }

        private int _positionSaveTick = 0;

        private void OnProgressTick(object sender, object e)
        {
            if (IsPlaying && Duration.TotalSeconds > 0)
                PositionChanged?.Invoke(Position, Duration);

            _positionSaveTick++;
            if (_positionSaveTick >= 20)
            {
                _positionSaveTick = 0;
                _ = SaveSessionAsync();
            }

            if (Position >= Duration || (Duration - Position).TotalMilliseconds <= 500)
            {
                _progressTimer.Stop();
                _ = DispatcherRun(() => Next());
            }
        }

        private static async Task DispatcherRun(Action action)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal, () => action());
        }
    }
}

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ytmusic_net;

namespace yomusic.Services
{
    public sealed class YTMusicClient
    {
        private static Task<YTMusic>? _clientTask;
        private static bool _createdWithCookies;
        private static readonly object _lock = new();

        public static bool IsAuthenticated { get; private set; }

        public static Task<YTMusic> Client => GetClientAsync();

        private static async Task<YTMusic> GetClientAsync()
        {
            var cookies = await AccountService.Instance.GetCookiesAsync();
            var hasCookies = !string.IsNullOrEmpty(cookies);

            Task<YTMusic>? task;
            lock (_lock)
            {
                if (_clientTask == null || _createdWithCookies != hasCookies)
                {
                    DisposeIfCompleted(_clientTask);
                    _createdWithCookies = hasCookies;
                    IsAuthenticated = hasCookies;
                    Debug.WriteLine(hasCookies
                        ? $"YTMusicClient: initializing with cookies ({cookies!.Length} chars)"
                        : "YTMusicClient: initializing without cookies");
                    _clientTask = hasCookies
                        ? new YTMusic().InitializeAsync(cookies)
                        : new YTMusic().InitializeAsync();
                }
                task = _clientTask;
            }

            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"YTMusicClient: init failed ({ex.Message}), retrying...");
                lock (_lock)
                {
                    if (_clientTask == task)
                    {
                        _clientTask = null;
                        _createdWithCookies = false;
                        IsAuthenticated = false;
                    }
                }
            }

            lock (_lock)
            {
                DisposeIfCompleted(_clientTask);
                _createdWithCookies = hasCookies;
                IsAuthenticated = hasCookies;
                _clientTask = hasCookies
                    ? new YTMusic().InitializeAsync(cookies)
                    : new YTMusic().InitializeAsync();
                task = _clientTask;
            }
            return await task;
        }

        private static void DisposeIfCompleted(Task<YTMusic>? task)
        {
            if (task?.IsCompletedSuccessfully == true)
            {
                task.Result.Dispose();
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                DisposeIfCompleted(_clientTask);
                _clientTask = null;
                _createdWithCookies = false;
                IsAuthenticated = false;
            }
        }
    }
}

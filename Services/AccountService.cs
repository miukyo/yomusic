using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using ytmusic_net;

namespace yomusic.Services
{
    public sealed class AccountService
    {
        private static readonly Lazy<AccountService> _instance = new(() => new AccountService());
        public static AccountService Instance => _instance.Value;

        private const string CookieFileName = "cookies.dat";
        private string? _currentCookies;
        private bool _loggedIn;

        public bool IsLoggedIn => _loggedIn;
        internal string? CurrentCookies => _currentCookies;
        public AccountInfo? AccountInfo { get; private set; }

        internal async Task<string?> GetCookiesAsync()
        {
            if (_currentCookies != null) return _currentCookies;
            var restored = await TryRestoreSessionAsync();
            return restored ? _currentCookies : null;
        }

        public event Action<bool>? LoginStateChanged;

        private AccountService() { }

        private static async Task<StorageFolder> GetAppDataFolderAsync()
        {
            var docFolder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(
                "YoMusic", CreationCollisionOption.OpenIfExists);
            return await docFolder.CreateFolderAsync("cache", CreationCollisionOption.OpenIfExists);
        }

        public async Task<bool> TryRestoreSessionAsync()
        {
            try
            {
                var folder = await GetAppDataFolderAsync();
                var file = await folder.TryGetItemAsync(CookieFileName);
                if (file == null) return false;

                var encryptedBytes = await FileIO.ReadBufferAsync((StorageFile)file);
                var provider = new DataProtectionProvider("LOCAL=user");
                var decryptedBuffer = await provider.UnprotectAsync(encryptedBytes);
                _currentCookies = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuffer);

                if (string.IsNullOrEmpty(_currentCookies)) return false;

                YTMusicClient.Reset();
                _loggedIn = true;
                LoginStateChanged?.Invoke(true);
                return true;
            }
            catch
            {
                _currentCookies = null;
                return false;
            }
        }

        public async Task<bool> LoginAsync(string cookies)
        {
            try
            {
                _currentCookies = cookies;
                _loggedIn = true;
                YTMusicClient.Reset();

                await SaveCookiesAsync(cookies);
                LoginStateChanged?.Invoke(true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task FetchAccountInfoAsync()
        {
            try
            {
                var client = await YTMusicClient.Client;
                AccountInfo = await client.GetAccountInfoAsync();
            }
            catch { }
        }

        public async Task LogoutAsync()
        {
            _currentCookies = null;
            _loggedIn = false;

            try
            {
                var folder = await GetAppDataFolderAsync();
                var file = await folder.TryGetItemAsync(CookieFileName);
                if (file != null)
                    await file.DeleteAsync();
            }
            catch { }

            YTMusicClient.Reset();
            LoginStateChanged?.Invoke(false);
        }

        private async Task SaveCookiesAsync(string cookies)
        {
            var provider = new DataProtectionProvider("LOCAL=user");
            var inputBuffer = CryptographicBuffer.ConvertStringToBinary(cookies, BinaryStringEncoding.Utf8);
            var encryptedBuffer = await provider.ProtectAsync(inputBuffer);

            var folder = await GetAppDataFolderAsync();
            var file = await folder.CreateFileAsync(
                CookieFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBufferAsync(file, encryptedBuffer);
        }
    }
}

using System;
using Windows.Storage;
using Windows.UI.Xaml;

namespace yomusic.Services
{
    public sealed class SettingsService
    {
        private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
        public static SettingsService Instance => _instance.Value;

        private readonly ApplicationDataContainer _local;

        private SettingsService()
        {
            _local = ApplicationData.Current.LocalSettings;
        }

        public ElementTheme Theme
        {
            get
            {
                var val = _local.Values["Theme"] as string;
                if (Enum.TryParse<ElementTheme>(val, out var t))
                    return t;
                return ElementTheme.Default;
            }
            set
            {
                _local.Values["Theme"] = value.ToString();
            }
        }

        public bool EnableAnimations
        {
            get => (bool)(_local.Values["EnableAnimations"] ?? true);
            set => _local.Values["EnableAnimations"] = value;
        }

        public int AudioQuality
        {
            get => (int)(_local.Values["AudioQuality"] ?? 2);
            set => _local.Values["AudioQuality"] = value;
        }

        public void ClearAll()
        {
            _local.Values.Clear();
        }
    }
}

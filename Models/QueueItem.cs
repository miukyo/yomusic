using System.Text.Json.Serialization;
using Windows.UI.Xaml.Media.Imaging;

namespace yomusic.Models
{
    public sealed class QueueItem
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        [JsonIgnore]
        public BitmapImage? Thumbnail { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string? Duration { get; set; }
    }
}
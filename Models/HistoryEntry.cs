using System;

namespace yomusic.Models
{
    public sealed class HistoryEntry
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string? Duration { get; set; }
        public DateTime PlayedAt { get; set; }
    }
}

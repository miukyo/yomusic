using System.Collections.Generic;

namespace yomusic.Models
{
    public sealed class SessionData
    {
        public List<QueueItem> Queue { get; set; } = new();
        public int CurrentIndex { get; set; }
        public double PositionSeconds { get; set; }
        public bool IsShuffled { get; set; }
        public int RepeatMode { get; set; }
        public double Volume { get; set; }
    }
}

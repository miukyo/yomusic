using System;
using System.Collections.Generic;

namespace yomusic.Models
{
    public enum LyricsType
    {
        None,
        Line
    }

    public sealed class LyricsResult
    {
        public string Source { get; set; } = "";
        public LyricsType Type { get; set; } = LyricsType.None;
        public List<LyricsLine> Lines { get; set; } = new();
    }

    public sealed class LyricsLine
    {
        public double Time { get; set; }
        public string Text { get; set; } = "";
    }
}

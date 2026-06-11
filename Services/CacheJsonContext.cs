using System.Collections.Generic;
using System.Text.Json.Serialization;
using yomusic.Models;

namespace yomusic.Services
{
    [JsonSerializable(typeof(SessionData))]
    [JsonSerializable(typeof(LyricsResult))]
    [JsonSerializable(typeof(LyricsLine))]
    [JsonSerializable(typeof(QueueItem))]
    [JsonSerializable(typeof(HistoryEntry))]
    [JsonSerializable(typeof(List<HistoryEntry>))]
    internal partial class CacheJsonContext : JsonSerializerContext
    {
    }
}

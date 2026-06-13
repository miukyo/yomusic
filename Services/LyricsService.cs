using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using yomusic.Models;

namespace yomusic.Services
{
    public static class LyricsService
    {
        private static readonly HttpClient _http = new();
        public static async Task<LyricsResult> FetchAsync(string title, string artist, string? videoId = null)
        {
            if (!string.IsNullOrEmpty(videoId))
            {
                var cached = await CacheService.LoadLyricsAsync(videoId);
                if (cached != null && cached.Lines.Count > 0)
                    return cached;
            }

            var result = await TryLrcLibAsync(title, artist);

            if (result != null && result.Lines.Count > 0 && !string.IsNullOrEmpty(videoId))
                await CacheService.SaveLyricsAsync(videoId, result);

            if (result != null && result.Lines.Count > 0)
                return result;

            return new LyricsResult { Type = LyricsType.None };
        }

        private static async Task<LyricsResult?> TryLrcLibAsync(string title, string artist)
        {
            try
            {
                var url = $"https://lrclib.net/api/get" +
                    $"?artist_name={Uri.EscapeDataString(artist)}" +
                    $"&track_name={Uri.EscapeDataString(title)}";
                var json = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("syncedLyrics", out var synced) &&
                    synced.ValueKind == JsonValueKind.String)
                {
                    var lrc = synced.GetString();
                    if (!string.IsNullOrEmpty(lrc))
                        return ParseLrc(lrc, "LRCLIB");
                }

                if (root.TryGetProperty("plainLyrics", out var plain) &&
                    plain.ValueKind == JsonValueKind.String)
                {
                    var text = plain.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var result = new LyricsResult
                        {
                            Source = "LRCLIB-Plain",
                            Type = LyricsType.Line,
                        };
                        foreach (var line in text.Split('\n'))
                        {
                            var trimmed = line.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                result.Lines.Add(new LyricsLine { Time = 0, Text = trimmed });
                            }
                        }
                        return result;
                    }
                }
            }
            catch { }
            return null;
        }

        private static LyricsResult ParseLrc(string lrc, string source)
        {
            var result = new LyricsResult
            {
                Source = source,
                Type = LyricsType.Synced,
            };
            var lineRegex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
            foreach (var line in lrc.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var match = lineRegex.Match(trimmed);
                if (match.Success)
                {
                    var minutes = int.Parse(match.Groups[1].Value);
                    var seconds = int.Parse(match.Groups[2].Value);
                    var millis = int.Parse(match.Groups[3].Value.PadRight(3, '0').Substring(0, 3));
                    var text = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        result.Lines.Add(new LyricsLine
                        {
                            Time = minutes * 60 + seconds + millis / 1000.0,
                            Text = text
                        });
                    }
                }
            }
            return result;
        }
    }
}
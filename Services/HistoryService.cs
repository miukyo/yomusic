using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using yomusic.Models;
using ytmusic_net;

namespace yomusic.Services
{
    public sealed class HistoryService
    {
        private static readonly Lazy<HistoryService> _instance = new(() => new HistoryService());
        public static HistoryService Instance => _instance.Value;

        private static readonly SemaphoreSlim _historyLock = new(1, 1);
        private const int MaxLocalEntries = 200;

        private HistoryService() { }

        public async Task RecordPlayAsync(QueueItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.VideoId)) return;

            var entry = new HistoryEntry
            {
                VideoId = item.VideoId,
                Title = item.Title,
                Artist = item.Artist,
                ThumbnailUrl = item.ThumbnailUrl,
                Duration = item.Duration,
                PlayedAt = DateTime.UtcNow
            };

            await SaveLocalEntryAsync(entry);

            if (YTMusicClient.IsAuthenticated)
            {
                _ = AddToApiHistoryAsync(item.VideoId);
            }
        }

        public async Task<List<HistoryEntry>> GetHistoryAsync()
        {
            if (YTMusicClient.IsAuthenticated)
            {
                try
                {
                    var client = await YTMusicClient.Client;
                    var apiHistory = await client.GetHistoryAsync();
                    return apiHistory.Select(h => new HistoryEntry
                    {
                        VideoId = h.VideoId,
                        Title = h.Title,
                        Artist = h.Artists != null ? string.Join(", ", h.Artists.Select(a => a.Name)) : "",
                        ThumbnailUrl = h.Thumbnails?.OrderByDescending(t => t.Width).FirstOrDefault()?.Url ?? "",
                        PlayedAt = DateTime.MinValue
                    }).ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GetHistoryAsync (API) failed: {ex.Message}");
                }
            }

            return await LoadLocalHistoryAsync();
        }

        private async Task AddToApiHistoryAsync(string videoId)
        {
            try
            {
                var client = await YTMusicClient.Client;
                var result = await client.AddHistoryItemAsync(videoId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddToApiHistoryAsync failed: {ex.Message}");
            }
        }

        private async Task SaveLocalEntryAsync(HistoryEntry entry)
        {
            await _historyLock.WaitAsync();
            try
            {
                var entries = await LoadLocalHistoryInternalAsync();
                entries.Insert(0, entry);
                if (entries.Count > MaxLocalEntries)
                    entries.RemoveRange(MaxLocalEntries, entries.Count - MaxLocalEntries);

                await WriteLocalHistoryAsync(entries);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveLocalEntryAsync failed: {ex.Message}");
            }
            finally
            {
                _historyLock.Release();
            }
        }

        private async Task<List<HistoryEntry>> LoadLocalHistoryAsync()
        {
            await _historyLock.WaitAsync();
            try
            {
                return await LoadLocalHistoryInternalAsync();
            }
            finally
            {
                _historyLock.Release();
            }
        }

        private async Task<List<HistoryEntry>> LoadLocalHistoryInternalAsync()
        {
            try
            {
                var folder = await EnsureHistoryFolderAsync();
                var item = await folder.TryGetItemAsync("history.json");
                if (item is StorageFile file)
                {
                    var props = await file.GetBasicPropertiesAsync();
                    if (props.Size == 0) return new List<HistoryEntry>();

                    var json = await FileIO.ReadTextAsync(file);
                    if (string.IsNullOrWhiteSpace(json)) return new List<HistoryEntry>();

                    return JsonSerializer.Deserialize(json, CacheJsonContext.Default.ListHistoryEntry) ?? new List<HistoryEntry>();
                }
            }
            catch (JsonException) { }
            catch (Exception ex) { Debug.WriteLine($"LoadLocalHistoryInternalAsync: {ex.Message}"); }
            return new List<HistoryEntry>();
        }

        private async Task WriteLocalHistoryAsync(List<HistoryEntry> entries)
        {
            try
            {
                var folder = await EnsureHistoryFolderAsync();
                var json = JsonSerializer.Serialize(entries, CacheJsonContext.Default.ListHistoryEntry);

                var tempFile = await folder.CreateFileAsync("history.tmp", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(tempFile, json);
                await tempFile.RenameAsync("history.json", NameCollisionOption.ReplaceExisting);
            }
            catch (Exception ex) { Debug.WriteLine($"WriteLocalHistoryAsync: {ex.Message}"); }
        }

        private static async Task<StorageFolder> EnsureHistoryFolderAsync()
        {
            var docFolder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(
                "YoMusic", CreationCollisionOption.OpenIfExists);
            return await docFolder.CreateFolderAsync("cache", CreationCollisionOption.OpenIfExists);
        }
    }
}

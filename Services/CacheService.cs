using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using yomusic.Models;

namespace yomusic.Services
{
    public static class CacheService
    {
        private static readonly SemaphoreSlim _sessionLock = new(1, 1);

        private static async Task<StorageFolder> EnsureCacheFolderAsync()
        {
            var docFolder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(
                "YoMusic", CreationCollisionOption.OpenIfExists);
            return await docFolder.CreateFolderAsync("cache", CreationCollisionOption.OpenIfExists);
        }

        public static async Task<SessionData?> LoadSessionAsync()
        {
            await _sessionLock.WaitAsync();
            try
            {
                var folder = await EnsureCacheFolderAsync();

                // Clean up stale temp files from previous interrupted saves
                var stale = await folder.TryGetItemAsync("session.tmp");
                if (stale is StorageFile staleFile)
                    await staleFile.DeleteAsync();

                var item = await folder.TryGetItemAsync("session.json");
                if (item is StorageFile file)
                {
                    var props = await file.GetBasicPropertiesAsync();
                    if (props.Size == 0) return null;

                    var json = await FileIO.ReadTextAsync(file);
                    if (string.IsNullOrWhiteSpace(json)) return null;

                    Debug.WriteLine(json);

                    return JsonSerializer.Deserialize(json, CacheJsonContext.Default.SessionData);
                }
            }
            catch (JsonException)
            {
                // Corrupt cache file - delete so it self-heals
                try
                {
                    var folder = await EnsureCacheFolderAsync();
                    var item = await folder.TryGetItemAsync("session.json");
                    if (item is StorageFile file)
                        await file.DeleteAsync();
                }
                catch { }
            }
            catch { }
            finally
            {
                _sessionLock.Release();
            }
            return null;
        }

        public static async Task SaveSessionAsync(SessionData session)
        {
            await _sessionLock.WaitAsync();
            try
            {
                var folder = await EnsureCacheFolderAsync();
                var json = JsonSerializer.Serialize(session, CacheJsonContext.Default.SessionData);

                var tempFile = await folder.CreateFileAsync("session.tmp", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(tempFile, json);
                await tempFile.RenameAsync("session.json", NameCollisionOption.ReplaceExisting);
            }
            catch { }
            finally
            {
                _sessionLock.Release();
            }
        }

        public static async Task<LyricsResult?> LoadLyricsAsync(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;
            try
            {
                var folder = await EnsureCacheFolderAsync();
                var lyricsFolder = await folder.CreateFolderAsync("lyrics", CreationCollisionOption.OpenIfExists);
                var sanitized = SanitizeFileName(videoId);
                var item = await lyricsFolder.TryGetItemAsync($"{sanitized}.json");
                if (item is StorageFile file)
                {
                    var json = await FileIO.ReadTextAsync(file);
                    if (string.IsNullOrWhiteSpace(json)) return null;
                    return JsonSerializer.Deserialize(json, CacheJsonContext.Default.LyricsResult);
                }
            }
            catch (JsonException) { }
            catch (Exception) { }
            return null;
        }

        public static async Task SaveLyricsAsync(string videoId, LyricsResult lyrics)
        {
            if (string.IsNullOrEmpty(videoId) || lyrics == null) return;
            try
            {
                var folder = await EnsureCacheFolderAsync();
                var lyricsFolder = await folder.CreateFolderAsync("lyrics", CreationCollisionOption.OpenIfExists);
                var sanitized = SanitizeFileName(videoId);
                var json = JsonSerializer.Serialize(lyrics, CacheJsonContext.Default.LyricsResult);

                var tempFile = await lyricsFolder.CreateFileAsync($"{sanitized}.tmp", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(tempFile, json);
                await tempFile.RenameAsync($"{sanitized}.json", NameCollisionOption.ReplaceExisting);
            }
            catch { }
        }

        public static async Task ClearAllAsync()
        {
            await _sessionLock.WaitAsync();
            try
            {
                var folder = await EnsureCacheFolderAsync();
                await folder.DeleteAsync();
            }
            catch { }
            finally
            {
                _sessionLock.Release();
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}

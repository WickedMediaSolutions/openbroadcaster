using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenBroadcaster.Core.Messaging;
using Avalonia.Media.Imaging;
using System.IO;
using TagLib;
using SixLabors.ImageSharp;
using OpenBroadcaster.Core.Messaging.Events;
using OpenBroadcaster.Core.Models;
using Avalonia.Threading;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace OpenBroadcaster.Avalonia.ViewModels
{
    public class DeckViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DeckIdentifier _deckId;
        private readonly OpenBroadcaster.Core.Models.AppSettings? _appSettings;
        private readonly IEventBus _eventBus;
        private IDisposable? _subscription;
        private string _title = string.Empty;
        private string _artist = string.Empty;
        private string _elapsed = "00:00";
        private string _remaining = "00:00";
        private Bitmap? _albumArt;
        private static readonly object _artCacheLock = new object();
        private static readonly System.Collections.Generic.Dictionary<string, Bitmap?> _artCache = new System.Collections.Generic.Dictionary<string, Bitmap?>();
        private static readonly System.Collections.Generic.HashSet<string> _artLoading = new System.Collections.Generic.HashSet<string>();

        public DeckViewModel(DeckIdentifier deckId, OpenBroadcaster.Core.Services.TransportService transportService, IEventBus eventBus, OpenBroadcaster.Core.Models.AppSettings? appSettings = null)
        {
            _deckId = deckId;
            _appSettings = appSettings;
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            // Seed from current transport deck state if available
            var deck = transportService == null ? null : (deckId == DeckIdentifier.A ? transportService.DeckA : transportService.DeckB);
            if (deck?.CurrentQueueItem?.Track != null)
            {
                _title = deck.CurrentQueueItem.Track.Title;
                _artist = deck.CurrentQueueItem.Track.Artist;
                _elapsed = FormatTime(deck.Elapsed);
                _remaining = FormatTime(deck.Remaining);
                _albumArt = TryLoadAlbumArt(deck.CurrentQueueItem.Track.FilePath);
            }

            _subscription = _eventBus.Subscribe<DeckStateChangedEvent>(OnDeckStateChanged);
        }

        public string Title { get => _title; private set { if (_title != value) { _title = value; OnPropertyChanged(); } } }
        public string Artist { get => _artist; private set { if (_artist != value) { _artist = value; OnPropertyChanged(); } } }
        public string ElapsedTime { get => _elapsed; private set { if (_elapsed != value) { _elapsed = value; OnPropertyChanged(); } } }
        public string RemainingTime { get => _remaining; private set { if (_remaining != value) { _remaining = value; OnPropertyChanged(); } } }

        private void OnDeckStateChanged(DeckStateChangedEvent ev)
        {
            if (ev.DeckId != _deckId) return;

            // Ensure UI thread updates, but perform heavy I/O/network work off the UI thread.
            try
            {
                LogDebug($"OnDeckStateChanged: received for deck {_deckId} - QueueItem present={(ev.QueueItem != null)}");

                if (ev.QueueItem?.Track != null)
                {
                    // update simple metadata on UI thread synchronously
                    Dispatcher.UIThread.Post(() =>
                    {
                        Title = ev.QueueItem.Track.Title;
                        Artist = ev.QueueItem.Track.Artist;
                        ElapsedTime = FormatTime(ev.Elapsed);
                        RemainingTime = FormatTime(ev.Remaining);
                    });

                    // load album art on background thread then push result to UI thread
                    var filePath = ev.QueueItem.Track.FilePath;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            LogDebug($"OnDeckStateChanged: starting art load for '{filePath}' (Title='{ev.QueueItem.Track.Title}', Artist='{ev.QueueItem.Track.Artist}')");
                            var bmp = TryLoadAlbumArt(filePath);
                            LogDebug($"OnDeckStateChanged: art load completed for '{filePath}' - found={(bmp != null)}");

                            // Construct Avalonia Bitmap on UI thread by loading from cached file when possible.
                            Dispatcher.UIThread.Post(() =>
                            {
                                try
                                {
                                    if (bmp != null)
                                    {
                                        try
                                        {
                                            // Attempt to locate cached file written by DownloadAndCacheImage
                                            var cacheDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "album-art");
                                            string Sanitize(string s)
                                            {
                                                var invalid = System.IO.Path.GetInvalidFileNameChars();
                                                foreach (var c in invalid) s = s.Replace(c, '_');
                                                return s;
                                            }

                                            var pref = Sanitize(ev.QueueItem.Track.Artist ?? "unknown") + "_" + Sanitize(ev.QueueItem.Track.Title ?? "unknown");
                                            string found = null;
                                            try
                                            {
                                                if (System.IO.Directory.Exists(cacheDir))
                                                {
                                                    var files = System.IO.Directory.GetFiles(cacheDir);
                                                    foreach (var f in files)
                                                    {
                                                        var name = System.IO.Path.GetFileNameWithoutExtension(f);
                                                        if (name != null && name.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            found = f;
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            catch { }

                                            if (!string.IsNullOrWhiteSpace(found) && System.IO.File.Exists(found))
                                            {
                                                try
                                                {
                                                    AlbumArt = new Bitmap(found);
                                                    return;
                                                }
                                                catch (Exception exBmp)
                                                {
                                                    LogDebug($"OnDeckStateChanged: failed to create UI Bitmap from cached file '{found}': {exBmp.Message}");
                                                }
                                            }
                                        }
                                        catch (Exception exLocate)
                                        {
                                            LogDebug($"OnDeckStateChanged: cache locate error: {exLocate.Message}");
                                        }

                                        // Fallback: use the Bitmap returned by TryLoadAlbumArt if UI construction from file failed
                                        AlbumArt = bmp;
                                    }
                                    else
                                    {
                                        AlbumArt = null;
                                    }
                                }
                                catch (Exception exUi)
                                {
                                    LogDebug($"OnDeckStateChanged: UI thread album assign error: {exUi.Message}");
                                }
                            });
                        }
                        catch (Exception exBg)
                        {
                            LogDebug($"OnDeckStateChanged: background art load error for '{filePath}': {exBg.Message}");
                        }
                    });
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Title = string.Empty;
                        Artist = string.Empty;
                        AlbumArt = null;
                        ElapsedTime = FormatTime(ev.Elapsed);
                        RemainingTime = FormatTime(ev.Remaining);
                    });
                }
            }
            catch (Exception exOuter)
            {
                LogDebug($"OnDeckStateChanged: unexpected error: {exOuter.Message}");
            }
        }

        public Bitmap? AlbumArt
        {
            get => _albumArt;
            private set
            {
                if (!object.Equals(_albumArt, value))
                {
                    _albumArt = value;
                    try { LogDebug($"AlbumArt.setter: deck {_deckId} updated AlbumArt present={(value != null)} on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}"); } catch { }
                    OnPropertyChanged();
                }
            }
        }

        private Bitmap? TryLoadAlbumArt(string? path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    lock (_artCacheLock)
                    {
                        if (_artCache.TryGetValue(path, out var cached))
                        {
                            return cached;
                        }
                        if (_artLoading.Contains(path))
                        {
                            // Another thread is already loading this path; avoid duplicate work
                            return null;
                        }
                        _artLoading.Add(path);
                    }
                }

                try
                {
                    // Primary: try Last.fm lookup using known Title/Artist from the viewmodel
                    string lookupTitle = Title ?? string.Empty;
                    string lookupArtist = Artist ?? string.Empty;

                    // If title/artist are missing, try to read them from tags so we can perform an iTunes lookup.
                    TagLib.File? tagFile = null;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                        {
                            try
                            {
                                tagFile = TagLib.File.Create(path);
                                if (string.IsNullOrWhiteSpace(lookupTitle) && !string.IsNullOrWhiteSpace(tagFile.Tag.Title)) lookupTitle = tagFile.Tag.Title;
                                if (string.IsNullOrWhiteSpace(lookupArtist) && tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0)
                                {
                                    var a = tagFile.Tag.Performers[0];
                                    if (!string.IsNullOrWhiteSpace(a)) lookupArtist = a;
                                }
                            }
                            catch (Exception exTagPref) { LogDebug($"TryLoadAlbumArt: pre-read TagLib failed for lookup: {exTagPref.Message}"); }
                        }

                        // Try iTunes (no API key) first when we have lookup data
                        if (!string.IsNullOrWhiteSpace(lookupTitle) && !string.IsNullOrWhiteSpace(lookupArtist))
                        {
                            try
                            {
                                var itunesBmp = TryFetchItunes(lookupTitle, lookupArtist, path);
                                if (itunesBmp != null)
                                {
                                    lock (_artCacheLock) { if (!string.IsNullOrWhiteSpace(path)) _artCache[path] = itunesBmp; }
                                    return itunesBmp;
                                }
                            }
                            catch (Exception exIt)
                            {
                                LogDebug($"TryLoadAlbumArt: iTunes lookup failed: {exIt.Message}");
                            }

                            // Fallback to Last.fm if available
                            try
                            {
                                var lastBmp = TryFetchLastFm(lookupTitle, lookupArtist, path);
                                if (lastBmp != null)
                                {
                                    lock (_artCacheLock) { if (!string.IsNullOrWhiteSpace(path)) _artCache[path] = lastBmp; }
                                    return lastBmp;
                                }
                            }
                            catch (Exception exLf)
                            {
                                LogDebug($"TryLoadAlbumArt: Last.fm primary lookup failed: {exLf.Message}");
                            }
                        }
                    }
                    catch (Exception exLfgen)
                    {
                        LogDebug($"TryLoadAlbumArt: primary lookup unexpected error: {exLfgen.Message}");
                    }

                    if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                        goto search_candidates;

                try
                {
                    // Read tag data (use tagFile if previously read for lookup)
                    var tfile = tagFile ?? TagLib.File.Create(path);
                    var pics = tfile?.Tag?.Pictures;
                    if (pics != null && pics.Length > 0)
                    {
                        foreach (var pic in pics)
                        {
                            var data = pic.Data?.Data;
                            if (data == null || data.Length == 0) continue;

                            // Choose extension from mime-type when available
                            var ext = "jpg";
                            try
                            {
                                var mt = pic.MimeType;
                                if (!string.IsNullOrWhiteSpace(mt))
                                {
                                    if (mt.Contains("png", StringComparison.OrdinalIgnoreCase)) ext = "png";
                                    else if (mt.Contains("gif", StringComparison.OrdinalIgnoreCase)) ext = "gif";
                                    else if (mt.Contains("bmp", StringComparison.OrdinalIgnoreCase)) ext = "bmp";
                                    else if (mt.Contains("webp", StringComparison.OrdinalIgnoreCase)) ext = "webp";
                                }
                            }
                            catch { }

                            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ob_art_{Guid.NewGuid():N}.{ext}");
                            try
                            {
                                System.IO.File.WriteAllBytes(tmp, data);
                                LogDebug($"TryLoadAlbumArt: wrote temp file '{tmp}' for '{path}'");
                                try
                                {
                                    if (!LooksLikeImage(data))
                                    {
                                        LogDebug($"TryLoadAlbumArt: temp data does not look like an image for '{path}', skipping Bitmap load");
                                    }
                                    else
                                    {
                                        var bmp = new Bitmap(tmp);
                                        LogDebug($"TryLoadAlbumArt: loaded Bitmap from temp file '{tmp}' for '{path}'");
                                        lock (_artCacheLock) { _artCache[path] = bmp; }
                                        return bmp;
                                    }
                                }
                                catch (Exception exFile)
                                {
                                    LogDebug($"TryLoadAlbumArt: failed to load Bitmap from temp file '{tmp}': {exFile.Message}");
                                    // Try to normalize with ImageSharp and re-encode as PNG
                                    try
                                    {
                                        using (var image = SixLabors.ImageSharp.Image.Load(data))
                                        {
                                            var normalized = System.IO.Path.ChangeExtension(tmp, ".png");
                                            image.SaveAsPng(normalized);
                                            LogDebug($"TryLoadAlbumArt: wrote normalized PNG '{normalized}' ({new System.IO.FileInfo(normalized).Length} bytes)");
                                            try
                                            {
                                                var bmp2 = new Bitmap(normalized);
                                                LogDebug($"TryLoadAlbumArt: loaded Bitmap from normalized PNG '{normalized}' for '{path}'");
                                                lock (_artCacheLock) { _artCache[path] = bmp2; }
                                                return bmp2;
                                            }
                                            catch (Exception ex2)
                                            {
                                                LogDebug($"TryLoadAlbumArt: failed to load bitmap from normalized PNG '{normalized}': {ex2.Message}");
                                            }
                                        }
                                    }
                                    catch (Exception imgEx)
                                    {
                                        LogDebug($"TryLoadAlbumArt: ImageSharp normalization failed for '{path}': {imgEx.Message}");
                                    }

                                    // Fall back to stream-based Bitmap creation
                                    try
                                    {
                                        if (!LooksLikeImage(data))
                                        {
                                            LogDebug($"TryLoadAlbumArt: TagLib data does not look like an image for '{path}', skipping stream load");
                                        }
                                        else
                                        {
                                            using var ms = new MemoryStream(data);
                                            var bmpStream = new Bitmap(ms);
                                            lock (_artCacheLock) { _artCache[path] = bmpStream; }
                                            return bmpStream;
                                        }
                                    }
                                    catch (Exception exStream)
                                    {
                                        LogDebug($"TryLoadAlbumArt: Bitmap creation failed from TagLib stream for '{path}': {exStream.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"TryLoadAlbumArt: temp-file write failed for '{path}': {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryLoadAlbumArt: TagLib read failed for '{path}': {ex.Message}");
                }

            search_candidates:
                // Fallback: attempt to find common image files in the same folder
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(path ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(dir) && System.IO.Directory.Exists(dir))
                    {
                        var candidates = new[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png", "album.jpg", "album.png" };
                        foreach (var c in candidates)
                        {
                            var fp = System.IO.Path.Combine(dir, c);
                            if (System.IO.File.Exists(fp))
                            {
                                try
                                {
                                    LogDebug($"TryLoadAlbumArt: trying candidate file '{fp}'");
                                    var cand = new Bitmap(fp);
                                    lock (_artCacheLock) { if (!string.IsNullOrWhiteSpace(path)) _artCache[path] = cand; }
                                    return cand;
                                }
                                catch (Exception ex)
                                {
                                    LogDebug($"TryLoadAlbumArt: failed loading candidate '{fp}': {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryLoadAlbumArt: candidate search failed for '{path}': {ex.Message}");
                }

                // If we didn't find an embedded or candidate image, try Last.fm lookup (title + artist).
                try
                {
                    var title = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                    // attempt to extract title/artist heuristically if TagLib read failed earlier
                    // prefer using provided metadata from tfile when available; as a simple fallback, split filename
                    var parts = title.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                    var artistGuess = parts.Length > 1 ? parts[0] : string.Empty;
                    var lastfmBmp = TryFetchLastFm(Title ?? title, Artist ?? artistGuess, path);
                    if (lastfmBmp != null)
                    {
                        lock (_artCacheLock) { if (!string.IsNullOrWhiteSpace(path)) _artCache[path] = lastfmBmp; }
                        return lastfmBmp;
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryLoadAlbumArt: Last.fm lookup failed for '{path}': {ex.Message}");
                }

                // Do not attempt to load the audio file itself as an image — record null and return.
                if (!string.IsNullOrWhiteSpace(path)) lock (_artCacheLock) { _artCache[path] = null; }
                return null;
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        lock (_artCacheLock)
                        {
                            _artLoading.Remove(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"TryLoadAlbumArt: unexpected error for '{path}': {ex.Message}");
                if (!string.IsNullOrWhiteSpace(path)) lock (_artCacheLock) { _artCache[path] = null; }
                return null;
            }
        }

        private static bool LooksLikeImage(byte[] data)
        {
            if (data == null || data.Length < 4) return false;
            // JPEG
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8) return true;
            // PNG
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return true;
            // GIF
            if (data.Length >= 3 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return true;
            // BMP 'B' 'M'
            if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D) return true;
            // WEBP: 'R' 'I' 'F' 'F' ... 'W' 'E' 'B' 'P'
            if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50) return true;
            return false;
        }

        private static void LogDebug(string msg)
        {
            try
            {
                var line = $"[DIAG] {DateTime.UtcNow:O} {msg}\n";
                try
                {
                    var cacheDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "logs");
                    System.IO.Directory.CreateDirectory(cacheDir);
                    var fp = System.IO.Path.Combine(cacheDir, "avalonia-art.log");
                    using (var fs = new System.IO.FileStream(fp, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                    {
                        var bytes = Encoding.UTF8.GetBytes(line);
                        fs.Write(bytes, 0, bytes.Length);
                    }
                }
                catch
                {
                    // fallback to current directory
                    try
                    {
                        using (var fs = new System.IO.FileStream("avalonia-art.log", System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                        {
                            var bytes = Encoding.UTF8.GetBytes(line);
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Attempt to fetch artwork from Last.fm using track title + artist.
        private Bitmap? TryFetchLastFm(string title, string artist, string? path)
        {
            try
            {
                var apiKey = _appSettings?.LastFm?.ApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    // Fallback to environment variable if settings not configured
                    apiKey = Environment.GetEnvironmentVariable("LASTFM_API_KEY");
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    LogDebug($"TryFetchLastFm: no Last.fm API key configured, skipping Last.fm lookup for '{title}' / '{artist}'");
                    return null;
                }

                using var client = new HttpClient();

                string UrlEncode(string s) => System.Net.WebUtility.UrlEncode(s ?? string.Empty);

                // 1) Try track.getInfo
                try
                {
                    var trackUrl = $"https://ws.audioscrobbler.com/2.0/?method=track.getInfo&api_key={UrlEncode(apiKey)}&artist={UrlEncode(artist)}&track={UrlEncode(title)}&format=json";
                    var resp = client.GetStringAsync(trackUrl).Result;
                    using var doc = JsonDocument.Parse(resp);
                    if (doc.RootElement.TryGetProperty("track", out var trackEl))
                    {
                        if (trackEl.TryGetProperty("album", out var albumEl) && albumEl.TryGetProperty("image", out var imgArr) && imgArr.ValueKind == JsonValueKind.Array)
                        {
                            for (int i = imgArr.GetArrayLength() - 1; i >= 0; i--)
                            {
                                var img = imgArr[i];
                                if (img.TryGetProperty("#text", out var text) && text.ValueKind == JsonValueKind.String)
                                {
                                    var url = text.GetString();
                                    if (!string.IsNullOrWhiteSpace(url))
                                    {
                                        var bmp = DownloadAndCacheImage(client, url, title, artist);
                                        if (bmp != null) return bmp;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryFetchLastFm: track.getInfo lookup failed for '{title}'/'{artist}': {ex.Message}");
                }

                // 2) Try artist.getInfo as fallback
                try
                {
                    var artistUrl = $"https://ws.audioscrobbler.com/2.0/?method=artist.getInfo&api_key={UrlEncode(apiKey)}&artist={UrlEncode(artist)}&format=json";
                    var resp2 = client.GetStringAsync(artistUrl).Result;
                    using var doc2 = JsonDocument.Parse(resp2);
                    if (doc2.RootElement.TryGetProperty("artist", out var artistEl) && artistEl.TryGetProperty("image", out var imgArr2) && imgArr2.ValueKind == JsonValueKind.Array)
                    {
                        for (int i = imgArr2.GetArrayLength() - 1; i >= 0; i--)
                        {
                            var img = imgArr2[i];
                            if (img.TryGetProperty("#text", out var text) && text.ValueKind == JsonValueKind.String)
                            {
                                var url = text.GetString();
                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    var bmp = DownloadAndCacheImage(client, url, title, artist);
                                    if (bmp != null) return bmp;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryFetchLastFm: artist.getInfo lookup failed for '{artist}': {ex.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"TryFetchLastFm: unexpected error for '{title}'/'{artist}': {ex.Message}");
                return null;
            }
        }

        // Attempt to fetch artwork from iTunes Search API (no API key required).
        private static Bitmap? TryFetchItunes(string title, string artist, string? path)
        {
            try
            {
                using var client = new HttpClient();

                string UrlEncode(string s) => System.Net.WebUtility.UrlEncode(s ?? string.Empty);

                // Search for song first
                try
                {
                    var q = UrlEncode($"{artist} {title}");
                    var url = $"https://itunes.apple.com/search?term={q}&entity=song&limit=1";
                    var resp = client.GetStringAsync(url).Result;
                    using var doc = JsonDocument.Parse(resp);
                    if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
                    {
                        var first = results[0];
                        if (first.TryGetProperty("artworkUrl100", out var aw) && aw.ValueKind == JsonValueKind.String)
                        {
                            var u = aw.GetString();
                            if (!string.IsNullOrWhiteSpace(u))
                            {
                                // request a larger image when available
                                var large = u.Replace("100x100", "600x600");
                                var bmp = DownloadAndCacheImage(client, large, title, artist);
                                if (bmp != null) return bmp;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryFetchItunes: song search failed for '{title}'/'{artist}': {ex.Message}");
                }

                // Try album search next
                try
                {
                    var q2 = UrlEncode($"{artist} {title}");
                    var url2 = $"https://itunes.apple.com/search?term={q2}&entity=album&limit=1";
                    var resp2 = client.GetStringAsync(url2).Result;
                    using var doc2 = JsonDocument.Parse(resp2);
                    if (doc2.RootElement.TryGetProperty("results", out var results2) && results2.ValueKind == JsonValueKind.Array && results2.GetArrayLength() > 0)
                    {
                        var first = results2[0];
                        if (first.TryGetProperty("artworkUrl100", out var aw2) && aw2.ValueKind == JsonValueKind.String)
                        {
                            var u2 = aw2.GetString();
                            if (!string.IsNullOrWhiteSpace(u2))
                            {
                                var large2 = u2.Replace("100x100", "600x600");
                                var bmp2 = DownloadAndCacheImage(client, large2, title, artist);
                                if (bmp2 != null) return bmp2;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"TryFetchItunes: album search failed for '{title}'/'{artist}': {ex.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"TryFetchItunes: unexpected error for '{title}'/'{artist}': {ex.Message}");
                return null;
            }
        }

        private static Bitmap? DownloadAndCacheImage(HttpClient client, string url, string title, string artist)
        {
            try
            {
                var bytes = client.GetByteArrayAsync(url).Result;
                if (bytes == null || bytes.Length == 0) return null;
                if (!LooksLikeImage(bytes))
                {
                    LogDebug($"DownloadAndCacheImage: downloaded data from {url} does not look like an image");
                    return null;
                }

                var cacheDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "album-art");
                System.IO.Directory.CreateDirectory(cacheDir);
                string Sanitize(string s)
                {
                    var invalid = System.IO.Path.GetInvalidFileNameChars();
                    foreach (var c in invalid) s = s.Replace(c, '_');
                    return s;
                }
                var fname = Sanitize(artist ?? "unknown") + "_" + Sanitize(title ?? "unknown") + System.IO.Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrWhiteSpace(System.IO.Path.GetExtension(fname))) fname = fname + ".jpg";
                var fp = System.IO.Path.Combine(cacheDir, fname);
                try { System.IO.File.WriteAllBytes(fp, bytes); } catch { }

                try
                {
                    var bmp = new Bitmap(fp);
                    LogDebug($"DownloadAndCacheImage: saved and loaded cached art '{fp}' for '{artist}'/'{title}'");
                    return bmp;
                }
                catch (Exception ex)
                {
                    LogDebug($"DownloadAndCacheImage: failed to load cached image '{fp}': {ex.Message}");
                    // try load from stream
                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        return new Bitmap(ms);
                    }
                    catch (Exception ex2)
                    {
                        LogDebug($"DownloadAndCacheImage: failed to create Bitmap from stream for '{url}': {ex2.Message}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"DownloadAndCacheImage: error downloading {url}: {ex.Message}");
                return null;
            }
        }

        private static string FormatTime(TimeSpan ts)
        {
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;

namespace OpenBroadcaster.Avalonia.Services
{
    public static class AlbumArtService
    {
        private static bool LooksLikeImage(byte[] data)
        {
            if (data == null || data.Length < 4) return false;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8) return true;
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return true;
            if (data.Length >= 3 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return true;
            if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D) return true;
            if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50) return true;
            return false;
        }

        private static Bitmap? DownloadAndCacheImage(HttpClient client, string url, string title, string artist)
        {
            try
            {
                var bytes = client.GetByteArrayAsync(url).Result;
                if (bytes == null || bytes.Length == 0) return null;
                if (!LooksLikeImage(bytes)) return null;

                var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "album-art");
                Directory.CreateDirectory(cacheDir);
                string Sanitize(string s)
                {
                    var invalid = Path.GetInvalidFileNameChars();
                    foreach (var c in invalid) s = s.Replace(c, '_');
                    return s;
                }
                var fname = Sanitize(artist ?? "unknown") + "_" + Sanitize(title ?? "unknown") + Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrWhiteSpace(Path.GetExtension(fname))) fname = fname + ".jpg";
                var fp = Path.Combine(cacheDir, fname);
                try { File.WriteAllBytes(fp, bytes); } catch { }

                try
                {
                    return new Bitmap(fp);
                }
                catch
                {
                    try { using var ms = new MemoryStream(bytes); return new Bitmap(ms); } catch { return null; }
                }
            }
            catch { return null; }
        }

        private static Bitmap? TryFetchItunesLocal(string title, string artist)
        {
            try
            {
                using var client = new HttpClient();
                string UrlEncode(string s) => System.Net.WebUtility.UrlEncode(s ?? string.Empty);
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
                            var large = u.Replace("100x100", "600x600");
                            var bmp = DownloadAndCacheImage(client, large, title, artist);
                            if (bmp != null) return bmp;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static Bitmap? FetchArtFor(string? path, string? title, string? artist)
        {
            try
            {
                string lookupTitle = title ?? string.Empty;
                string lookupArtist = artist ?? string.Empty;
                TagLib.File? tagFile = null;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try { tagFile = TagLib.File.Create(path); }
                    catch { tagFile = null; }
                    if (tagFile != null)
                    {
                        if (string.IsNullOrWhiteSpace(lookupTitle) && !string.IsNullOrWhiteSpace(tagFile.Tag.Title)) lookupTitle = tagFile.Tag.Title;
                        if (string.IsNullOrWhiteSpace(lookupArtist) && tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0) lookupArtist = tagFile.Tag.Performers[0];
                    }
                }

                if (!string.IsNullOrWhiteSpace(lookupTitle) && !string.IsNullOrWhiteSpace(lookupArtist))
                {
                    var it = TryFetchItunesLocal(lookupTitle, lookupArtist);
                    if (it != null) return it;
                }

                // Try embedded pictures
                try
                {
                    var t = tagFile ?? (string.IsNullOrWhiteSpace(path) ? null : TagLib.File.Create(path));
                    var pics = t?.Tag?.Pictures;
                    if (pics != null && pics.Length > 0)
                    {
                        foreach (var pic in pics)
                        {
                            var data = pic.Data?.Data;
                            if (data == null || data.Length == 0) continue;
                            try
                            {
                                using var ms = new MemoryStream(data);
                                return new Bitmap(ms);
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // Candidate files in same folder
                try
                {
                    var dir = Path.GetDirectoryName(path ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        var candidates = new[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png", "album.jpg", "album.png" };
                        foreach (var c in candidates)
                        {
                            var fp = Path.Combine(dir, c);
                            if (File.Exists(fp))
                            {
                                try { return new Bitmap(fp); } catch { }
                            }
                        }
                    }
                }
                catch { }

                return null;
            }
            catch { return null; }
        }
    }
}

using System;
using TagLib;

namespace OpenBroadcaster.Core.Audio
{
    public sealed class TagLibMetadataReader : IAudioMetadataReader
    {
        public LibraryTrackMetadata ReadMetadata(string filePath)
        {
            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var tag = tagFile.Tag;
                var properties = tagFile.Properties;

                var title = string.IsNullOrWhiteSpace(tag?.Title)
                    ? System.IO.Path.GetFileNameWithoutExtension(filePath)
                    : tag!.Title;
                var artist = tag?.FirstPerformer;
                if (string.IsNullOrWhiteSpace(artist))
                {
                    var performers = tag?.Performers ?? Array.Empty<string>();
                    artist = performers.Length > 0
                        ? string.Join(", ", performers)
                        : "Unknown Artist";
                }
                var album = tag?.Album ?? string.Empty;
                var genre = tag?.FirstGenre ?? string.Empty;
                var year = (int)(tag?.Year ?? 0);
                var duration = properties?.Duration ?? TimeSpan.Zero;

                return new LibraryTrackMetadata(title, artist, album, genre, year, duration);
            }
            catch
            {
                var fallbackTitle = System.IO.Path.GetFileNameWithoutExtension(filePath);
                return new LibraryTrackMetadata(fallbackTitle, "Unknown Artist", string.Empty, string.Empty, DateTime.UtcNow.Year, TimeSpan.Zero);
            }
        }
    }
}

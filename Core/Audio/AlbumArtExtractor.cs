using System;
using System.IO;
using Avalonia.Media.Imaging;
using TagLib;

namespace OpenBroadcaster.Core.Audio
{
    /// <summary>
    /// Extracts album artwork from audio files using embedded ID3 tags.
    /// </summary>
    public static class AlbumArtExtractor
    {
        /// <summary>
        /// Extracts album art from the specified audio file.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <returns>A BitmapImage of the album art, or null if none found.</returns>
        public static Bitmap? ExtractAlbumArt(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                return null;
            }

            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var pictures = tagFile.Tag?.Pictures;

                if (pictures == null || pictures.Length == 0)
                {
                    return null;
                }

                // Prefer front cover, fall back to first available
                IPicture? picture = null;
                foreach (var pic in pictures)
                {
                    if (pic.Type == PictureType.FrontCover)
                    {
                        picture = pic;
                        break;
                    }
                    picture ??= pic;
                }

                if (picture?.Data?.Data == null || picture.Data.Data.Length == 0)
                {
                    return null;
                }

                using var stream = new MemoryStream(picture.Data.Data);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }
}

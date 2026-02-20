using System.IO;
using System.Windows.Media.Imaging;

namespace Spotify
{
    public class SongItem
    {
        public string Name { get; set; } = string.Empty;
        public BitmapImage? CoverImage { get; set; }

        public static BitmapImage? LoadCover(string songName)
        {
            string[] searchFolders =
            [
                Path.Combine(AppContext.BaseDirectory, "Music", "AlbumCovers"),
                Path.Combine(AppContext.BaseDirectory, "AlbumCovers"),
            ];

            string[] extensions = [".avif", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];
            string? coverPath = null;

            foreach (string folder in searchFolders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (string ext in extensions)
                {
                    string candidate = Path.Combine(folder, songName + ext);
                    if (File.Exists(candidate))
                    {
                        coverPath = candidate;
                        break;
                    }
                }
                if (coverPath != null) break;

                try
                {
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        if (string.Equals(fileNameWithoutExt, songName, StringComparison.OrdinalIgnoreCase))
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (Array.Exists(extensions, e => e == ext))
                            {
                                coverPath = file;
                                break;
                            }
                        }
                    }
                }
                catch { /* ignore access errors */ }

                if (coverPath != null) break;
            }

            if (coverPath != null)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(coverPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 72;
                    bitmap.DecodePixelHeight = 72;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}

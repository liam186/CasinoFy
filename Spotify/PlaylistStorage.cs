using System.IO;
using System.Text.Json;

namespace Spotify
{
    public static class PlaylistStorage
    {
        private static readonly string _filePath = Path.Combine(
            AppContext.BaseDirectory, "playlist.json");

        /// <summary>
        /// Saves the list of song file paths to a JSON file.
        /// </summary>
        public static void Save(List<string> paths)
        {
            try
            {
                string json = JsonSerializer.Serialize(paths, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Silently ignore save errors
            }
        }

        /// <summary>
        /// Loads the list of song file paths from the JSON file.
        /// Returns null if the file doesn't exist or can't be read.
        /// </summary>
        public static List<string>? Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return null;

                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<string>>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}

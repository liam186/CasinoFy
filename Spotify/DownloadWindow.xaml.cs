using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Spotify
{
    public partial class DownloadWindow : Window
    {
        private readonly string _downloadFolder;
        private readonly string _coverFolder;
        private readonly string _ffmpegFolder;
        private readonly List<string> _downloadedPaths = [];
        private static readonly HttpClient _httpClient = new();
        private bool _isDownloading;
        private bool _ffmpegReady;

        /// <summary>
        /// The file paths of all successfully downloaded songs.
        /// Read this after the window closes.
        /// </summary>
        public IReadOnlyList<string> DownloadedPaths => _downloadedPaths;

        public DownloadWindow()
        {
            InitializeComponent();

            _downloadFolder = Path.Combine(AppContext.BaseDirectory, "Music");
            _coverFolder = Path.Combine(_downloadFolder, "AlbumCovers");
            _ffmpegFolder = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
            Directory.CreateDirectory(_downloadFolder);
            Directory.CreateDirectory(_coverFolder);
            Directory.CreateDirectory(_ffmpegFolder);
        }

        private async Task EnsureFfmpegAsync()
        {
            if (_ffmpegReady)
                return;

            FFmpeg.SetExecutablesPath(_ffmpegFolder);

            // Check if ffmpeg is already downloaded
            string ffmpegExe = Path.Combine(_ffmpegFolder, "ffmpeg.exe");
            if (!File.Exists(ffmpegExe))
            {
                StatusText.Text = "Downloading ffmpeg (first time only)...";
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _ffmpegFolder);
            }

            _ffmpegReady = true;
        }

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                StatusText.Text = "Please paste a YouTube URL first.";
                return;
            }

            if (_isDownloading)
                return;

            _isDownloading = true;
            DownloadBtn.IsEnabled = false;
            DownloadBtn.Content = "Downloading...";
            _downloadedPaths.Clear();
            CountText.Text = "";
            DownloadProgressFill.Width = 0;

            try
            {
                // Ensure ffmpeg is available for conversion
                await EnsureFfmpegAsync();

                var youtube = new YoutubeClient();

                // Collect video info to download
                var videoInfos = new List<(VideoId Id, string Title, IReadOnlyList<Thumbnail> Thumbnails)>();

                if (url.Contains("list=", StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text = "Fetching playlist...";
                    var playlistVideos = await youtube.Playlists.GetVideosAsync(url);
                    foreach (var v in playlistVideos)
                        videoInfos.Add((v.Id, v.Title, v.Thumbnails));
                    StatusText.Text = $"Found {videoInfos.Count} videos in playlist.";
                }
                else
                {
                    StatusText.Text = "Fetching video info...";
                    var video = await youtube.Videos.GetAsync(url);
                    videoInfos.Add((video.Id, video.Title, video.Thumbnails));
                    StatusText.Text = $"Found: {video.Title}";
                }

                if (videoInfos.Count == 0)
                {
                    StatusText.Text = "No videos found at that URL.";
                    return;
                }

                int total = videoInfos.Count;
                int done = 0;
                int failed = 0;
                string lastError = "";

                foreach (var (id, title, thumbnails) in videoInfos)
                {
                    try
                    {
                        string safeTitle = SanitizeFileName(title);
                        string wavPath = Path.Combine(_downloadFolder, $"{safeTitle}.wav");

                        // Skip if WAV already exists
                        if (File.Exists(wavPath))
                        {
                            _downloadedPaths.Add(wavPath);
                            done++;
                            CountText.Text = $"{done} / {total} done";
                            continue;
                        }

                        StatusText.Text = $"[{done + 1}/{total}] Downloading: {title}";

                        var manifest = await youtube.Videos.Streams.GetManifestAsync(id);
                        var audioStream = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                        if (audioStream == null)
                        {
                            lastError = $"No audio stream for: {title}";
                            StatusText.Text = $"[{done + 1}/{total}] {lastError}";
                            failed++;
                            done++;
                            continue;
                        }

                        // Download to a temp file first
                        string ext = audioStream.Container.Name;
                        string tempPath = Path.Combine(_downloadFolder, $"{safeTitle}_temp.{ext}");

                        var progress = new Progress<double>(p =>
                        {
                            // Download is ~80% of the work, conversion is ~20%
                            double totalProgress = (done + p * 0.8) / total;
                            var parentGrid = (Grid)DownloadProgressFill.Parent;
                            if (parentGrid.ActualWidth > 0)
                                DownloadProgressFill.Width = totalProgress * parentGrid.ActualWidth;
                        });

                        await youtube.Videos.Streams.DownloadAsync(audioStream, tempPath, progress);

                        // Convert to WAV
                        StatusText.Text = $"[{done + 1}/{total}] Converting to WAV: {title}";

                        var conversion = await FFmpeg.Conversions.FromSnippet.Convert(tempPath, wavPath);
                        await conversion.Start();

                        // Clean up temp file
                        try { File.Delete(tempPath); } catch { }

                        // Download thumbnail as album cover
                        await DownloadThumbnailAsync(thumbnails, safeTitle);

                        _downloadedPaths.Add(wavPath);
                        done++;
                        CountText.Text = $"{done} / {total} done";

                        var progressGrid = (Grid)DownloadProgressFill.Parent;
                        if (progressGrid.ActualWidth > 0)
                            DownloadProgressFill.Width = (double)done / total * progressGrid.ActualWidth;
                    }
                    catch (Exception ex)
                    {
                        done++;
                        failed++;
                        lastError = $"{title} — {ex.Message}";
                        StatusText.Text = $"[{done}/{total}] Failed: {lastError}";
                        CountText.Text = $"{done} / {total} done ({failed} failed)";
                    }
                }

                // Final summary
                if (_downloadedPaths.Count > 0 && failed == 0)
                    StatusText.Text = $"? Done! Downloaded {_downloadedPaths.Count} song(s) as WAV.";
                else if (_downloadedPaths.Count > 0)
                    StatusText.Text = $"Downloaded {_downloadedPaths.Count} song(s), {failed} failed.";
                else
                    StatusText.Text = $"All {total} download(s) failed. Last error: {lastError}";

                CountText.Text = $"{_downloadedPaths.Count} song(s) ready to add";
            }
            catch (Exception ex)
            {
                // This catches errors from fetching the video/playlist info
                StatusText.Text = $"Error: {ex.GetType().Name}: {ex.Message}";
                CountText.Text = "Download failed — see error above.";
            }
            finally
            {
                _isDownloading = false;
                DownloadBtn.IsEnabled = true;
                DownloadBtn.Content = "Download";
            }
        }

        /// <summary>
        /// Downloads the best available YouTube thumbnail and saves it as a .jpg
        /// in the AlbumCovers folder, named to match the audio file.
        /// </summary>
        private async Task DownloadThumbnailAsync(IReadOnlyList<Thumbnail> thumbnails, string safeTitle)
        {
            string coverPath = Path.Combine(_coverFolder, $"{safeTitle}.jpg");

            // Skip if cover already exists
            if (File.Exists(coverPath))
                return;

            if (thumbnails.Count == 0)
                return;

            try
            {
                // Pick the highest resolution thumbnail
                var best = thumbnails.GetWithHighestResolution();
                string thumbUrl = best.Url;

                byte[] imageBytes = await _httpClient.GetByteArrayAsync(thumbUrl);
                await File.WriteAllBytesAsync(coverPath, imageBytes);
            }
            catch
            {
                // Non-critical — song still works without a cover
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _downloadedPaths.Count > 0;
            Close();
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
                name = name.Replace(c, '_');

            if (name.Length > 200)
                name = name[..200];

            return name.Trim();
        }
    }
}

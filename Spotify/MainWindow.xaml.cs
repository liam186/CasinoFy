using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Media;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Spotify
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<SongItem> _songs;
        private readonly List<string> _musicPaths;
        private int _currentIndex = -1;
        private readonly MediaPlayer _player;
        private readonly DispatcherTimer _timer;
        private bool _isPlaying;

        public MainWindow()
        {
            InitializeComponent();

            string musicFolder = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Music"
            );

            // Try to load saved playlist; fall back to defaults
            List<string>? saved = PlaylistStorage.Load();

            if (saved != null && saved.Count > 0)
            {
                _musicPaths = saved;
            }
            else
            {
                _musicPaths =
                [
                 
                ];

                // Save the defaults so they persist
                PlaylistStorage.Save(_musicPaths);
            }

            _songs = new ObservableCollection<SongItem>();
            foreach (string path in _musicPaths)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                _songs.Add(new SongItem
                {
                    Name = name,
                    CoverImage = SongItem.LoadCover(name)
                });
            }

            SongListBox.ItemsSource = _songs;

            _player = new MediaPlayer();
            _player.MediaOpened += Player_MediaOpened;
            _player.MediaEnded += Player_MediaEnded;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += Timer_Tick;

            UpdateSongCount();
        }

        // ---------- Add / Remove ----------

        private void AddSongBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select audio files",
                Filter = "Audio Files|*.wav;*.mp3;*.wma;*.aac;*.m4a;*.flac;*.ogg;*.aiff|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return;

            foreach (string path in dialog.FileNames)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                _musicPaths.Add(path);
                _songs.Add(new SongItem
                {
                    Name = name,
                    CoverImage = SongItem.LoadCover(name)
                });
            }

            UpdateSongCount();
            PlaylistStorage.Save(_musicPaths);
        }

        private void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlWindow = new DownloadWindow { Owner = this };
            if (dlWindow.ShowDialog() == true)
            {
                foreach (string path in dlWindow.DownloadedPaths)
                {
                    // Avoid duplicates
                    if (_musicPaths.Contains(path))
                        continue;

                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    _musicPaths.Add(path);
                    _songs.Add(new SongItem
                    {
                        Name = name,
                        CoverImage = SongItem.LoadCover(name)
                    });
                }

                UpdateSongCount();
                PlaylistStorage.Save(_musicPaths);
            }
        }

        private void RemoveSongBtn_Click(object sender, RoutedEventArgs e)
        {
            int sel = SongListBox.SelectedIndex;
            if (sel < 0)
            {
                MessageBox.Show("Select a song to remove.", "Casino Player",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (sel == _currentIndex)
            {
                _timer.Stop();
                _player.Stop();
                _isPlaying = false;
                PlayPauseBtn.Content = "▶";
                Music_name.Text = "Select a song";
                TimeStamp.Text = "0:00";
                ProgressFill.Width = 0;
                BottomSongName.Text = "";
                _currentIndex = -1;
                ClearAlbumArt();
            }
            else if (sel < _currentIndex)
            {
                _currentIndex--;
            }

            _musicPaths.RemoveAt(sel);
            _songs.RemoveAt(sel);
            UpdateSongCount();
            PlaylistStorage.Save(_musicPaths);
        }

        // ---------- List selection ----------

        private void SongListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int sel = SongListBox.SelectedIndex;
            if (sel < 0 || sel >= _musicPaths.Count) return;

            _currentIndex = sel;
            PlayCurrentSong();
        }

        // ---------- Transport ----------

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < 0)
            {
                if (_musicPaths.Count > 0)
                {
                    _currentIndex = 0;
                    SongListBox.SelectedIndex = 0;
                    PlayCurrentSong();
                }
                return;
            }

            if (_isPlaying)
            {
                _player.Pause();
                _timer.Stop();
                _isPlaying = false;
                PlayPauseBtn.Content = "▶";
            }
            else
            {
                _player.Play();
                _timer.Start();
                _isPlaying = true;
                PlayPauseBtn.Content = "⏸";
            }
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_musicPaths.Count == 0) return;

            _currentIndex++;
            if (_currentIndex >= _musicPaths.Count)
                _currentIndex = 0;

            SongListBox.SelectedIndex = _currentIndex;
            PlayCurrentSong();
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_musicPaths.Count == 0) return;

            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _musicPaths.Count - 1;

            SongListBox.SelectedIndex = _currentIndex;
            PlayCurrentSong();
        }

        // ---------- Playback core ----------

        private void PlayCurrentSong()
        {
            _timer.Stop();
            _player.Stop();

            string path = _musicPaths[_currentIndex];

            if (!File.Exists(path))
            {
                MessageBox.Show($"File not found:\n{path}", "Casino Player",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = _currentIndex >= 0 && _currentIndex < _songs.Count
                ? _songs[_currentIndex].Name
                : System.IO.Path.GetFileNameWithoutExtension(path);

            Music_name.Text = name;
            BottomSongName.Text = path;

            UpdateAlbumArt(name);

            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Play();
            _isPlaying = true;
            PlayPauseBtn.Content = "⏸";
        }

        private void Player_MediaOpened(object? sender, EventArgs e)
        {
            TimeStamp.Text = "0:00";
            ProgressFill.Width = 0;
            _timer.Start();
        }

        private void Player_MediaEnded(object? sender, EventArgs e)
        {
            _timer.Stop();
            _isPlaying = false;
            PlayPauseBtn.Content = "▶";

            if (_musicPaths.Count == 0) return;
            _currentIndex++;
            if (_currentIndex >= _musicPaths.Count)
                _currentIndex = 0;

            SongListBox.SelectedIndex = _currentIndex;
            PlayCurrentSong();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_player.NaturalDuration.HasTimeSpan) return;

            var pos = _player.Position;
            var dur = _player.NaturalDuration.TimeSpan;

            TimeStamp.Text = $"{(int)pos.TotalMinutes}:{pos.Seconds:D2} / {(int)dur.TotalMinutes}:{dur.Seconds:D2}";

            double fraction = dur.TotalSeconds > 0 ? pos.TotalSeconds / dur.TotalSeconds : 0;
            ProgressFill.Width = fraction * ((Grid)ProgressFill.Parent).ActualWidth;
        }

        // ---------- Helpers ----------

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_player == null) return;

            double vol = e.NewValue / 100.0;
            _player.Volume = vol;

            int pct = (int)e.NewValue;
            if (VolumePercent != null)
                VolumePercent.Text = $"{pct}%";
            if (VolumeIcon != null)
            {
                VolumeIcon.Text = pct == 0 ? "🔇"
                                : pct < 33 ? "🔈"
                                : pct < 66 ? "🔉"
                                : "🔊";
            }
        }

        private void UpdateSongCount()
        {
            SongCountText.Text = _musicPaths.Count == 1 ? "1 song" : $"{_musicPaths.Count} songs";
        }

        private void UpdateAlbumArt(string songName)
        {
            string[] searchFolders =
            [
                System.IO.Path.Combine(AppContext.BaseDirectory, "Music", "AlbumCovers"),
                System.IO.Path.Combine(AppContext.BaseDirectory, "AlbumCovers"),
            ];

            string[] extensions = [".avif", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];
            string? coverPath = null;

            foreach (string folder in searchFolders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (string ext in extensions)
                {
                    string candidate = System.IO.Path.Combine(folder, songName + ext);
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
                        string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (string.Equals(fileNameWithoutExt, songName, StringComparison.OrdinalIgnoreCase))
                        {
                            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
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
                    bitmap.DecodePixelWidth = 260;
                    bitmap.EndInit();

                    AlbumArtImage.Source = bitmap;
                    AlbumArtPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    ClearAlbumArt();
                }
            }
            else
            {
                ClearAlbumArt();
            }
        }

        private void ClearAlbumArt()
        {
            AlbumArtImage.Source = null;
            AlbumArtPlaceholder.Visibility = Visibility.Visible;
        }
    }
}
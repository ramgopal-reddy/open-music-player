using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using TagLib;
using System.Collections.ObjectModel;


namespace MyMusicPlayer
{
    public partial class MainWindow : Window
    {
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;
        private Storyboard rotateStoryboard;
        private bool isDraggingSlider;
        private bool isFullScreen = false;
        private WindowState previousState;
        private WindowStyle previousStyle;
        private bool controlsVisible = true;
        // PLAYLIST
        private ObservableCollection<string> playlist = new();
        private int currentIndex = -1;
        // LOOP MODES
        private enum LoopMode
        {
            None,
            One,
            All
        }

        private LoopMode currentLoopMode = LoopMode.None;


        public MainWindow()
        {
            InitializeComponent();
            rotateStoryboard = (Storyboard)FindResource("RotateStoryboard");
            this.WindowState = WindowState.Maximized;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
        }

        // ---------------- ADD Songs to Playlist ------------------
        private void AddSongs_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.m4a",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    playlist.Add(file);
                    PlaylistList.Items.Add(System.IO.Path.GetFileName(file));
                }

                if (currentIndex == -1 && playlist.Count > 0)
                {
                    currentIndex = 0;
                    PlayFromPlaylist(currentIndex);
                }
            }
        }

        // ---------------- Play from Playlist ------------------
        private void PlayFromPlaylist(int index)
        {
            if (index < 0 || index >= playlist.Count)
                return;

            StopPlayback();

            string filePath = playlist[index];
            LoadMetadata(filePath);

            audioFile = new AudioFileReader(filePath);
            outputDevice = new WaveOutEvent();
            outputDevice.Init(audioFile);
            outputDevice.Volume = (float)VolumeSlider.Value;
            outputDevice.Play();

            SeekSlider.Maximum = audioFile.TotalTime.TotalSeconds;
            rotateStoryboard.Begin();

            currentIndex = index;
            PlaylistList.SelectedIndex = index;

            outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
        }

        // ----------------- Handle Song End --------------
        private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (currentLoopMode)
                {
                    case LoopMode.One:
                        PlayFromPlaylist(currentIndex);
                        break;

                    case LoopMode.All:
                        currentIndex = (currentIndex + 1) % playlist.Count;
                        PlayFromPlaylist(currentIndex);
                        break;

                    case LoopMode.None:
                        if (currentIndex + 1 < playlist.Count)
                        {
                            currentIndex++;
                            PlayFromPlaylist(currentIndex);
                        }
                        break;
                }
            });
        }

        // -------------------- Playlist Click -------------------------

        private void PlaylistList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PlaylistList.SelectedIndex >= 0)
            {
                PlayFromPlaylist(PlaylistList.SelectedIndex);
            }
        }

        // ---------------- Loop Button Logic --------------------
        private void LoopBtn_Click(object sender, RoutedEventArgs e)
        {
            currentLoopMode = currentLoopMode switch
            {
                LoopMode.None => LoopMode.All,
                LoopMode.All => LoopMode.One,
                LoopMode.One => LoopMode.None,
                _ => LoopMode.None
            };

            LoopBtn.Content = currentLoopMode switch
            {
                LoopMode.None => "No Loop",
                LoopMode.All => "Loop All",
                LoopMode.One => "Loop One",
                _ => "No Loop"
            };
        }



        // ---------------- FullScreen Toggle Button ----------------

        private void ToggleFullScreen(object sender, RoutedEventArgs e)
        {
            if (this.WindowStyle == WindowStyle.None)
            {
                // Exit full screen
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ResizeMode = ResizeMode.CanResize;
                this.WindowState = WindowState.Normal;
            }
            else
            {
                // Enter full screen
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowState = WindowState.Maximized;
            }
        }

        // ---------------- FULLSCREEN TOGGLE ----------------
        private void ToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (!isFullScreen)
            {
                previousState = WindowState;
                previousStyle = WindowStyle;

                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                Topmost = true;

                isFullScreen = true;
            }
            else
            {
                WindowStyle = previousStyle;
                WindowState = previousState;
                Topmost = false;

                isFullScreen = false;
            }
        }

        // ---------------- TOGGLE CONTROLS ----------------
        private void ToggleControls_Click(object sender, RoutedEventArgs e)
        {
            if (controlsVisible)
            {
                ControlsPanel.Visibility = Visibility.Collapsed;
                controlsVisible = false;
            }
            else
            {
                ControlsPanel.Visibility = Visibility.Visible;
                controlsVisible = true;
            }
        }

        // ---------------- PLAY ----------------
        private void Play_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.flac;*.m4a"
            };

            if (dialog.ShowDialog() == true)
            {
                StopPlayback();

                LoadMetadata(dialog.FileName);

                audioFile = new AudioFileReader(dialog.FileName);
                outputDevice = new WaveOutEvent();
                outputDevice.Init(audioFile);
                outputDevice.Volume = (float)VolumeSlider.Value;
                outputDevice.Play();

                SeekSlider.Maximum = audioFile.TotalTime.TotalSeconds;
                AnimateNeedle(-30); // Move needle to playing position

                rotateStoryboard.Begin();
            }
        }

        // ---------------- PAUSE ----------------
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (outputDevice?.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
                rotateStoryboard.Pause();
                AnimateNeedle(0); // Move needle to paused position
            }
            else if (outputDevice?.PlaybackState == PlaybackState.Paused)
            {
                outputDevice.Play();
                rotateStoryboard.Resume();
                AnimateNeedle(-30); // Move needle to playing position
            }
        }

        // ---------------- Needle Rotation Animation ----------------
        private void AnimateNeedle(double targetAngle)
        {
            var animation = new DoubleAnimation
            {
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(1000),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            NeedleRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        // ---------------- BACKGROUND CHANGE ----------------
        private void Background_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(path, UriKind.Relative)),
                    Stretch = Stretch.Fill,
                    RelativeTransform = new RotateTransform(270, 0.5, 0.5)
                };
            }
        }

        // --------------- BackGround Change ComboBox ----------------
        private void Background_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)sender).SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(tag, UriKind.Relative)),
                    Stretch = Stretch.Fill,
                    RelativeTransform = new RotateTransform(270, 0.5, 0.5)
                };
            }
        }



        // ---------------- STOP ----------------
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            rotateStoryboard.Stop();
            AlbumRotate.Angle = 0;
            AnimateNeedle(0); // Move needle to paused position
            SeekSlider.Value = 0;
        }

        private void StopPlayback()
        {
            outputDevice?.Stop();
            outputDevice?.Dispose();
            audioFile?.Dispose();
            outputDevice = null;
            audioFile = null;
        }

        // ---------------- METADATA ----------------
        private void LoadMetadata(string path)
        {
            var tagFile = TagLib.File.Create(path);

            TitleText.Text = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(path);
            ArtistText.Text = tagFile.Tag.FirstPerformer ?? "Unknown Artist";

            if (tagFile.Tag.Pictures.Length > 0)
            {
                var pic = tagFile.Tag.Pictures[0];
                using var ms = new MemoryStream(pic.Data.Data);
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                AlbumImageBrush.ImageSource = image;
            }
            else
            {
                AlbumImageBrush.ImageSource = new BitmapImage(
                    new Uri("MyMusic/Resources/music_note.png", UriKind.Relative));
            }
        }

        // ---------------- SEEK ----------------
        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioFile != null && !isDraggingSlider)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(SeekSlider.Value);
            }
        }

        // ---------------- VOLUME ----------------
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (outputDevice != null)
            {
                outputDevice.Volume = (float)VolumeSlider.Value;
            }
        }

        private void SeekSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isDraggingSlider = true;
        }

        private void SeekSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            isDraggingSlider = false;
            if (audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(SeekSlider.Value);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPlayback();
            base.OnClosed(e);
        }
    }
}

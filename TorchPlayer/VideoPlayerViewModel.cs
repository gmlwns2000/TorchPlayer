using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace TorchPlayer
{
    public class VideoPlayerViewModel : INotifyPropertyChanged, IDisposable
    {
        bool progressRingActive = true;
        public bool ProgressRingActive
        {
            get => progressRingActive;
            set { progressRingActive = value; OnPropertyChanged(); }
        }

        public string Title => vp.args.Name;

        double seekBarMax;
        public double SeekBarMax
        {
            get => seekBarMax;
            set { seekBarMax = value; OnPropertyChanged(); }
        }

        public double SeekBarValue
        {
            get => vp.player.Position.TotalSeconds;
            set { vp.player.Position = TimeSpan.FromSeconds(value); }
        }

        public string PositionText => vp.player.Position.ToString("hh\\:mm\\:ss");

        public int Volume
        {
            get => vp.player.Volume;
            set { vp.player.Volume = value; OnPropertyChanged(); }
        }

        Visibility playButtonVisibility = Visibility.Collapsed;
        public Visibility PlayButtonVisibility
        {
            get => playButtonVisibility;
            set { playButtonVisibility = value; OnPropertyChanged(); }
        }

        Visibility pauseButtonVisibility = Visibility.Visible;
        public Visibility PauseButtonVisibility
        {
            get => pauseButtonVisibility;
            set { pauseButtonVisibility = value; OnPropertyChanged(); }
        }

        public ICommand PauseCommand => new UICommand(() =>
        {
            vp.player.Pause();
            PauseButtonVisibility = Visibility.Collapsed;
            PlayButtonVisibility = Visibility.Visible;
        });

        public ICommand PlayCommand => new UICommand(() =>
        {
            vp.player.Play();
            PauseButtonVisibility = Visibility.Visible;
            PlayButtonVisibility = Visibility.Collapsed;
        });

        Visibility fullScreenButtonVisibility;
        public Visibility FullScreenButtonVisibility
        {
            get => fullScreenButtonVisibility;
            set { fullScreenButtonVisibility = value; OnPropertyChanged(); }
        }

        Visibility backToWindowButtonVisibility;
        public Visibility BackToWindowButtonVisibility
        {
            get => backToWindowButtonVisibility;
            set { backToWindowButtonVisibility = value; OnPropertyChanged(); }
        }

        public ICommand FullScreenButtonCommand => new UICommand(() =>
        {
            var view = ApplicationView.GetForCurrentView();
            if (!view.IsFullScreenMode)
                view.TryEnterFullScreenMode();
            FullScreenButtonVisibility = Visibility.Collapsed;
            BackToWindowButtonVisibility = Visibility.Visible;
        });

        public ICommand BackToWindowButtonCommand => new UICommand(() =>
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
                view.ExitFullScreenMode();
            FullScreenButtonVisibility = Visibility.Visible;
            BackToWindowButtonVisibility = Visibility.Collapsed;
        });

        bool zoomUniformFill = false;
        public ICommand VideoZoomButtonCommand => new UICommand(() =>
        {
            zoomUniformFill = !zoomUniformFill;
            var adsf = vp.player.MediaPlayer.cropGeometry();
            if (zoomUniformFill)
            {
                vp.SizeChanged += Vp_SizeChanged;
                Vp_SizeChanged(vp, null);
            }
            else
            {
                vp.SizeChanged -= Vp_SizeChanged;
                vp.player.MediaPlayer.setCropGeometry("");
            }
        });

        void Vp_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            vp.player.MediaPlayer.setCropGeometry($"{(int)vp.ActualWidth}:{(int)vp.ActualHeight}");
        }

        public ICommand GoBackButtonCommand => new UICommand(() =>
        {
            vp.GoBack();
        });

        Visibility positionNotifyVisibility = Visibility.Collapsed;
        public Visibility PositionNotifyVisibility
        {
            get => positionNotifyVisibility;
            set { positionNotifyVisibility = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        CoreDispatcher dispatcher;
        public DispatcherTimer updater;
        VideoPlayer vp;

        public VideoPlayerViewModel(VideoPlayer vp)
        {
            this.vp = vp;
            var player = vp.player;
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            updater = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(250) };
            updater.Tick += delegate
            {
                SeekBarMax = player.MediaPlayer.length() / 1000.0;
                OnPropertyChanged(nameof(SeekBarValue));
                OnPropertyChanged(nameof(PositionText));
            };

            updater.Start();

            var view = ApplicationView.GetForCurrentView();
            fullScreenButtonVisibility = view.IsFullScreenMode ? Visibility.Collapsed : Visibility.Visible;
            backToWindowButtonVisibility = view.IsFullScreenMode ? Visibility.Visible : Visibility.Collapsed;
        }

        async void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (dispatcher.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.High,
                    () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }

        public void Dispose()
        {
            updater?.Stop();
            updater = null;
        }
    }

}

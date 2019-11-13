using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace TorchPlayer
{
    public sealed partial class VideoPlayer : Page
    {
        public PlaybackOpenedEventArgs args;
        public VLC.MediaElement player => PlayerMain;

        VideoPlayerViewModel vm;
        DispatcherTimer hider;
        bool isShowed = true;

        public VideoPlayer()
        {
            InitializeComponent();
            hider = new DispatcherTimer();
            hider.Interval = TimeSpan.FromSeconds(2);
            hider.Tick += delegate
            {
                HideUI();
                hider.Stop();
            };
            hider.Start();

            PlayerMain.Tapped += delegate
            {
                if (isShowed)
                    HideUI();
            };

            PlayerMain.DoubleTapped += delegate
            {
                if (vm.PlayButtonVisibility == Visibility.Visible)
                {
                    vm.PlayCommand.Execute(null);
                }
                else
                {
                    vm.PauseCommand.Execute(null);
                }
            };

            PlayerMain.PointerMoved += (sender, e) =>
            {
                var pointer = e.GetCurrentPoint(this);

                if (!isPointerDragging && pointerPressedPoint != null && PointDistance(pointerPressedPoint.Position, pointer.Position) > 32)
                {
                    isPointerDragging = true;
                    pointerPressedTime = player.Position;
                    vm.PositionNotifyVisibility = Visibility.Visible;
                    vm.updater.Interval = TimeSpan.FromMilliseconds(12);
                }

                if (isPointerDragging)
                {
                    var offset = (pointer.Position.X - pointerPressedPoint.Position.X) / 10;
                    player.Position = pointerPressedTime + TimeSpan.FromSeconds(offset);
                }
            };
        }

        PointerPoint pointerPressedPoint;
        TimeSpan pointerPressedTime;
        bool isPointerDragging;
        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            pointerPressedPoint = e.GetCurrentPoint(this);
            pointerPressedTime = player.Position;
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            OnALLPointerReleased();
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            OnALLPointerReleased();
        }

        void OnALLPointerReleased()
        {
            pointerPressedPoint = null;
            isPointerDragging = false;
            if (vm != null)
            {
                vm.PositionNotifyVisibility = Visibility.Collapsed;
                vm.updater.Interval = TimeSpan.FromMilliseconds(250);
            }
        }

        double PointDistance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            ShowUI();
            hider.Start();
        }

        DispatcherTimer showWaiter;
        void ShowUI()
        {
            VisualStateManager.GoToState(this, "UIFadeIn", true);
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 537);
            if(showWaiter == null)
            {
                showWaiter = new DispatcherTimer();
                showWaiter.Interval = TimeSpan.FromMilliseconds(300);
                showWaiter.Tick += delegate
                {
                    isShowed = true;
                    showWaiter.Stop();
                };
            }
            showWaiter.Start();
        }

        void HideUI()
        {
            showWaiter?.Stop();
            hider?.Stop();
            if (vm != null)
            {
                VisualStateManager.GoToState(this, "UIFadeOut", true);
                Window.Current.CoreWindow.PointerCursor = null;
            }
            isShowed = false;
        }

        void VideoPlayer_BackRequested(object sender, BackRequestedEventArgs e)
        {
            GoBack();
            e.Handled = true;
        }

        DisplayRequest displayRequest;
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ClosePlayer();
            args = (PlaybackOpenedEventArgs)e.Parameter;
            player.MediaFailed += async (sender, args) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                {
                    var dialog = new MessageDialog(
                        $"{args.ErrorMessage}\n{args.OriginalSource}",
                        args.ErrorTitle);
                    await dialog.ShowAsync();
                    GoBack();
                });
            };
            player.MediaOpened += (sender, args) =>
            {
                vm.ProgressRingActive = false;
            };
            player.MediaEnded += async (sender, args) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () => GoBack());
            };

            DataContext = vm = new VideoPlayerViewModel(this);

            PlayerMain.Source = args.Address;
            player.Play();

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Disabled;
            SystemNavigationManager.GetForCurrentView().BackRequested += VideoPlayer_BackRequested;

            displayRequest = new DisplayRequest();
            displayRequest.RequestActive();

            Window.Current.SetTitleBar(TitleBar);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ClosePlayer();

            SystemNavigationManager.GetForCurrentView().BackRequested -= VideoPlayer_BackRequested;
        }

        void ClosePlayer()
        {
            DataContext = null;

            args?.Dispose();
            args = null;

            displayRequest?.RequestRelease();
            displayRequest = null;

            player?.Stop();
            //player = null;

            vm?.Dispose();
            vm = null;

            args = null;

            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 573);
        }

        public void GoBack()
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Core;
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
    public sealed partial class MainPage : Page
    {
        public bool IsForwardPage;
        Explorer explorer;
        public MainPage()
        {
            NavigationCacheMode = NavigationCacheMode.Required;

            InitializeComponent();

            InitAsync();
        }

        int TryConvertToInt(string text)
        {
            if (text == null) return 0;
            try { return Convert.ToInt32(text); }
            catch { return 0; }
        }

        async void InitAsync()
        {
            //load address
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            var server = new FTPServerAddress()
            {
                Server = settings.Values["server.server"] as string ?? "localhost",
                Port = TryConvertToInt(settings.Values["server.port"] as string ?? "21"),
                User = settings.Values["server.user"] as string ?? "anonymous",
                Password = settings.Values["server.password"] as string ?? "anonymous",
            };

            var okay = await FTPExplorer.CheckServer(server);
            if (!okay)
                Environment.Exit(0);

            settings.Values["server.server"] = server.Server;
            settings.Values["server.port"] = server.Port.ToString();
            settings.Values["server.user"] = server.User;
            settings.Values["server.password"] = server.Password;

            explorer = new FTPExplorer(server);

            explorer.PlaybackOpened += (sender, e) =>
            {
                Frame.Navigate(typeof(VideoPlayer), e, new DrillInNavigationTransitionInfo());
            };

            DataContext = explorer;

            SystemNavigationManager.GetForCurrentView().BackRequested += (sender, e) =>
            {
                if (IsForwardPage)
                {
                    explorer.GoBackCommand.Execute(sender);
                    e.Handled = true;
                }
            };
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            IsForwardPage = false;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Disabled;
            IsForwardPage = true;

            ApplicationViewTitleBar formattableTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            formattableTitleBar.ButtonBackgroundColor = Colors.Transparent;
            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            Window.Current.SetTitleBar(Topbar_Background);
        }

        void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = (FTPEntry)e.ClickedItem;
            item.Command.Execute(sender);
        }

        bool isSearching = false;
        private void Bt_ToggleSearch_Click(object sender, RoutedEventArgs e)
        {
            isSearching = !isSearching;
            if (isSearching)
            {
                Tbx_SearchTerm.IsEnabled = true;
                Tbx_SearchTerm.IsHitTestVisible = true;
                VisualStateManager.GoToState(this, "SearchFadeIn", true);
            }
            else
            {
                Tbx_SearchTerm.IsEnabled = false;
                Tbx_SearchTerm.IsHitTestVisible = false;
                explorer.SearchTerm = "";
                VisualStateManager.GoToState(this, "SearchFadeOut", true);
            }
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VpnMonitor.WUI.MVVM.ViewModel;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.View
{
    public sealed partial class ShellView : Page
    {
        public ShellViewModel ViewModel { get; set; }

        public ShellView(ShellViewModel shellViewModel, InfoBarService infoBarService)
        {
            InitializeComponent();
            DataContext = ViewModel = shellViewModel;

            App.MainWindow.ExtendsContentIntoTitleBar = true;
            App.MainWindow.SetTitleBar(AppTitleBar);

            // Before navigation: pages report loading errors to the InfoBar
            infoBarService.Initialization(PageInfoBar);

            ViewModel.NavigationHelperService.Initialize(NavView, ContentFrame);
            NavView.SelectedItem = NavView.MenuItems[0];
            ViewModel.NavigationHelperService.Navigate(NavigationHelperService.ActualClients);
        }

        private void NavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            AppTitleBar.Margin = new Thickness()
            {
                Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
                Top = AppTitleBar.Margin.Top,
                Right = AppTitleBar.Margin.Right,
                Bottom = AppTitleBar.Margin.Bottom
            };
        }
    }
}

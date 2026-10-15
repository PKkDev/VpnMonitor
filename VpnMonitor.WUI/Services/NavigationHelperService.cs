using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using VpnMonitor.WUI.MVVM.View;

namespace VpnMonitor.WUI.Services
{
    public class NavigationHelperService
    {
        public const string ActualClients = "ActualClients";
        public const string ClientDetails = "ClientDetails";
        public const string TrafficStatistics = "TrafficStatistics";
        public const string ServerOverview = "ServerOverview";

        private NavigationView NavigationView { get; set; } = null!;
        private Frame ContentFrame { get; set; } = null!;

        private readonly Dictionary<string, Type> _pages = new()
        {
            { ActualClients, typeof(ActualClientsView) },
            { ClientDetails, typeof(ClientDetailsView) },
            { TrafficStatistics, typeof(TrafficStatisticsView) },
            { ServerOverview, typeof(ServerOverviewView) },
        };

        public NavigationHelperService() { }

        public void Initialize(NavigationView navigationView, Frame contentFrame)
        {
            NavigationView = navigationView;
            ContentFrame = contentFrame;

            NavigationView.BackRequested += OnBackRequested;
            NavigationView.ItemInvoked += OnItemInvoked;
            ContentFrame.NavigationFailed += OnNavigationFailed;
            ContentFrame.Navigated += OnNavigated;
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        { }

        private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            NavigationView.IsBackEnabled = ContentFrame.CanGoBack;

            // After going back the menu highlights the page that is shown; the client card keeps the item it was opened from
            var tag = _pages.FirstOrDefault(x => x.Value == e.SourcePageType).Key;
            var item = NavigationView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(x => Equals(x.Tag, tag));

            if (item != null)
                NavigationView.SelectedItem = item;
        }

        private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                Navigate("Settings");
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                Navigate(navItemTag!);
            }
        }

        public void ClearBackStack()
        {
            ContentFrame.BackStack.Clear();
        }

        /// <param name="parameter">Passed to the page, e.g. the name of the client for its card.</param>
        public void Navigate(string navItemTag, object? parameter = null)
        {
            if (!_pages.TryGetValue(navItemTag, out var page))
                return;

            // The same page is opened again only with another parameter
            if (page == ContentFrame.CurrentSourcePageType && parameter == null)
                return;

            ContentFrame.Navigate(page, parameter, new DrillInNavigationTransitionInfo());
        }
    }
}

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VpnMonitor.WUI.MVVM.ViewModel;

namespace VpnMonitor.WUI.MVVM.View
{
    public sealed partial class TrafficStatisticsView : Page
    {
        public TrafficStatisticsViewModel ViewModel { get; set; }

        public TrafficStatisticsView()
        {
            InitializeComponent();

            DataContext = ViewModel = App.GetService<TrafficStatisticsViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = ViewModel.Refresh();
        }

        private void OnClientClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ClientTrafficItem item)
                ViewModel.OpenClient(item.Client);
        }
    }
}

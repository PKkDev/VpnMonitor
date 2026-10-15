using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VpnMonitor.WUI.MVVM.ViewModel;

namespace VpnMonitor.WUI.MVVM.View
{
    public sealed partial class ServerOverviewView : Page
    {
        public ServerOverviewViewModel ViewModel { get; set; }

        public ServerOverviewView()
        {
            InitializeComponent();

            DataContext = ViewModel = App.GetService<ServerOverviewViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = ViewModel.Refresh();
        }
    }
}

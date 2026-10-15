using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VpnMonitor.WUI.MVVM.ViewModel;

namespace VpnMonitor.WUI.MVVM.View
{
    public sealed partial class ClientDetailsView : Page
    {
        public ClientDetailsViewModel ViewModel { get; set; }

        public ClientDetailsView()
        {
            InitializeComponent();

            DataContext = ViewModel = App.GetService<ClientDetailsViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string clientName)
                _ = ViewModel.Load(clientName);
        }
    }
}

using Microsoft.UI.Xaml.Controls;
using VpnMonitor.WUI.MVVM.Model;
using VpnMonitor.WUI.MVVM.ViewModel;

namespace VpnMonitor.WUI.MVVM.View
{
    public sealed partial class ActualClientsView : Page
    {
        public ActualClientsViewModel ViewModel { get; set; }

        public ActualClientsView()
        {
            InitializeComponent();

            DataContext = ViewModel = App.GetService<ActualClientsViewModel>();
        }

        private void OnClientClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is VpnClientActualDataResult client)
                ViewModel.OpenClient(client);
        }
    }
}

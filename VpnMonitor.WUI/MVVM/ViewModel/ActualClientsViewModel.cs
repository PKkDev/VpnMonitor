using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VpnMonitor.WUI.MVVM.Model;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    public class ActualClientsViewModel : ObservableRecipient
    {
        private readonly NavigationHelperService _navigationHelperService;

        /// <summary>Data refreshed in the background, shared with the tray and the notifications.</summary>
        public ClientStatusService Status { get; }

        public AsyncRelayCommand OnRefreshData { get; }

        public ActualClientsViewModel(ClientStatusService status, NavigationHelperService navigationHelperService)
        {
            Status = status;
            _navigationHelperService = navigationHelperService;

            OnRefreshData = new AsyncRelayCommand(Status.RefreshAsync);
        }

        public void OpenClient(VpnClientActualDataResult client)
        {
            _navigationHelperService.Navigate(NavigationHelperService.ClientDetails, client.Client);
        }
    }
}

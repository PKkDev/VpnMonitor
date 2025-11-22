using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    public class ShellViewModel
    {
        public NavigationHelperService NavigationHelperService { get; init; }

        public ShellViewModel(NavigationHelperService navigationHelperService)
        {
            NavigationHelperService = navigationHelperService;
        }
    }
}

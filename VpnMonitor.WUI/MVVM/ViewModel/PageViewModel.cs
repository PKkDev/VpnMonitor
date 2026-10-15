using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;
using System.Threading.Tasks;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    /// <summary>
    /// Loading indicator and error reporting shared by the pages that read the database.
    /// </summary>
    public abstract class PageViewModel : ObservableObject
    {
        private readonly InfoBarService _infoBarService;

        // The running load: a new one cancels it, e.g. when the period is changed during loading
        private CancellationTokenSource? _loading;

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

        protected PageViewModel(InfoBarService infoBarService)
        {
            _infoBarService = infoBarService;
        }

        /// <summary>
        /// Runs a load, cancelling the previous one.
        /// Cancellation is noticed only at an await, and a query may finish just before it:
        /// a load checks the token after every query, so a superseded load never overwrites the data of the newer one.
        /// </summary>
        protected async Task Run(Func<CancellationToken, Task> load)
        {
            _loading?.Cancel();

            var loading = new CancellationTokenSource();
            _loading = loading;
            IsLoading = true;

            try
            {
                await load(loading.Token);
            }
            catch (Exception) when (loading.IsCancellationRequested)
            {
                // Superseded by a newer load; Npgsql may report the cancelled query with its own exception
            }
            catch (Exception ex)
            {
                _infoBarService.Show($"Не удалось загрузить данные: {DbErrors.Describe(ex)}");
            }
            finally
            {
                // A cancelled load must not hide the indicator of the load that replaced it
                if (_loading == loading)
                {
                    _loading = null;
                    IsLoading = false;
                }

                loading.Dispose();
            }
        }
    }
}

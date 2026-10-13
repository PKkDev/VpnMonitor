using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VpnMonitor.Data.Context;
using VpnMonitor.Data.Statistics;
using VpnMonitor.WUI.MVVM.Model;
using VpnMonitor.WUI.Services;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    public class ActualClientsViewModel : ObservableRecipient
    {
        private DateTime _lastInserted;
        public DateTime LastInserted { get => _lastInserted; set => SetProperty(ref _lastInserted, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        public ObservableCollection<VpnClientActualDataResult> Clients { get; set; }

        public AsyncRelayCommand OnRefreshData { get; set; }

        private readonly DataContext _dataContext;
        private readonly InfoBarService _infoBarService;

        public ActualClientsViewModel(DataContext dataContext, InfoBarService infoBarService)
        {
            _dataContext = dataContext;
            _infoBarService = infoBarService;

            Clients = new();

            OnRefreshData = new AsyncRelayCommand(UpdateClientData);

            OnRefreshData.Execute(null);
        }

        public async Task UpdateClientData()
        {
            IsLoading = true;

            try
            {
                var tempoClients = new List<VpnClientActualDataResult>();

                var start = DateTime.UtcNow.AddMinutes(-10);

                var lastByClient = await _dataContext.VpnClient
                    .GroupBy(x => x.CommonName)
                    .Select(x => new
                    {
                        CommonName = x.Key,
                        LastConnected = x.Max(y => y.ConnectedSince),
                        LastSeen = x.Max(y => y.Inserted),
                    })
                    .ToListAsync();

                var recent = await _dataContext.VpnClient
                    .AsNoTracking()
                    .Where(x => x.Inserted >= start)
                    .ToListAsync();

                var recentByClient = recent.ToLookup(x => x.CommonName);

                foreach (var last in lastByClient)
                {
                    var client = new VpnClientActualDataResult(last.CommonName)
                    {
                        LastConnected = ToLocal(last.LastConnected),
                        LastSeen = ToLocal(last.LastSeen),
                    };

                    var speed = ClientSpeedCalculator.Calculate(recentByClient[last.CommonName]);
                    if (speed != null)
                    {
                        var diff = DateTime.Now - ToLocal(speed.ConnectedSince);

                        client.IsOnline = true;
                        client.LastSend = $"{speed.SentMbit} Mb/s";
                        client.LasrReceived = $"{speed.ReceivedMbit} Mb/s";
                        client.ConnectedTime = Math.Round(diff.TotalMinutes, 2);
                    }

                    tempoClients.Add(client);
                }

                if (lastByClient.Count > 0)
                    LastInserted = ToLocal(lastByClient.Max(x => x.LastSeen));

                Clients = new(tempoClients
                    .OrderByDescending(x => x.IsOnline)
                    .ThenByDescending(x => x.LastSeen));
                OnPropertyChanged(nameof(Clients));
            }
            catch (Exception ex)
            {
                _infoBarService.Show($"Не удалось загрузить данные: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static DateTime ToLocal(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VpnMonitor.Data.Context;
using VpnMonitor.WUI.MVVM.Model;

namespace VpnMonitor.WUI.MVVM.ViewModel
{
    public class ActualClientsViewModel : ObservableRecipient
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        private DateTime _lastInserted;
        public DateTime LastInserted { get => _lastInserted; set => SetProperty(ref _lastInserted, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        public ObservableCollection<VpnClientActualDataResult> Clients { get; set; }

        public RelayCommand OnRefreshData { get; set; }

        private readonly DataContext _dataContext;

        public ActualClientsViewModel(DataContext dataContext)
        {
            _dataContext = dataContext;

            Clients = new();

            OnRefreshData = new RelayCommand(async () =>
            {
                await UpdateClientData();
            });

            OnRefreshData.Execute(null);
        }

        public async Task UpdateClientData()
        {
            _dispatcherQueue.TryEnqueue(() => IsLoading = true);

            var tempoClients = new List<VpnClientActualDataResult>();
            Clients.Clear();

            var now = DateTime.Now.ToUniversalTime();
            var start = now.AddMinutes(-10);

            //using (DataContext db = new DataContext()) { }

            var lastInserted = await _dataContext.VpnClient.MaxAsync(x => x.Inserted);
            LastInserted = TimeZoneInfo.ConvertTimeFromUtc(lastInserted, TimeZoneInfo.Local);
            OnPropertyChanged(nameof(LastInserted));

            var clients = await _dataContext.VpnClient
                 .Where(x => x.Inserted >= start)
                 .ToListAsync();

            if (clients.Count != 0)
            {
                //var ordered1 = clients
                //    .GroupBy(x => x.CommonName)
                //    .Select(x => new
                //    {
                //        CommonName = x.Key,
                //        Data = x.OrderByDescending(x => x.Inserted).ToList(),
                //    });

                var ordered2 = clients
                    .GroupBy(x => x.CommonName)
                    .Select(x => new
                    {
                        CommonName = x.Key,
                        Data = x
                            .GroupBy(y => y.ConnectedSince)
                            .Select(z => new
                            {
                                ConnectedSince = z.Key,
                                Data = z
                                    .OrderByDescending(x => x.Inserted)
                                    .ToList()
                            })
                            .OrderByDescending(c => c.ConnectedSince)
                            .ToList(),
                    });

                foreach (var itemOr in ordered2)
                {
                    var client = new VpnClientActualDataResult(itemOr.CommonName);

                    if (itemOr.Data.Count > 0 && itemOr.Data.First().Data.Count > 1)
                    {
                        var item = itemOr.Data.First();

                        var bytesReceivedD = new List<(double, long)>();
                        var bytesSentD = new List<(double, long)>();

                        for (var i = 0; i < item.Data.Count; i++)
                        {
                            if (i + 1 < item.Data.Count)
                            {
                                var cur = item.Data[i];
                                var prev = item.Data[i + 1];
                                var a = Convert.ToInt64(cur.BytesReceived);
                                var b = Convert.ToInt64(prev.BytesReceived);
                                bytesReceivedD.Add(new(
                                    (cur.Inserted - prev.Inserted).TotalSeconds,
                                    a - b));
                            }
                        }

                        for (var i = 0; i < item.Data.Count; i++)
                        {
                            if (i + 1 < item.Data.Count)
                            {
                                var cur = item.Data[i];
                                var prev = item.Data[i + 1];
                                var a = Convert.ToInt64(cur.BytesSent);
                                var b = Convert.ToInt64(prev.BytesSent);
                                bytesSentD.Add(new(
                                    (cur.Inserted - prev.Inserted).TotalSeconds,
                                    a - b));
                            }
                        }

                        var lastReceived = bytesReceivedD.First().Item2 * 8 / bytesReceivedD.First().Item1;
                        var lastReceivedMbit = Math.Round(lastReceived / 1048576, 2);

                        var lastSent = bytesSentD.First().Item2 * 8 / bytesSentD.First().Item1;
                        var lastSentMbit = Math.Round(lastSent / 1048576, 2);

                        var connectedSince = item.Data.First().ConnectedSince;
                        var connectedSinceAct = TimeZoneInfo.ConvertTimeFromUtc(connectedSince, TimeZoneInfo.Local);
                        var diff = DateTime.Now - connectedSinceAct;

                        client.IsOnline = true;
                        client.LastSend = $"{lastSentMbit} Mb/s";
                        client.LasrReceived = $"{lastReceivedMbit} Mb/s";
                        client.ConnectedTime = Math.Round(diff.TotalMinutes, 2);
                        tempoClients.Add(client);
                    }
                    else
                    {
                        tempoClients.Add(client);
                    }
                }
            }

            var online = tempoClients.Select(x => x.Client).ToList();
            var allClients = await _dataContext.VpnClient.Select(x => x.CommonName).Distinct().ToListAsync();
            foreach (var item in allClients)
            {
                if (!tempoClients.Any(x => x.Client.Equals(item)))
                    tempoClients.Add(new VpnClientActualDataResult(item));
            }


            Clients = new(tempoClients.OrderByDescending(x => x.IsOnline));
            OnPropertyChanged(nameof(Clients));

            _dispatcherQueue.TryEnqueue(() => IsLoading = false);
        }

    }
}

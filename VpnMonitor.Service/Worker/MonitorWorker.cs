using System.Reflection.PortableExecutable;
using VpnMonitor.Data.Context;
using VpnMonitor.Service.Services;

namespace VpnMonitor.Service.Worker
{
    public class MonitorWorker : IDisposable
    {
        private readonly string _connectionString;

        private readonly int _interval = 120;

        private CancellationTokenSource _tokenSource { get; set; }
        private CancellationToken _ct { get; set; }

        private string ipStr = "127.0.0.1";
        private int port = 7505;
        private TelnetClient client { get; set; }
        private VpnClientParser vpnClientParser { get; set; }

        public MonitorWorker(string connectionString)
        {
            _connectionString = connectionString;

            //_interval = 3; 
            //ipStr = "45.79.112.203";
            //port = 4242;

            _tokenSource = new CancellationTokenSource();
            _ct = _tokenSource.Token;

            client = new TelnetClient(ipStr, port);
            vpnClientParser = new VpnClientParser();
        }

        public async Task Start()
        {
            await client.Connect();
            WriteLog("Connected");

            try
            {
                var task = Task.Run(() => Cycle(), _ct);
                await Task.WhenAll(task);
            }
            catch (OperationCanceledException)
            {
                WriteLog($"{nameof(OperationCanceledException)} thrown");
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message.ToString());
                throw;
            }
            finally
            {
                client.Dispose();
            }
        }

        public void Stop()
        {
            _tokenSource.Cancel();
            WriteLog($"Task cancellation requested");
        }

        private void Cycle()
        {
            while (true)
            {
                CheckWork();
                SetInterval();
            }
        }

        private void CheckWork()
        {
            CheckCancel();
            var now = DateTime.Now.ToUniversalTime();
            var response = client.SendCommand("status").GetAwaiter().GetResult();
            var clients = vpnClientParser.ParseClients(response, now);
            Console.WriteLine($"Active clients: {clients.Count} - {string.Join(',', clients.Select(x => x.CommonName))}");

            CheckCancel();
            if (clients.Any())
            {
                using (DataContext db = new DataContext(_connectionString))
                {
                    db.VpnClient.AddRange(clients);
                    db.SaveChanges();
                }
            }
        }

        private void SetInterval()
        {
            var temo = _interval;
            while (temo > 0)
            {
                CheckCancel();
                WriteLog($"Waiting {temo}");
                temo--;
                Thread.Sleep((int)TimeSpan.FromSeconds(1).TotalMilliseconds);
            }
        }

        private void CheckCancel()
        {
            if (_ct.IsCancellationRequested)
            {
                WriteLog($"Task cancelled");
                _ct.ThrowIfCancellationRequested();
            }
        }

        private void WriteLog(string message)
        {
            Console.WriteLine(message);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
using System.Net.Sockets;
using System.Text;

namespace VpnMonitor.Service.Services
{
    public class TelnetClient : IDisposable
    {
        private const string EndOfResponse = "END";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        private TcpClient? _tcpClient;
        private StreamWriter? _writer;
        private StreamReader? _reader;

        private readonly string _host;
        private readonly int _port;

        public TelnetClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task Connect(CancellationToken ct = default)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            _tcpClient = new TcpClient();
            try
            {
                await _tcpClient.ConnectAsync(_host, _port, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"No connection to {_host}:{_port} in {Timeout.TotalSeconds} s");
            }

            var stream = _tcpClient.GetStream();
            _writer = new StreamWriter(stream);
            _reader = new StreamReader(stream);
        }

        /// <summary>
        /// Sends a command whose response is terminated by the END line (such as "status") and returns all lines before it.
        /// </summary>
        public async Task<StringBuilder> SendCommand(string command, CancellationToken ct = default)
        {
            if (_writer == null || _reader == null)
                throw new InvalidOperationException("Not connected");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            StringBuilder stringBuilder = new();

            try
            {
                await _writer.WriteAsync($"{command}\r\n".AsMemory(), timeout.Token);
                await _writer.FlushAsync(timeout.Token);

                while (true)
                {
                    var response = await _reader.ReadLineAsync(timeout.Token);
                    if (response == null)
                        throw new IOException("Connection closed by OpenVPN");

                    if (response == EndOfResponse)
                        return stringBuilder;

                    stringBuilder.AppendLine(response);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"No complete response to \"{command}\" in {Timeout.TotalSeconds} s");
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _tcpClient?.Dispose();
        }
    }

}

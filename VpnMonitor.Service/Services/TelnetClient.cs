using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VpnMonitor.Service.Services
{
    public class TelnetClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private StreamWriter? _writer;
        private StreamReader? _reader;

        private IPEndPoint _endPoint;

        public TelnetClient(string host, int port)
        {
            IPAddress ip = IPAddress.Parse(host);
            _endPoint = new IPEndPoint(ip, port);
        }

        public async Task Connect()
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_endPoint);

            _networkStream = _tcpClient.GetStream();
            _writer = new StreamWriter(_networkStream);
            _reader = new StreamReader(_networkStream);
        }

        public async Task<StringBuilder> SendCommand(string command)
        {
            if (_networkStream == null || _writer == null || _reader == null)
                throw new ArgumentNullException(nameof(_networkStream));

            await _writer.WriteAsync($"{command}\r\n");
            await _writer.FlushAsync();
            Console.WriteLine($"Send: {command}");

            StringBuilder stringBuilder = new();

            await Task.Delay(250);
            var response = await _reader.ReadLineAsync();
            Console.WriteLine($"Receive: {response}");
            stringBuilder.AppendLine(response);
            while (_networkStream.DataAvailable)
            {
                await Task.Delay(250);
                response = await _reader.ReadLineAsync();
                stringBuilder.AppendLine(response);
                Console.WriteLine($"Receive: {response}");
            }

            return stringBuilder;
        }

        public void Dispose()
        {
            _tcpClient?.Close();
            _writer?.Close();
            _reader?.Close();
        }
    }

}

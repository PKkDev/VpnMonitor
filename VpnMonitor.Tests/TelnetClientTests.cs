using System.Net;
using System.Net.Sockets;
using System.Text;
using VpnMonitor.Service.Services;

namespace VpnMonitor.Tests;

public class TelnetClientTests
{
    /// <summary>
    /// Stands in for the OpenVPN management interface: greets, reads one command and writes the response.
    /// </summary>
    private static (TcpListener Listener, Task<string?> Command) StartServer(string response)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var command = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);

            await stream.WriteAsync(Encoding.ASCII.GetBytes(">INFO:OpenVPN Management Interface Version 5\r\n"));
            var received = await reader.ReadLineAsync();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));

            return received;
        });

        return (listener, command);
    }

    private static int Port(TcpListener listener)
    {
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public async Task SendCommand_ReturnsAllLinesBeforeEnd()
    {
        var clientLine = "CLIENT_LIST,bav-all-client,192.0.2.20:58267,10.8.0.6,,1,2,2025-02-09 17:13:11,1739117591,UNDEF,45,1,AES-256-GCM";
        var (listener, command) = StartServer($"TITLE,OpenVPN\r\n{clientLine}\r\nGLOBAL_STATS,dco_enabled,0\r\nEND\r\n");

        using var telnet = new TelnetClient("127.0.0.1", Port(listener));
        await telnet.Connect();
        var response = await telnet.SendCommand("status");
        listener.Stop();

        Assert.Equal("status", await command);

        var lines = response.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            new[] { ">INFO:OpenVPN Management Interface Version 5", "TITLE,OpenVPN", clientLine, "GLOBAL_STATS,dco_enabled,0" },
            lines);
    }

    [Fact]
    public async Task SendCommand_ResponseIsParsedByVpnClientParser()
    {
        var (listener, _) = StartServer(
            "CLIENT_LIST,bav-all-client,192.0.2.20:58267,10.8.0.6,,1,2,2025-02-09 17:13:11,1739117591,UNDEF,45,1,AES-256-GCM\r\nEND\r\n");

        using var telnet = new TelnetClient("127.0.0.1", Port(listener));
        await telnet.Connect();
        var response = await telnet.SendCommand("status");
        listener.Stop();

        var client = Assert.Single(new VpnClientParser().ParseClients(response, DateTime.UtcNow));
        Assert.Equal("bav-all-client", client.CommonName);
    }

    [Fact]
    public async Task SendCommand_WhenConnectionClosedBeforeEnd_Throws()
    {
        var (listener, _) = StartServer("TITLE,OpenVPN\r\n");

        using var telnet = new TelnetClient("127.0.0.1", Port(listener));
        await telnet.Connect();

        await Assert.ThrowsAsync<IOException>(() => telnet.SendCommand("status"));
        listener.Stop();
    }

    [Fact]
    public async Task SendCommand_WithoutConnect_Throws()
    {
        using var telnet = new TelnetClient("127.0.0.1", 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => telnet.SendCommand("status"));
    }
}

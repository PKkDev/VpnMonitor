using System.Text.Json;
using VpnMonitor.Service.Worker;

internal class Program
{
    private static async Task Main(string[] args)
    {
        MonitorWorker monitorWorker = null;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Private.json");
            var root = JsonDocument.Parse(File.ReadAllText(path));
            var config = root.RootElement.GetProperty("NpgSql");
            var connection = config.GetProperty("ConnectionString");
            var connectionString = connection.GetString();

            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("Connection string is not found");

            //new VpnClientParser().ParseClientsTest();
            //new VpnDataParser().View();

            monitorWorker = new MonitorWorker(connectionString);
            await monitorWorker.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            monitorWorker?.Dispose();
        }
    }
}
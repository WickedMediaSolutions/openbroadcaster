using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services.DirectServer;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services.DirectServer;

internal static class Program
{
    static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static DirectServerSnapshot GetSnapshot()
    {
        return new DirectServerSnapshot
        {
            NowPlaying = null,
            Queue = new System.Collections.Generic.List<DirectServerDtos.QueueItem>()
        };
    }

    static System.Collections.Generic.IEnumerable<DirectServerLibraryItem> SearchLibrary(string q, int page, int perPage)
    {
        return Enumerable.Empty<DirectServerLibraryItem>();
    }

    static void OnSongRequest(SongRequest req)
    {
        Console.WriteLine($"Song request received: {req.TrackId} from {req.RequesterName}");
    }

    static async Task<int> Main()
    {
        var port = GetFreePort();
        Console.WriteLine($"Selected free port: {port}");

        var settings = new DirectServerSettings { Enabled = true, Port = port, AllowRemoteConnections = false, ApiKey = string.Empty, EnableCors = true, CorsOrigins = "*" };

        using var server = new DirectHttpServer(settings, getSnapshot: GetSnapshot, searchLibrary: SearchLibrary, onSongRequest: OnSongRequest, getStationName: () => "TestStation");

        try
        {
            server.Start();
            Console.WriteLine($"Server started on {server.ListeningUrl}");

            using var client = new HttpClient();
            var status = await client.GetStringAsync($"{server.ListeningUrl}api/status");
            Console.WriteLine("Status response: " + status);

            // Stop the server
            server.Stop();
            Console.WriteLine("Server stopped");

            // Now try restarting with AllowRemoteConnections=true to exercise '+' binding and fallback
            settings.AllowRemoteConnections = true;
            using var server2 = new DirectHttpServer(settings, getSnapshot: GetSnapshot, searchLibrary: SearchLibrary, onSongRequest: OnSongRequest, getStationName: () => "TestStation");
            try
            {
                server2.Start();
                Console.WriteLine($"Server2 started on {server2.ListeningUrl}");
                var status2 = await client.GetStringAsync($"{server2.ListeningUrl}api/status");
                Console.WriteLine("Status2 response: " + status2);
                server2.Stop();
                Console.WriteLine("Server2 stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server2 failed to start: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Test FAILED: " + ex);
            return 2;
        }

        return 0;
    }
}
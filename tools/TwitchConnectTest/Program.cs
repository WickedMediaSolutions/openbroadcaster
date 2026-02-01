using System;
using System.Threading;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Tools.Fakes;

class Program
{
    static async Task<int> Main()
    {
        Console.WriteLine("Twitch connect test starting...");

        var eventBus = new EventBus();
        var queue = new QueueService();
        var transport = new TransportService(eventBus, queue, null);
        var ledger = new LoyaltyLedger();
        var library = new LibraryService();

        using var fake = new FakeTwitchIrcClient();
        using var twitch = new TwitchIntegrationService(queue, transport, ledger, library, fake);

        var settings = new TwitchSettings
        {
            UserName = "testbot",
            OAuthToken = "oauth:dummy",
            Channel = "#testchannel"
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await twitch.StartAsync(settings, cts.Token);
            Console.WriteLine("StartAsync completed (fake connect)");

            // Wait briefly to allow timers/tasks to start
            await Task.Delay(200);

            await twitch.StopAsync();
            Console.WriteLine("StopAsync completed");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Twitch connect test FAILED: " + ex);
            return 2;
        }
    }
}

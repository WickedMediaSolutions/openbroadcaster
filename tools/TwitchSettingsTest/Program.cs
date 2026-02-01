using System;
using System.Threading.Tasks;
using OpenBroadcaster.Core.Services;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Messaging;

class Program
{
    static async Task<int> Main()
    {
        Console.WriteLine("Twitch settings test starting...");

        var eventBus = new EventBus();
        var queue = new QueueService();
        var transport = new TransportService(eventBus, queue, null);
        var ledger = new LoyaltyLedger();
        var library = new LibraryService();

        using var twitch = new TwitchIntegrationService(queue, transport, ledger, library);

        try
        {
            twitch.UpdateSettings(new TwitchSettings { Channel = "", OAuthToken = "" });
            twitch.UpdateRequestSettings(new RequestSettings { MaxRequestsPerUser = 2 });
            Console.WriteLine("UpdateSettings and UpdateRequestSettings succeeded.");

            // StopAsync when not connected should be a no-op
            await twitch.StopAsync();
            Console.WriteLine("StopAsync (not connected) completed.");

            // StartAsync with invalid settings should throw
            var invalid = new TwitchSettings { Channel = "", OAuthToken = "" };
            try
            {
                await twitch.StartAsync(invalid, default);
                Console.WriteLine("StartAsync unexpectedly succeeded with invalid settings.");
                return 2;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("StartAsync correctly rejected invalid settings.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Twitch settings test FAILED: " + ex);
            return 3;
        }
    }
}

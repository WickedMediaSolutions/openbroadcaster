using System;
using OpenBroadcaster.Core.Streaming;
using OpenBroadcaster.Core.Models;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Encoder config test starting...");
        using var mgr = new EncoderManager();
        var settings = new EncoderSettings();
        // Add a disabled test profile (safe)
        settings.Profiles.Add(new EncoderProfile { Name = "TestProfile", Enabled = false });
        try
        {
            mgr.UpdateConfiguration(settings, -1);
            Console.WriteLine("UpdateConfiguration succeeded.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("UpdateConfiguration FAILED: " + ex);
            return 2;
        }
    }
}

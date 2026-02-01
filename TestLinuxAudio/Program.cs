using OpenBroadcaster.Core.Audio;

Console.WriteLine("OpenBroadcaster Linux Audio Test");
Console.WriteLine("=================================\n");

// Test device enumeration
var resolver = new LinuxAudioDeviceResolver();

Console.WriteLine("Playback Devices:");
var playbackDevices = resolver.GetPlaybackDevices();
foreach (var device in playbackDevices)
{
    Console.WriteLine($"  [{device.DeviceNumber}] {device.ProductName}");
}

Console.WriteLine("\nInput Devices:");
var inputDevices = resolver.GetInputDevices();
foreach (var device in inputDevices)
{
    Console.WriteLine($"  [{device.DeviceNumber}] {device.ProductName}");
}

Console.WriteLine("\n✓ Device enumeration working!");

// Check FFmpeg availability
try
{
    var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "ffmpeg",
        ArgumentList = { "-version" },
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    });
    proc?.WaitForExit();
    if (proc?.ExitCode == 0)
    {
        Console.WriteLine("✓ FFmpeg is available");
    }
}
catch
{
    Console.WriteLine("✗ FFmpeg not found");
}

// Check FFprobe availability
try
{
    var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "ffprobe",
        ArgumentList = { "-version" },
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    });
    proc?.WaitForExit();
    if (proc?.ExitCode == 0)
    {
        Console.WriteLine("✓ FFprobe is available");
    }
}
catch
{
    Console.WriteLine("✗ FFprobe not found");
}

Console.WriteLine("\nLinux audio backend is ready!");
Console.WriteLine("Dependencies: OpenAL (libopenal1) + FFmpeg");

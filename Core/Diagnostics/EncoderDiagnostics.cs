using System;
using System.IO;
using System.Text;
using OpenBroadcaster.Core.Models;

namespace OpenBroadcaster.Core.Diagnostics
{
    public static class EncoderDiagnostics
    {
        private static readonly object SyncRoot = new();
        private const string EncoderLogFileName = "encoder-errors.log";

        public static string RecordException(EncoderProfile profile, Exception exception)
        {
            var directory = Path.Combine(AppLogger.LogDirectory, "encoder");
            Directory.CreateDirectory(directory);
            var logFile = Path.Combine(directory, EncoderLogFileName);
            var builder = new StringBuilder();
            builder.AppendLine(new string('-', 80));
            builder.Append("Timestamp: ").AppendLine(DateTime.UtcNow.ToString("u"));
            builder.Append("Profile: ").Append(profile.Name).Append(" (ID: ").Append(profile.Id).AppendLine(")");
            builder.Append("Target: ")
                .Append(string.IsNullOrWhiteSpace(profile.Host) ? "localhost" : profile.Host)
                .Append(":")
                .Append(profile.Port)
                .Append(" ")
                .Append(profile.Protocol)
                .AppendLine();
            builder.Append("Format: ").Append(profile.Format).Append(" @ ").Append(profile.BitrateKbps).AppendLine(" kbps");
            builder.AppendLine("Exception:");
            builder.AppendLine(exception.ToString());

            lock (SyncRoot)
            {
                File.AppendAllText(logFile, builder.ToString());
            }

            return logFile;
        }
    }
}

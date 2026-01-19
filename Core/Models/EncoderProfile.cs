using System;

namespace OpenBroadcaster.Core.Models
{
    public enum EncoderFormat
    {
        Mp3 = 0
    }

    public enum EncoderProtocol
    {
        Icecast = 0,
        Shoutcast = 1
    }

    public sealed class EncoderProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 8000;
        public string Mount { get; set; } = "/live";
        public string Username { get; set; } = "source";
        public string Password { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool UseSsl { get; set; }
        public int BitrateKbps { get; set; } = 256;
        public EncoderFormat Format { get; set; } = EncoderFormat.Mp3;
        public EncoderProtocol Protocol { get; set; } = EncoderProtocol.Icecast;
        public string Description { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public bool Public { get; set; }
        public string AdminUser { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }
}

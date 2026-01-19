using System.Collections.Generic;
using Xunit;

namespace OpenBroadcaster.Tests.Streaming
{
    public sealed class EncoderManagerHarness
    {
        [Fact]
        public void HarnessDefinesDefaultEndpoints()
        {
            var harness = BuildProfiles();
            Assert.Equal(2, harness.Count);
            Assert.Contains(harness, profile => profile.Name == "Primary Shoutcast");
            Assert.Contains(harness, profile => profile.Format == "MP3" && profile.BitrateKbps == 256);
        }

        [Fact(Skip = "Encoder manager pending Phase 6 implementation")]
        public void EncoderManager_ConnectsToAllConfiguredEndpoints()
        {
        }

        public IList<EncoderProfile> BuildProfiles()
        {
            return new List<EncoderProfile>
            {
                new EncoderProfile
                {
                    Name = "Primary Shoutcast",
                    Host = "stream1.example.com",
                    Port = 8000,
                    Mount = "/live",
                    Format = "MP3",
                    BitrateKbps = 256,
                    Username = "source",
                    Password = "secret"
                },
                new EncoderProfile
                {
                    Name = "Backup Icecast",
                    Host = "backup.example.net",
                    Port = 8444,
                    Mount = "/fallback",
                    Format = "MP3",
                    BitrateKbps = 192,
                    Username = "dj",
                    Password = "backup"
                }
            };
        }
    }

    public sealed class EncoderProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 8000;
        public string Mount { get; set; } = string.Empty;
        public string Format { get; set; } = "MP3";
        public int BitrateKbps { get; set; } = 256;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

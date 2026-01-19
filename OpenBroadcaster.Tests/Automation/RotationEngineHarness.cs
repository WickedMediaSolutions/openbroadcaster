using System.Collections.Generic;
using System.Linq;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Models;
using Xunit;

namespace OpenBroadcaster.Tests.Automation
{
    public sealed class RotationEngineHarness
    {
        [Fact]
        public void HarnessProvidesSeedCategories()
        {
            var scenario = BuildScenario();
            Assert.True(scenario.Categories.Count >= 3);
            Assert.All(scenario.Categories.Values, bucket => Assert.NotEmpty(bucket));
        }

        [Fact]
        public void RotationEngine_HonorsArtistSeparation()
        {
            var scenario = BuildScenario();
            var engine = new RotationEngine(new RotationRules
            {
                MinArtistSeparation = 1,
                MinTitleSeparation = 1,
                HistoryLimit = 10
            });

            var definitions = new List<RotationCategoryDefinition>();
            foreach (var bucket in scenario.Categories)
            {
                definitions.Add(new RotationCategoryDefinition(bucket.Key, bucket.Value));
            }

            engine.LoadCategories(definitions);

            var played = new List<Track>();
            for (var i = 0; i < 3; i++)
            {
                var next = engine.NextTrack("Hot AC");
                Assert.NotNull(next);
                played.Add(next!);
            }

            Assert.True(played.Count >= 2);
            for (var i = 1; i < played.Count; i++)
            {
                Assert.NotEqual(played[i - 1].Artist, played[i].Artist);
            }
        }

        public RotationHarnessScenario BuildScenario()
        {
            var buckets = new Dictionary<string, List<Track>>
            {
                ["Hot AC"] = new List<Track>
                {
                    CreateTrack("Satellite", "WinAmp Society"),
                    CreateTrack("Satellite Remix", "WinAmp Society"),
                    CreateTrack("Blue Pulse", "Deck Ninety")
                },
                ["Night"] = new List<Track>
                {
                    CreateTrack("Night Drive", "FM Skyline"),
                    CreateTrack("City Pop", "Reverb Club")
                },
                ["Imaging"] = new List<Track>
                {
                    CreateTrack("Station ID 7", "OBR"),
                    CreateTrack("Promo Weekend", "OBR Promo")
                }
            };

            return new RotationHarnessScenario(buckets);
        }

        private static Track CreateTrack(string title, string artist)
        {
            return new Track(title, artist, "Harness", "Test", 2024, System.TimeSpan.FromMinutes(3));
        }
    }

    public sealed record RotationHarnessScenario(IReadOnlyDictionary<string, List<Track>> Categories)
    {
        public IReadOnlyList<Track> AllTracks => Categories.Values.SelectMany(static bucket => bucket).ToList();
    }
}

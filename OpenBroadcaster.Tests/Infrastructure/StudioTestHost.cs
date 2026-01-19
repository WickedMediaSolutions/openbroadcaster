using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using OpenBroadcaster.Core.Messaging;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;

namespace OpenBroadcaster.Tests.Infrastructure
{
    public sealed class StudioTestHost : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        private readonly EventHandler<TwitchChatMessage> _chatHandler;

        public StudioTestHost()
        {
            EventBus = new EventBus();
            QueueService = new QueueService();
            TransportService = new TransportService(EventBus, QueueService);
            LoyaltyLedger = new LoyaltyLedger(CreateTempFilePath("loyalty"));
            LibraryService = new LibraryService(CreateTempFilePath("library"));
            TwitchClient = new TwitchIrcClient(NullLogger<TwitchIrcClient>.Instance);
            TwitchService = new TwitchIntegrationService(
                QueueService,
                TransportService,
                LoyaltyLedger,
                LibraryService,
                TwitchClient,
                NullLogger<TwitchIntegrationService>.Instance);

            _chatHandler = (_, message) => ChatMessages.Add(message);
            TwitchService.ChatMessageReceived += _chatHandler;
        }

        public IEventBus EventBus { get; }
        public QueueService QueueService { get; }
        public TransportService TransportService { get; }
        public LoyaltyLedger LoyaltyLedger { get; }
        public LibraryService LibraryService { get; }
        public TwitchIrcClient TwitchClient { get; }
        public TwitchIntegrationService TwitchService { get; }
        public IList<TwitchChatMessage> ChatMessages { get; } = new List<TwitchChatMessage>();

        public TwitchSettings CreateDefaultTwitchSettings()
        {
            return new TwitchSettings
            {
                UserName = "openbot",
                OAuthToken = "oauth:test-token",
                Channel = "openbroadcaster",
                PointsName = "Credits",
                RequestCost = 5,
                PlayNextCost = 25
            };
        }

        public Track CreateTrack(string title)
        {
            return new Track(title, "Test Artist", "Test Album", "Test", 2024, TimeSpan.FromMinutes(3));
        }

        public Track AddLibraryTrack(string title, string? artist = null)
        {
            var track = new Track(title, artist ?? "Test Artist", "Test Album", "Test", 2024, TimeSpan.FromMinutes(3));
            return LibraryService.SaveTrack(track);
        }

        private string CreateTempFilePath(string prefix)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.json");
            _tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            TwitchService.ChatMessageReceived -= _chatHandler;
            TwitchService.Dispose();
            TwitchClient.Dispose();

            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                }
            }
        }
    }
}

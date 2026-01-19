using System.Collections.Generic;
using OpenBroadcaster.Core.Automation;
using OpenBroadcaster.Core.Models;
using OpenBroadcaster.Core.Services;
using Xunit;

namespace OpenBroadcaster.Tests
{
    public sealed class SimpleAutoDjServiceTests
    {
        [Fact]
        public void Enabled_FillsQueueToTargetDepth()
        {
            var queue = new QueueService();
            var library = new LibraryService();
            // Ensure the 'Library' category exists
            var category = library.GetCategories().FirstOrDefault(c => c.Name == "Library");
            if (category == null)
            {
                category = new LibraryCategory("Library", "General");
                var categoriesField = typeof(LibraryService).GetField("_categories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var categoriesDict = (Dictionary<Guid, LibraryCategory>?)categoriesField?.GetValue(library);
                if (categoriesDict != null)
                {
                    categoriesDict[category.Id] = category;
                }
            }
            // Create a track associated with the correct category ID
            var track = new Track("Test Song", "Artist", "Album", "Genre", 2026, System.TimeSpan.FromMinutes(3), null, true, new[] { category.Id });
            library.SaveTrack(track);
            var rotation = new SimpleRotation { Name = "Default", Enabled = true, CategoryNames = new List<string> { "Library" } };
            var service = new SimpleAutoDjService(queue, library, new List<SimpleRotation> { rotation }, new List<SimpleSchedulerEntry>(), 2, rotation.Id);
            service.Enabled = true;
            service.EnsureQueueDepth();
            var snapshot = queue.Snapshot();
            Assert.Equal(5, snapshot.Count);
            foreach (var item in snapshot)
            {
                Assert.Equal(QueueSource.AutoDj, item.SourceType);
            }
        }
    }
}

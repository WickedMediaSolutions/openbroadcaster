using System;
using System.Text.Json.Serialization;

namespace OpenBroadcaster.Core.Models
{
    public sealed class LibraryCategory
    {
        public LibraryCategory(string name, string type)
            : this(Guid.NewGuid(), name, type)
        {
        }

        [JsonConstructor]
        public LibraryCategory(Guid id, string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Category name is required.", nameof(name));
            }

            Id = id == Guid.Empty ? Guid.NewGuid() : id;
            Name = name.Trim();
            Type = string.IsNullOrWhiteSpace(type) ? "General" : type.Trim();
        }

        public Guid Id { get; }
        public string Name { get; }
        public string Type { get; }

        public LibraryCategory WithMetadata(string name, string type)
        {
            return new LibraryCategory(Id, name, type);
        }
    }
}

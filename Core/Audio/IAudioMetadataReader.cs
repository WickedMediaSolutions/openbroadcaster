namespace OpenBroadcaster.Core.Audio
{
    public interface IAudioMetadataReader
    {
        LibraryTrackMetadata ReadMetadata(string filePath);
    }
}

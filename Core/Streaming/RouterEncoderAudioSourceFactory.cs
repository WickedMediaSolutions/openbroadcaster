using OpenBroadcaster.Core.Audio;

namespace OpenBroadcaster.Core.Streaming
{
    public sealed class RouterEncoderAudioSourceFactory : IEncoderAudioSourceFactory
    {
        private readonly SharedEncoderAudioSource _sharedSource;

        public RouterEncoderAudioSourceFactory(SharedEncoderAudioSource sharedSource)
        {
            _sharedSource = sharedSource;
        }

        public IEncoderAudioSource Create(int deviceNumber)
        {
            return _sharedSource;
        }
    }
}

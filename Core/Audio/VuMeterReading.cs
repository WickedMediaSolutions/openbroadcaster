namespace OpenBroadcaster.Core.Audio
{
    public readonly struct VuMeterReading
    {
        public VuMeterReading(double program, double encoder, double mic)
        {
            Program = Clamp(program);
            Encoder = Clamp(encoder);
            Mic = Clamp(mic);
        }

        public double Program { get; }
        public double Encoder { get; }
        public double Mic { get; }

        private static double Clamp(double value)
        {
            if (double.IsNaN(value))
            {
                return 0;
            }

            if (value < 0)
            {
                return 0;
            }

            if (value > 1)
            {
                return 1;
            }

            return value;
        }
    }
}

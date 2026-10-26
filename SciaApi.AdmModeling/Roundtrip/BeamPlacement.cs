namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Free space, in metres, for the two parallel roundtrip test beams.
    /// </summary>
    public sealed class BeamPlacement
    {
        public BeamPlacement(double startX, double endX, double firstBeamY, double secondBeamY, double z)
        {
            StartX = startX;
            EndX = endX;
            FirstBeamY = firstBeamY;
            SecondBeamY = secondBeamY;
            Z = z;
        }

        public double StartX { get; }

        public double EndX { get; }

        public double FirstBeamY { get; }

        public double SecondBeamY { get; }

        public double Z { get; }
    }
}

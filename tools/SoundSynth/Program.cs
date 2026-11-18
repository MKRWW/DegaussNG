namespace DegaussNG.SoundSynth;

internal static class Program
{
    private static int Main(string[] args)
    {
        string directory = args.Length > 0 ? args[0] : "assets/sounds";
        string path = Path.Combine(directory, "degauss.wav");

        WavWriter.WriteFile(path, DegaussSound.Render(), DegaussSound.SampleRate);

        return 0;
    }
}

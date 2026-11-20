namespace DegaussNG.SoundSynth;

internal static class Program
{
    private static int Main(string[] args)
    {
        string directory = args.Length > 0 ? args[0] : "assets/sounds";

        WavWriter.WriteFile(Path.Combine(directory, "degauss.wav"), DegaussSound.Render(), DegaussSound.SampleRate);
        WavWriter.WriteFile(Path.Combine(directory, "modem-short.wav"), ModemSound.RenderShort(), ModemSound.SampleRate);
        WavWriter.WriteFile(Path.Combine(directory, "modem-full.wav"), ModemSound.RenderFull(), ModemSound.SampleRate);

        return 0;
    }
}

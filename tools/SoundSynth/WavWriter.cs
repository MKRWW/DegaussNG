namespace DegaussNG.SoundSynth;

/// <summary>
/// Writes the rendered samples as an uncompressed PCM WAV file.
/// </summary>
public static class WavWriter
{
    private const int HeaderLength = 44;

    public static byte[] Build(short[] samples, int sampleRate)
    {
        int dataLength = samples.Length * 2;
        byte[] bytes = new byte[HeaderLength + dataLength];

        WriteString(bytes, 0, "RIFF");
        WriteInt32(bytes, 4, 36 + dataLength);
        WriteString(bytes, 8, "WAVE");

        WriteString(bytes, 12, "fmt ");
        WriteInt32(bytes, 16, 16);
        WriteInt16(bytes, 20, 1);                  // PCM
        WriteInt16(bytes, 22, 1);                  // mono
        WriteInt32(bytes, 24, sampleRate);
        WriteInt32(bytes, 28, sampleRate * 2);     // bytes per second
        WriteInt16(bytes, 32, 2);                  // block align
        WriteInt16(bytes, 34, 16);                 // bits per sample

        WriteString(bytes, 36, "data");
        WriteInt32(bytes, 40, dataLength);

        for (int i = 0; i < samples.Length; i++)
        {
            short value = samples[i];
            bytes[HeaderLength + i * 2] = (byte)(value & 0xFF);
            bytes[HeaderLength + i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return bytes;
    }

    public static void WriteFile(string path, short[] samples, int sampleRate)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, Build(samples, sampleRate));
    }

    private static void WriteString(byte[] bytes, int offset, string value)
    {
        for (int i = 0; i < 4; i++)
        {
            bytes[offset + i] = (byte)value[i];
        }
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value & 0xFF);
        bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value & 0xFF);
        bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}

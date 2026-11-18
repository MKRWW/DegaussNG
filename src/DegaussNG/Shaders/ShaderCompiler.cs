using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DegaussNG.Shaders;

/// <summary>
/// Compiles the embedded HLSL source once at runtime through D3DCompile and caches the bytes.
/// There is no build step and no compiled shader in the repo.
/// </summary>
internal static class ShaderCompiler
{
    private const string DllName = "d3dcompiler_47.dll";
    private const string EntryPoint = "main";
    private const string Target = "ps_3_0";
    private const string ResourceName = "DegaussNG.Shaders.Degauss.hlsl";
    private const int S_OK = 0;

    private static readonly object Gate = new object();

    private static byte[]? _cached;
    private static bool _attempted;
    private static string? _lastError;

    public static string? LastError => _lastError;

    public static byte[]? Compile()
    {
        lock (Gate)
        {
            if (_attempted)
            {
                return _cached;
            }

            _attempted = true;

            try
            {
                Stream? source = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
                if (source == null)
                {
                    _lastError = "the embedded shader source is missing";
                    return null;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(ReadAllText(source));

                int result = D3DCompile(
                    bytes,
                    (UIntPtr)bytes.Length,
                    "Degauss.hlsl",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    EntryPoint,
                    Target,
                    0,
                    0,
                    out ID3DBlob? code,
                    out ID3DBlob? errors);

                if (result != S_OK)
                {
                    _lastError = errors != null ? ReadText(errors) : "D3DCompile failed with 0x" + result.ToString("X8");
                    return null;
                }

                if (code == null)
                {
                    _lastError = "D3DCompile returned no shader bytes";
                    return null;
                }

                _cached = ReadBytes(code);
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
            }

            return _cached;
        }
    }

    private static string ReadAllText(Stream stream)
    {
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(ID3DBlob blob)
    {
        byte[] bytes = new byte[(int)blob.GetBufferSize()];
        Marshal.Copy(blob.GetBufferPointer(), bytes, 0, bytes.Length);
        return bytes;
    }

    private static string ReadText(ID3DBlob blob)
    {
        return Encoding.UTF8.GetString(ReadBytes(blob));
    }

    [DllImport(DllName, EntryPoint = "D3DCompile", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int D3DCompile(
        byte[] srcData,
        UIntPtr srcDataSize,
        string? sourceName,
        IntPtr defines,
        IntPtr include,
        string entryPoint,
        string target,
        uint flags1,
        uint flags2,
        out ID3DBlob? code,
        out ID3DBlob? errorMessages);

    [ComImport]
    [Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3DBlob
    {
        [PreserveSig]
        IntPtr GetBufferPointer();

        [PreserveSig]
        UIntPtr GetBufferSize();
    }
}

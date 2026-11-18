using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DegaussNG.Core;
using DegaussNG.Shaders;

namespace DegaussNG;

/// <summary>
/// One degauss run: capture first, then show the overlays and drive the effect from
/// the render loop. Everything is closed and released in a finally path, and a watchdog
/// force-closes the overlays after five seconds.
/// </summary>
internal sealed class DegaussRunner
{
    private const double WatchdogSeconds = 5.0;

    private readonly RunGate _gate;
    private readonly SoundPlayer? _player;
    private readonly List<OverlayWindow> _windows = new();
    private readonly List<BitmapSource> _bitmaps = new();
    private readonly Stopwatch _stopwatch = new();

    private AppConfig _config;
    private TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _forceClosed;

    public DegaussRunner(RunGate gate, AppConfig config, SoundPlayer? player)
    {
        _gate = gate;
        _config = config.Normalized();
        _player = player;
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config.Normalized();
    }

    public void Run()
    {
        _ = RunAsync();
    }

    /// <summary>
    /// Returns true when the effect was shown on at least one monitor and ran to its end.
    /// </summary>
    public async Task<bool> RunAsync()
    {
        // A trigger during a running effect is ignored.
        if (!_gate.TryEnter())
        {
            return false;
        }

        _forceClosed = false;
        _closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        DispatcherTimer watchdog = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(WatchdogSeconds),
        };

        watchdog.Tick += (_, _) => CloseOverlays();
        watchdog.Start();

        try
        {
            byte[]? shaderBytes = ShaderCompiler.Compile();

            if (shaderBytes != null && RenderCapability.IsPixelShaderVersionSupported(3, 0))
            {
                PixelShader pixelShader = new PixelShader();
                pixelShader.SetStreamSource(new MemoryStream(shaderBytes));

                foreach (MonitorCapture capture in ScreenCapture.Capture())
                {
                    _bitmaps.Add(capture.Image);

                    OverlayWindow window = new OverlayWindow(capture, pixelShader);
                    _windows.Add(window);
                    window.Show();
                }
            }
            else
            {
                Log.Error("overlay skipped: " + (ShaderCompiler.LastError ?? "pixel shader version 3.0 is not supported"));
            }

            // The clock starts with the sound, so picture and thump stay in step.
            _stopwatch.Restart();
            _player?.Play(_config.Volume);

            CompositionTarget.Rendering += OnRendering;

            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(DegaussEnvelope.TotalSeconds)), _closed.Task);

            return _windows.Count > 0 && !_forceClosed;
        }
        catch (Exception exception)
        {
            Log.Error("run failed: " + exception.Message);
            return false;
        }
        finally
        {
            watchdog.Stop();
            CompositionTarget.Rendering -= OnRendering;

            CloseOverlays();
            _bitmaps.Clear();
            _windows.Clear();

            _gate.Exit();
        }
    }

    public void CloseOverlays()
    {
        _forceClosed = true;
        _closed.TrySetResult();

        foreach (OverlayWindow window in _windows)
        {
            try
            {
                window.Close();
            }
            catch (Exception exception)
            {
                Log.Error("overlay close failed: " + exception.Message);
            }
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        try
        {
            DegaussFrame frame = DegaussEnvelope.At(_stopwatch.Elapsed.TotalSeconds, _config.ReducedMotion);

            foreach (OverlayWindow window in _windows)
            {
                window.Apply(frame);
            }
        }
        catch (Exception exception)
        {
            Log.Error("render failed: " + exception.Message);
            CloseOverlays();
        }
    }
}

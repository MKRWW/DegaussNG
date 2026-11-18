using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DegaussNG.Core;

namespace DegaussNG;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "DegaussNG.SingleInstance";

    private Mutex? _mutex;
    private TrayController? _tray;
    private int? _selfTestExitCode;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Dispatcher.UnhandledException += OnDispatcherUnhandledException;

        if (StartSelfTest(e.Args))
        {
            return;
        }

        _mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // A second start exits silently.
            Shutdown();
            return;
        }

        _tray = new TrayController(new ConfigStore(ConfigStore.DefaultPath));
        _tray.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_selfTestExitCode is int exitCode)
        {
            e.ApplicationExitCode = exitCode;
            Environment.ExitCode = exitCode;
        }

        _tray?.Dispose();
        _mutex?.Dispose();

        base.OnExit(e);
    }

    private bool StartSelfTest(string[] args)
    {
        if (args.Length < 3 || args[0] != "--selftest")
        {
            return false;
        }

        if (!int.TryParse(args[1], out int cycles) || cycles <= 0)
        {
            return false;
        }

        string logPath = args[2];

        // The self-test runs without tray, hotkey, sound or config access and ignores the mutex.
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            _selfTestExitCode = await SelfTest.Run(cycles, logPath);

            Shutdown();
        }));

        return true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("unhandled exception: " + e.ExceptionObject);
        _tray?.CloseOverlays();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("unhandled exception: " + e.Exception.Message);
        _tray?.CloseOverlays();

        e.Handled = true;
    }
}

/// <summary>
/// Appends to %LOCALAPPDATA%\DegaussNG\degauss.log.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new object();

    public static void Error(string message)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DegaussNG");

            Directory.CreateDirectory(directory);

            lock (Gate)
            {
                File.AppendAllText(
                    Path.Combine(directory, "degauss.log"),
                    DateTime.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Logging must never fail the run.
        }
    }
}

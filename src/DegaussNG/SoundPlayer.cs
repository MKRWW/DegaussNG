using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DegaussNG.Core;
using NAudio.Wave;

namespace DegaussNG;

/// <summary>
/// Plays the embedded WAV. Every NAudio object is disposed when playback stops,
/// and a missing audio device is logged instead of failing the run.
/// </summary>
internal sealed class SoundPlayer
{
    private const string ResourceName = "DegaussNG.assets.sounds.degauss.wav";
    private const string ShortModemResourceName = "DegaussNG.assets.sounds.modem-short.wav";
    private const string FullModemResourceName = "DegaussNG.assets.sounds.modem-full.wav";

    // A short output buffer keeps the thump in step with the picture.
    private const int DesiredLatencyMilliseconds = 120;

    private readonly List<Session> _sessions = new();

    private Session? _modem;

    public void Play(double volume)
    {
        Session? session = null;

        try
        {
            session = OpenSession(ResourceName);
            if (session == null)
            {
                return;
            }

            Start(session, volume);
        }
        catch (Exception exception)
        {
            if (session != null)
            {
                _sessions.Remove(session);
                session.Dispose();
            }

            Log.Error("audio playback failed: " + exception.Message);
        }
    }

    /// <summary>
    /// Plays the modem sound. While one is running, a further call does nothing.
    /// </summary>
    public void PlayModem(string mode, double volume)
    {
        if (_modem != null)
        {
            return;
        }

        Session? session = null;

        try
        {
            string resource = mode == ModemModes.Full ? FullModemResourceName : ShortModemResourceName;

            session = OpenSession(resource);
            if (session == null)
            {
                return;
            }

            _modem = session;
            Start(session, volume);
        }
        catch (Exception exception)
        {
            if (session != null)
            {
                _sessions.Remove(session);
                session.Dispose();
            }

            _modem = null;
            Log.Error("audio playback failed: " + exception.Message);
        }
    }

    /// <summary>
    /// Stops the running modem sound. Without one it does nothing.
    /// </summary>
    public void StopModem()
    {
        if (_modem == null)
        {
            return;
        }

        Session session = _modem;
        _modem = null;

        session.Player.Stop();
    }

    private Session? OpenSession(string resourceName)
    {
        Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Log.Error("the embedded sound is missing");
            return null;
        }

        return new Session(new WaveFileReader(stream));
    }

    private void Start(Session session, double volume)
    {
        session.Player.PlaybackStopped += OnPlaybackStopped;
        _sessions.Add(session);

        session.Player.DesiredLatency = DesiredLatencyMilliseconds;
        session.Player.Init(session.Reader);
        session.Player.Volume = (float)volume;
        session.Player.Play();
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Session? session = _sessions.FirstOrDefault(item => ReferenceEquals(item.Player, sender));
        if (session == null)
        {
            return;
        }

        _sessions.Remove(session);

        if (ReferenceEquals(session, _modem))
        {
            _modem = null;
        }

        session.Dispose();
    }

    private sealed class Session : IDisposable
    {
        public Session(WaveFileReader reader)
        {
            Reader = reader;
            Player = new WaveOutEvent();
        }

        public WaveFileReader Reader { get; }

        public WaveOutEvent Player { get; }

        public void Dispose()
        {
            Player.Dispose();
            Reader.Dispose();
        }
    }
}

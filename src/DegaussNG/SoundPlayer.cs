using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NAudio.Wave;

namespace DegaussNG;

/// <summary>
/// Plays the embedded WAV. Every NAudio object is disposed when playback stops,
/// and a missing audio device is logged instead of failing the run.
/// </summary>
internal sealed class SoundPlayer
{
    private const string ResourceName = "DegaussNG.assets.sounds.degauss.wav";

    private readonly List<Session> _sessions = new();

    public void Play(double volume)
    {
        Session? session = null;

        try
        {
            Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                Log.Error("the embedded sound is missing");
                return;
            }

            session = new Session(new WaveFileReader(stream));
            session.Player.PlaybackStopped += OnPlaybackStopped;
            _sessions.Add(session);

            session.Player.Init(session.Reader);
            session.Player.Volume = (float)volume;
            session.Player.Play();
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

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Session? session = _sessions.FirstOrDefault(item => ReferenceEquals(item.Player, sender));
        if (session == null)
        {
            return;
        }

        _sessions.Remove(session);
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

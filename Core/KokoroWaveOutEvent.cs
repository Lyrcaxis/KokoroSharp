namespace KokoroSharp.Core;

using NAudio.Wave;

using OpenTK.Audio.OpenAL;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using System.Diagnostics;

/// <summary> Base class for cross platform audio playback, with API mostly compatible with NAudio's <see cref="WaveOutEvent"/> API. </summary>
/// <remarks> Each platform (Windows/Linux/MacOS) derives from this to expose a nice interface back to KokoroSharp. </remarks>
public abstract class KokoroWaveOutEvent
{
    public RawSourceWaveStream stream { get; protected set; }

    /// <summary> The state of the playback (Playing/Stopped). </summary>
    public abstract PlaybackState PlaybackState { get; }

    /// <summary> Initializes the audio buffer with an audio stream. </summary>
    public void Init(RawSourceWaveStream stream) => this.stream = stream;

    /// <summary> Begins playing back the audio stream this instance was initialized with. </summary>
    public abstract void Play();

    /// <summary> Immediately stops the playback. Does not touch the 'stream' though. </summary>
    public abstract void Stop();

    /// <summary> Adjust the volume of the playback. [0.0, to 1.0] </summary>
    public abstract void SetVolume(float volume);

    /// <summary> Disposes the instance, freeing up any memory or threads it uses. </summary>
    public abstract void Dispose();

    /// <summary> Gets the percentage of how much audio has already been played back. </summary>
    /// <remarks> NOTE that for non-windows platforms, this is an approximate. </remarks>
    public virtual float CurrentPercentage => stream.Position / (float)stream.Length;

    /// <summary> Pause not supported for simplicity. </summary>
    public void Pause() => throw new NotImplementedException("We're not gonna support this.");
}

// A wrapper for NAudio's WaveOutEvent.
public class WindowsAudioPlayer : KokoroWaveOutEvent
{
    readonly WaveOutEvent waveOut = new();
    public override PlaybackState PlaybackState => waveOut.PlaybackState;
    public override void Dispose() => waveOut.Dispose();
    public override void Play() { waveOut.Init(stream); waveOut.Play(); }
    public override void SetVolume(float volume) => waveOut.Volume = volume;
    public override void Stop() => waveOut.Stop();
}

public class MacOSAudioPlayer : LinuxAudioPlayer { }

public class LinuxAudioPlayer : KokoroWaveOutEvent
{
    private static readonly MiniAudioEngine _audioEngine = new(KokoroPlayback.waveFormat.SampleRate,
        SoundFlow.Enums.Capability.Playback);

    private bool _isDisposed = false;
    private SoundPlayer _player = null!;

    private PlaybackState _pState = PlaybackState.Stopped;

    public override PlaybackState PlaybackState => _pState;

    public new void Init(RawSourceWaveStream input)
    {
        stream = input;
        _player = new SoundPlayer(new AssetDataProvider(stream));
    }

    public override void Play()
    {
        _player.Play();
        _pState = PlaybackState.Playing;
    }

    public override void Stop()
    {
        _player.Stop();
        _pState = PlaybackState.Stopped;
    }

    public override void SetVolume(float volume)
    {
        _player.Volume = volume;
    }

    public override void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _player?.Stop();
        _pState = PlaybackState.Stopped;

        //_audioEngine.Dispose(); // Can we manage this lifetime better?
        _isDisposed = true;
    }
}

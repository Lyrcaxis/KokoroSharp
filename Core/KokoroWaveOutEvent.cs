namespace KokoroSharp.Core;

using NAudio.Wave;

using OpenTK.Audio.OpenAL;

using System.Diagnostics;

/// <summary> Base class for cross platform audio playback, with API mostly compatible with NAudio's <see cref="WaveOutEvent"/> API. </summary>
/// <remarks> Each platform (Windows/Linux/MacOS) derives from this to expose a nice interface back to KokoroSharp. </remarks>
public abstract class KokoroWaveOutEvent {
    public RawSourceWaveStream stream { get; private set; }

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
    public virtual float CurrentPercentage => stream.Position / (float) stream.Length;

    /// <summary> Pause not supported for simplicity. </summary>
    public void Pause() => throw new NotImplementedException("We're not gonna support this.");
}

// A wrapper for NAudio's WaveOutEvent.
public class WindowsAudioPlayer : KokoroWaveOutEvent {
    readonly WaveOutEvent waveOut = new();
    public override PlaybackState PlaybackState => waveOut.PlaybackState;
    public override void Dispose() => waveOut.Dispose();
    public override void Play() { waveOut.Init(stream); waveOut.Play(); }
    public override void SetVolume(float volume) => waveOut.Volume = volume;
    public override void Stop() => waveOut.Stop();
}

public class MacOSAudioPlayer : LinuxAudioPlayer { }

// Warning: Terrible, TERRIBLE code..
public class LinuxAudioPlayer : KokoroWaveOutEvent {
    public static int BufferSize = 4096 * 64;   // Yes it's long. Could use help to optimize.
    public static int BufferCount = 256; // 64 MB. Devs can shorten it if needed.

    int source;
    int[] buffers;
    Thread streamThread;
    bool stopRequested;
    PlaybackState state = PlaybackState.Stopped;

    public override PlaybackState PlaybackState => state;

    // ATM it's joining and creating new thread each time. Not the best idea.
    public override void Play() {
        if (streamThread != null) { Stop(); }
        var device = ALC.OpenDevice(null);
        var context = ALC.CreateContext(device, (int[]) null);
        ALC.MakeContextCurrent(context);
        source = AL.GenSource();
        buffers = AL.GenBuffers(BufferCount);
        stopRequested = false;

        // Initialize the buffer
        for (int i = 0; i < BufferCount; i++) {
            if (GetBufferFromStream() is not byte[] data) { break; }
            FillALBuffer(buffers[i], data);
        }
        AL.SourceQueueBuffers(source, buffers);
        AL.SourcePlay(source);
        state = PlaybackState.Playing;

        streamThread = new Thread(() => {
            AL.GetSource(source, ALGetSourcei.BuffersProcessed, out int processed);

            var sw = Stopwatch.StartNew();
            while (processed-- > 0 && !stopRequested) {
                int buf = AL.SourceUnqueueBuffer(source);
                if (GetBufferFromStream() is not byte[] data) { break; }
                FillALBuffer(buf, data);
                AL.SourceQueueBuffer(source, buf);
                Thread.Sleep(10);
            }

            while (!stopRequested && AL.GetSource(source, ALGetSourcei.SourceState) == (int) ALSourceState.Playing) {
                stream.Position = (int) ((sw.ElapsedMilliseconds / 1000f) * stream.WaveFormat.AverageBytesPerSecond);
                Thread.Sleep(10);
            }
            if (!stopRequested) { stream.Position = stream.Length; }
            else { stream.Position = (int) ((sw.ElapsedMilliseconds / 1000f) * stream.WaveFormat.AverageBytesPerSecond); }

            state = PlaybackState.Stopped;
        }) { IsBackground = true };
        streamThread.Start();

        unsafe void FillALBuffer(int buffer, byte[] data) { fixed (byte* ptr = data) { AL.BufferData(buffer, ALFormat.Mono16, (IntPtr) ptr, data.Length, stream.WaveFormat.SampleRate); } }
        byte[] GetBufferFromStream() {
            var buffer = new byte[BufferSize];
            int bytesRead = stream.Read(buffer, 0, BufferSize);
            if (bytesRead < BufferSize) { Array.Resize(ref buffer, bytesRead); }
            return bytesRead > 0 ? buffer : null;
        }
    }

    public override void Stop() => Dispose();
    public override void SetVolume(float volume) => AL.Source(source, ALSourcef.Gain, Math.Clamp(volume, 0, 1f)); // Technically supports > 1 volume but not sure if it's a good idea.
    public override void Dispose() {
        AL.SourceStop(source);
        state = PlaybackState.Stopped;
        stopRequested = true;
        streamThread?.Join();
        streamThread = null;
        AL.DeleteSource(source);
        AL.DeleteBuffers(buffers);
        var context = ALC.GetCurrentContext();
        var device = ALC.GetContextsDevice(context);
        ALC.DestroyContext(context);
        ALC.CloseDevice(device);
    }
}

namespace KokoroSharp.Core;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary> Base class for cross platform audio playback, with API mostly compatible with NAudio's <see cref="WaveOutEvent"/> API. </summary>
/// <remarks> Each platform (Windows/Linux/MacOS) derives from this to expose a nice interface back to KokoroSharp. </remarks>
public abstract class KokoroWaveOutEvent {
    public RawSourceWaveStream stream { get; private set; }

    public float Volume { get; protected set; } = 1;

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
    VolumeSampleProvider volumeProvider;

    public override PlaybackState PlaybackState => waveOut.PlaybackState;
    public override void Dispose() => waveOut.Dispose();
    public override void Play() {
        volumeProvider = new(stream.ToSampleProvider()) { Volume = Volume };
        waveOut.Init(volumeProvider);
        waveOut.Play();
    }
    public override void SetVolume(float volume) {
        Volume = volume;
        if (volumeProvider != null) { volumeProvider.Volume = volume; }
    }
    public override void Stop() => waveOut.Stop();
}

public class MacOSAudioPlayer : OpenALAudioPlayer { }
public class LinuxAudioPlayer : OpenALAudioPlayer { }

/// <summary> Plays complete PCM segments through the bundled OpenAL Soft backend on Linux and macOS. </summary>
/// <remarks> Calls the small native OpenAL API directly, avoiding managed binding dependencies and version conflicts. </remarks>
public unsafe class OpenALAudioPlayer : KokoroWaveOutEvent {
    const int PlaybackPollMilliseconds = 5;

    IntPtr device, context;
    uint source, buffer;
    Thread playbackThread;
    long playbackStartPosition, playbackEndPosition;
    int volumeVersion, appliedVolumeVersion = -1;
    volatile bool stopRequested;
    volatile PlaybackState state = PlaybackState.Stopped;
    bool isDisposed;

    public override PlaybackState PlaybackState => state;

    /// <summary> Creates an OpenAL player and verifies that its native backend can load on this operating system. </summary>
    /// <remarks> Construction fails immediately on unsupported systems instead of failing later on a playback thread. </remarks>
    public OpenALAudioPlayer() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { throw new PlatformNotSupportedException("OpenAL playback is supported only on Linux and macOS."); }
        OpenAL.EnsureAvailable();
    }

    /// <summary> Begins playing the initialized PCM audio through OpenAL. </summary>
    /// <remarks> Native setup happens synchronously so device and format errors reach the caller immediately. </remarks>
    public override void Play() {
        if (isDisposed) { throw new ObjectDisposedException(nameof(OpenALAudioPlayer)); }
        Stop();

        int bufferFormat = (stream.WaveFormat.Channels, stream.WaveFormat.BitsPerSample) switch {
            (1, 8) => OpenAL.FormatMono8,
            (1, 16) => OpenAL.FormatMono16,
            (2, 8) => OpenAL.FormatStereo8,
            (2, 16) => OpenAL.FormatStereo16,
            _ => throw new NotSupportedException($"OpenAL playback does not support {stream.WaveFormat.Channels} channels at {stream.WaveFormat.BitsPerSample} bits per sample.")
        };

        playbackStartPosition = stream.Position;
        var audioData = new byte[checked((int) (stream.Length - playbackStartPosition))];
        int totalBytesRead = 0;
        while (totalBytesRead < audioData.Length) {
            int bytesRead = stream.Read(audioData, totalBytesRead, audioData.Length - totalBytesRead);
            if (bytesRead == 0) { break; }
            totalBytesRead += bytesRead;
        }
        if (totalBytesRead != audioData.Length) { Array.Resize(ref audioData, totalBytesRead); }
        playbackEndPosition = playbackStartPosition + audioData.Length;
        stream.Position = playbackStartPosition;
        if (audioData.Length == 0) { stream.Position = stream.Length; return; }

        try {
            device = OpenAL.OpenDevice(IntPtr.Zero);
            if (device == IntPtr.Zero) { throw new InvalidOperationException("OpenAL could not open the default audio device."); }
            context = OpenAL.CreateContext(device, IntPtr.Zero);
            if (context == IntPtr.Zero) { throw new InvalidOperationException("OpenAL could not create an audio context."); }
            if (!OpenAL.MakeContextCurrent(context)) { throw new InvalidOperationException("OpenAL could not activate its audio context."); }
            source = OpenAL.GenSource();
            appliedVolumeVersion = -1; // Every new source starts at gain 1 and must receive the current requested volume.
            buffer = OpenAL.GenBuffer();
            OpenAL.BufferData(buffer, bufferFormat, audioData, stream.WaveFormat.SampleRate);
            OpenAL.BindBuffer(source, buffer);
            OpenAL.MakeContextCurrent(IntPtr.Zero);

            stopRequested = false;
            state = PlaybackState.Playing;
            playbackThread = new Thread(StreamPlayback) { IsBackground = true, Name = "KokoroSharp OpenAL Playback" };
            playbackThread.Start();
        }
        catch {
            ReleasePlaybackResources();
            state = PlaybackState.Stopped;
            throw;
        }
    }

    /// <summary> Stops the active segment and waits until its native resources have been released. </summary>
    /// <remarks> The playback thread owns every OpenAL call after startup, keeping the context on one thread at a time. </remarks>
    public override void Stop() {
        stopRequested = true;
        var activeThread = playbackThread;
        if (activeThread != null && activeThread != Thread.CurrentThread) { activeThread.Join(); }
        playbackThread = null;
        state = PlaybackState.Stopped;
    }

    /// <summary> Sets playback volume between silence and the original sample volume. </summary>
    /// <remarks> The playback thread applies changes because its OpenAL context is thread-local. </remarks>
    public override void SetVolume(float volume) {
        Volume = Math.Min(Math.Max(volume, 0f), 1f);
        Interlocked.Increment(ref volumeVersion);
    }

    /// <summary> Stops playback and releases its native resources. </summary>
    /// <remarks> Safe to call repeatedly. </remarks>
    public override void Dispose() {
        if (isDisposed) { return; }
        isDisposed = true;
        Stop();
    }

    void StreamPlayback() {
        try {
            if (!OpenAL.MakeContextCurrent(context)) { throw new InvalidOperationException("OpenAL could not move its audio context to the playback thread."); }
            ApplyVolume();
            OpenAL.PlaySource(source);

            // Mirror OpenAL's byte offset onto the managed stream until the source finishes or cancellation is requested.
            while (!stopRequested) {
                if (OpenAL.GetSourceInteger(source, OpenAL.SourceState) != OpenAL.Playing) { stream.Position = playbackEndPosition; break; }
                UpdateStreamPosition();
                ApplyVolume();
                Thread.Sleep(PlaybackPollMilliseconds);
            }
        }
        catch (Exception exception) { Debug.WriteLine($"OpenAL playback failed: {exception}"); }
        finally {
            if (context != IntPtr.Zero && source != 0 && stopRequested) { UpdateStreamPosition(); }
            ReleasePlaybackResources();
            playbackThread = null;
            state = PlaybackState.Stopped;
        }
    }

    void UpdateStreamPosition() {
        int byteOffset = OpenAL.GetSourceInteger(source, OpenAL.ByteOffset);
        stream.Position = Math.Min(playbackEndPosition, playbackStartPosition + Math.Max(0, byteOffset));
    }

    void ApplyVolume() {
        int requestedVolumeVersion = Volatile.Read(ref volumeVersion);
        if (requestedVolumeVersion == appliedVolumeVersion) { return; }
        OpenAL.SetSourceFloat(source, OpenAL.Gain, Volume);
        appliedVolumeVersion = requestedVolumeVersion;
    }

    void ReleasePlaybackResources() {
        if (context != IntPtr.Zero) { OpenAL.MakeContextCurrent(context); }
        if (source != 0) { OpenAL.StopSource(source); OpenAL.DeleteSource(source); source = 0; }
        if (buffer != 0) { OpenAL.DeleteBuffer(buffer); buffer = 0; }
        if (context != IntPtr.Zero) { OpenAL.MakeContextCurrent(IntPtr.Zero); OpenAL.DestroyContext(context); context = IntPtr.Zero; }
        if (device != IntPtr.Zero) { OpenAL.CloseDevice(device); device = IntPtr.Zero; }
    }

    static class OpenAL {
        const string Library = "openal";
        const int DefaultDeviceSpecifier = 0x1004;
        const int Buffer = 0x1009;

        public const int FormatMono8 = 0x1100, FormatMono16 = 0x1101, FormatStereo8 = 0x1102, FormatStereo16 = 0x1103;
        public const int Gain = 0x100A, SourceState = 0x1010, Playing = 0x1012, ByteOffset = 0x1026;

        public static void EnsureAvailable() => GetString(IntPtr.Zero, DefaultDeviceSpecifier);
        public static uint GenSource() { GenSources(1, out uint generatedSource); return generatedSource; }
        public static uint GenBuffer() { GenBuffers(1, out uint generatedBuffer); return generatedBuffer; }
        public static void DeleteSource(uint source) => DeleteSources(1, ref source);
        public static void DeleteBuffer(uint buffer) => DeleteBuffers(1, ref buffer);
        public static void BindBuffer(uint source, uint buffer) => SetSourceInteger(source, Buffer, (int) buffer);
        public static int GetSourceInteger(uint source, int parameter) { GetSourceInteger(source, parameter, out int value); return value; }
        public static void BufferData(uint buffer, int format, byte[] data, int frequency) { fixed (byte* audioData = data) { BufferData(buffer, format, audioData, data.Length, frequency); } }

        [DllImport(Library, EntryPoint = "alcGetString", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr GetString(IntPtr device, int parameter);
        [DllImport(Library, EntryPoint = "alcOpenDevice", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr OpenDevice(IntPtr deviceName);
        [DllImport(Library, EntryPoint = "alcCreateContext", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr CreateContext(IntPtr device, IntPtr attributes);
        [DllImport(Library, EntryPoint = "alcMakeContextCurrent", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool MakeContextCurrent(IntPtr context);
        [DllImport(Library, EntryPoint = "alcDestroyContext", CallingConvention = CallingConvention.Cdecl)] public static extern void DestroyContext(IntPtr context);
        [DllImport(Library, EntryPoint = "alcCloseDevice", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool CloseDevice(IntPtr device);
        [DllImport(Library, EntryPoint = "alGenSources", CallingConvention = CallingConvention.Cdecl)] static extern void GenSources(int count, out uint sources);
        [DllImport(Library, EntryPoint = "alDeleteSources", CallingConvention = CallingConvention.Cdecl)] static extern void DeleteSources(int count, ref uint sources);
        [DllImport(Library, EntryPoint = "alGenBuffers", CallingConvention = CallingConvention.Cdecl)] static extern void GenBuffers(int count, out uint buffers);
        [DllImport(Library, EntryPoint = "alDeleteBuffers", CallingConvention = CallingConvention.Cdecl)] static extern void DeleteBuffers(int count, ref uint buffers);
        [DllImport(Library, EntryPoint = "alBufferData", CallingConvention = CallingConvention.Cdecl)] static extern void BufferData(uint buffer, int format, void* data, int size, int frequency);
        [DllImport(Library, EntryPoint = "alSourcei", CallingConvention = CallingConvention.Cdecl)] static extern void SetSourceInteger(uint source, int parameter, int value);
        [DllImport(Library, EntryPoint = "alSourcef", CallingConvention = CallingConvention.Cdecl)] public static extern void SetSourceFloat(uint source, int parameter, float value);
        [DllImport(Library, EntryPoint = "alGetSourcei", CallingConvention = CallingConvention.Cdecl)] static extern void GetSourceInteger(uint source, int parameter, out int value);
        [DllImport(Library, EntryPoint = "alSourcePlay", CallingConvention = CallingConvention.Cdecl)] public static extern void PlaySource(uint source);
        [DllImport(Library, EntryPoint = "alSourceStop", CallingConvention = CallingConvention.Cdecl)] public static extern void StopSource(uint source);
    }
}

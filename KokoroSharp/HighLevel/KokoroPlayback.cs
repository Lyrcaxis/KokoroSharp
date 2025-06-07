namespace KokoroSharp;

using KokoroSharp.Core;
using KokoroSharp.Utilities;

using NAudio.Wave;
using System.Collections.Concurrent;

/// <summary> Helper class that can simplify audio playback from Kokoro Inference Jobs. Can be either reused or live with a specific KokoroJob instance. </summary>
/// <remarks> Internally hosts a background worker thread that keeps checking for any queued samples, and plays them back if there's nothing else playing, in the same order they were queued. </remarks>
public sealed class KokoroPlayback : IDisposable {
    public static readonly WaveFormat waveFormat = new(24000, 16, 1);
    readonly KokoroWaveOutEvent waveOut = CrossPlatformHelper.GetAudioPlayer();
    readonly ConcurrentQueue<PlaybackHandle> queuedPackets = [];

    volatile bool hasExited;

    /// <summary> If true, the output audio of the model will be *nicified* before being played back. </summary>
    /// <remarks> Nicification includes trimming silent start and finish, and attempting to reduce noise. </remarks>
    public bool NicifySamples { get; set; } = true;

    /// <summary> Creates a background audio playback instance, and causes it to automatically play back all samples added via <see cref="Enqueue(float[])"/>. </summary>
    /// <remarks> If 'job' is specified, the instance will automatically cease when the job is completed or canceled. </remarks>
    public KokoroPlayback() {
        new Thread(async () => {
            while (!hasExited) {
                await Task.Delay(100);
                while (!hasExited && queuedPackets.TryDequeue(out var packet)) {
                    if (packet.Aborted) { continue; }

                    var (samples, startTime) = (packet.Samples, DateTime.Now);
                    packet.OnStarted?.Invoke();
                    if (NicifySamples) { samples = PostProcessSamples(samples); }

                    var stream = new RawSourceWaveStream(GetBytes(samples), 0, samples.Length * 2, waveFormat);
                    waveOut.Init(stream); waveOut.Play(); // Initialize and play the audio stream, then wait until it's done.
                    while (!hasExited && !packet.Aborted && waveOut.PlaybackState == PlaybackState.Playing) { await Task.Delay(10); }
                    if (!hasExited && packet.Aborted) { waveOut.Stop(); }

                    // Once playback finished, invoke the correct callback.
                    if (stream.Position == stream.Length) { packet.OnSpoken?.Invoke(); packet.State = KokoroPlaybackHandleState.Completed; }
                    else { packet.OnCanceled?.Invoke(((float) (DateTime.Now - startTime).TotalSeconds, (float) (stream.Position / (float) stream.Length))); }
                    stream.Dispose();
                }
            }
        }) { IsBackground = true }.Start();
    }

    /// <summary> Enqueues specified audio samples for playback. They will be played once all previously queued samples have been played. </summary>
    public void Enqueue(float[] samples) => Enqueue(samples, null, null);

    /// <summary> Enqueues specified audio samples for playback. They will be played once all previously queued samples have been played. </summary>
    /// <remarks> The callbacks will be raised appropriately during playback. Note that "Cancel" will NOT BE CALLED for playbacks that never started. </remarks>
    internal PlaybackHandle Enqueue(float[] samples, Action OnStarted = null, Action OnSpoken = null, Action<(float time, float percentage)> OnCanceled = null) {
        ObjectDisposedException.ThrowIf(hasExited, this);

        var packet = new PlaybackHandle(samples, OnStarted, OnSpoken, OnCanceled) { Owner = this };
        queuedPackets.Enqueue(packet);
        return packet;
    }

    /// <summary> Stops the playback of the currently playing samples. This will also trigger callbacks. The next samples that are queued (if any) will begin playing immediately. </summary>
    /// <remarks> Note that this will NOT completely stop this instance from playing audio. To completely dispose this, call the `Dispose()` method. </remarks>
    public void StopPlayback(bool clearQueue = false) {
        waveOut.Stop();
        if (clearQueue) {
            foreach (var p in queuedPackets) { p.Abort(false); }
            queuedPackets.Clear();
        }
    }

    /// <summary> Adjust the volume of the playback. [0.0, to 1.0] </summary>
    public void SetVolume(float volume) => waveOut.SetVolume(Math.Clamp(volume, 0f, 1f));

    /// <summary> Immediately stops the playback and notifies the background worker thread to exit. </summary>
    /// <remarks> Note that this DOES NOT terminate any <see cref="KokoroJob"/>s related to this instance. </remarks>
    public void Dispose() {
        hasExited = true;
        waveOut.Stop();
        waveOut.Dispose();
        foreach (var p in queuedPackets) { p.Abort(false); }
        queuedPackets.Clear();
    }

    /// <summary> Performs some pre-processing on target samples, like trimming silence, and discarding potential noise. </summary>
    /// <remarks> Returns a new array with the processed audio samples. Note that the returned array will likely be smaller in size. </remarks>
    public static float[] PostProcessSamples(float[] samples) {
        var (start, end) = (0, samples.Length - 1);
        while (start < samples.Length && Math.Abs(samples[start]) <= 0.01f) { start++; }
        while (end > start && Math.Abs(samples[end]) <= 0.005f) { end--; }
        for (int i = 0; i < samples.Length; i++) { if (Math.Abs(samples[i]) < 0.001f) { samples[i] = 0; } }

        float[] trimmedSamples = new float[end - start + 1];
        Array.Copy(samples, start, trimmedSamples, 0, trimmedSamples.Length);
        if (trimmedSamples.Length == 0) { return samples; }
        return trimmedSamples;
    }

    /// <summary> Converts given 16bit audio sample array to bytes. </summary>
    public static byte[] GetBytes(float[] samples) => samples.Select(f => (short) (f * short.MaxValue)).SelectMany(BitConverter.GetBytes).ToArray();
}

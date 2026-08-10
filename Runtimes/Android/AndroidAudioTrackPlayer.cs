namespace KokoroSharp.Core;

using Android.Media;

using NAudio.Wave;

using AndroidEncoding = Android.Media.Encoding;
using PlaybackState = NAudio.Wave.PlaybackState;

/// <summary> Plays audio through Android's own <see cref="AudioTrack"/> API. Registered by <see cref="KokoroSharpAndroid.Init"/>. </summary>
public class AndroidAudioTrackPlayer : KokoroWaveOutEvent {
    AudioTrack audioTrack;
    Thread streamThread;
    volatile bool stopRequested;
    PlaybackState state = PlaybackState.Stopped;

    public override PlaybackState PlaybackState => state;

    public override void Play() {
        if (streamThread != null) { Stop(); }
        stopRequested = false;
        state = PlaybackState.Playing;
        streamThread = new Thread(() => {
            // Each Play() receives one complete, bounded segment, so the whole thing goes into a static track upfront.
            // Feeding chunk-by-chunk instead is glitch-prone: any late write becomes an audible crack.
            var audioBytes = new byte[stream.Length - stream.Position];
            stream.Read(audioBytes, 0, audioBytes.Length);
            int totalFrames = audioBytes.Length / 2;
            if (totalFrames > 0) {
                audioTrack = new AudioTrack.Builder()
                    .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media).SetContentType(AudioContentType.Speech).Build())
                    .SetAudioFormat(new AudioFormat.Builder().SetEncoding(AndroidEncoding.Pcm16bit).SetSampleRate(stream.WaveFormat.SampleRate).SetChannelMask(ChannelOut.Mono).Build())
                    .SetTransferMode(AudioTrackMode.Static)
                    .SetBufferSizeInBytes(audioBytes.Length)
                    .Build();
                audioTrack.Write(audioBytes, 0, audioBytes.Length);
                audioTrack.SetVolume(Volume);
                audioTrack.Play();

                // Mirror the playback head onto 'stream.Position', so progress and phoneme callbacks stay accurate.
                while (!stopRequested && audioTrack.PlaybackHeadPosition < totalFrames) {
                    stream.Position = Math.Min(stream.Length, audioTrack.PlaybackHeadPosition * 2L);
                    Thread.Sleep(10);
                }
                if (!stopRequested) { stream.Position = stream.Length; }
                audioTrack.Stop();
                audioTrack.Release();
            }
            state = PlaybackState.Stopped;
        }) { IsBackground = true };
        streamThread.Start();
    }

    public override void Stop() {
        stopRequested = true;
        streamThread?.Join();
        streamThread = null;
        state = PlaybackState.Stopped;
    }

    public override void SetVolume(float volume) {
        Volume = Math.Clamp(volume, 0, 1f);
        audioTrack?.SetVolume(Volume);
    }

    public override void Dispose() => Stop();
}

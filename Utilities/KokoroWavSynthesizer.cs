namespace KokoroSharp;

using KokoroSharp.Core;
using KokoroSharp.Processing;

using Microsoft.ML.OnnxRuntime;

using NAudio.Wave;

using System.Collections.Generic;
using System.Diagnostics;

/// <summary> Class that allows synthesizing audio without speaking it. </summary>
public partial class KokoroWavSynthesizer : KokoroEngine {
    KokoroTTSPipelineConfig defaultPipelineConfig = new(new DefaultSegmentationConfig() { MaxFirstSegmentLength = 510 });

    /// <summary> Creates a new instance that allows synthesizing audio without speaking it. </summary>
    public KokoroWavSynthesizer(string modelPath, SessionOptions options = null) : base(modelPath, options) { }

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, and returns the bytes that the total audio consists of. </summary>
    public byte[] Synthesize(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = null) => Task.Run(() => SynthesizeAsync(text, voice, pipelineConfig)).Result;

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, and returns the bytes that the total audio consists of. </summary>
    public async Task<byte[]> SynthesizeAsync(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = null) => (await SynthesizeWithTimestampsAsync(text, voice, pipelineConfig)).AudioBytes;

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, and notifies back with the given callback. </summary>
    /// <param name="OnProgress"> Will be invoked with the model's outputs (audio samples) the moment they're ready. Note that these are not ALL samples, but only samples of the segment. </param>
    /// <param name="OnComplete"> Will be invoked once all segments have been translated to audio samples. </param>
    public void Synthesize(string text, KokoroVoice voice, Action<float[]> OnProgress, Action OnComplete, KokoroTTSPipelineConfig pipelineConfig = null) {
        pipelineConfig ??= defaultPipelineConfig;
        var tokens = Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), pipelineConfig.PreprocessText);
        var segments = pipelineConfig.SegmentationFunc(tokens);
        var job = EnqueueJob(KokoroJob.Create(segments, voice, pipelineConfig.Speed, null));

        foreach (var step in job.Steps) {
            step.OnStepComplete = (samples) => {
                OnProgress?.Invoke(samples);
                if (step == job.Steps[^1]) { OnComplete?.Invoke(); }
            };
        }
    }

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, returning the total audio bytes together with per-phoneme timestamps relative to that audio. </summary>
    public async Task<(byte[] AudioBytes, PhonemeTimestamp[] Timestamps)> SynthesizeWithTimestampsAsync(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = null) {
        pipelineConfig ??= defaultPipelineConfig;
        var tokens = Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), pipelineConfig.PreprocessText);
        var segments = pipelineConfig.SegmentationFunc(tokens);
        var job = EnqueueJob(KokoroJob.Create(segments, voice, pipelineConfig.Speed, null));

        List<byte> bytes = [];
        List<PhonemeTimestamp> timestamps = [];

        foreach (var step in job.Steps) {
            step.OnStepComplete = (samples) => {
                Debug.WriteLine($"[{job.Steps.IndexOf(step) + 1}/{job.Steps.Count}] Retrieved {samples.Length} samples.");
                var trimmedSamples = KokoroPlayback.PostProcessSamples(samples, out var leadTrim);

                // Shift each timestamp forward by the audio gathered so far, and back by the silence trimmed off this segment's start.
                var (stepStart, trim) = (bytes.Count / 2f / 24_000, leadTrim / (float) 24_000);
                foreach (var t in step.Timestamps ?? []) { timestamps.Add(t with { StartSecond = stepStart + Math.Max(0, t.StartSecond - trim), EndSecond = stepStart + Math.Max(0, t.EndSecond - trim) }); }

                bytes.AddRange(KokoroPlayback.GetBytes(trimmedSamples));
                if (step.Tokens.Length == 0 || !Tokenizer.PunctuationTokens.Contains(step.Tokens[^1])) { return; }
                var secondsToWait = pipelineConfig.SecondsOfPauseBetweenProperSegments[Tokenizer.TokenToChar[step.Tokens[^1]]];
                bytes.AddRange(KokoroPlayback.GetBytes(new float[(int) (secondsToWait * KokoroPlayback.waveFormat.SampleRate)]));
            };
        }
        while (!job.isDone) { await Task.Delay(10); }
        return ([.. bytes], [.. timestamps]);
    }

    /// <summary> Saves the specified audio bytes to the specified file path. </summary>
    public static void SaveAudioToFile(byte[] audioBytes, string filePath) {
        using var writer = new WaveFileWriter(filePath, KokoroPlayback.waveFormat);
        writer.Write(audioBytes, 0, audioBytes.Length);
    }
}

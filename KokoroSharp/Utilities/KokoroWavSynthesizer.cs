namespace KokoroSharp.Utilities;

using KokoroSharp.Core;
using KokoroSharp.Processing;

using Microsoft.ML.OnnxRuntime;

using NAudio.Wave;

using System.Collections.Generic;
using System.Diagnostics;

/// <summary> Class that allows synthesizing audio without speaking it. </summary>
public class KokoroWavSynthesizer : KokoroEngine {
    KokoroTTSPipelineConfig defaultPipelineConfig = new(new DefaultSegmentationConfig() {
        MaxFirstSegmentLength = 510,
        MaxSecondSegmentLength = 510
    });

    /// <summary> Creates a new instance that allows synthesizing audio without speaking it. </summary>
    public KokoroWavSynthesizer(string modelPath, SessionOptions options = null) : base(modelPath, options) { }

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, and returns the bytes that the total audio consists of. </summary>
    /// <param name="text"> The text to speak. </param>
    /// <param name="voice"> The voice that will speak it. </param>
    public byte[] Synthesize(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = null) => Task.Run(() => SynthesizeAsync(text, voice, pipelineConfig)).Result;

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, and returns the bytes that the total audio consists of. </summary>
    /// <param name="text"> The text to speak. </param>
    /// <param name="voice"> The voice that will speak it. </param>
    public async Task<byte[]> SynthesizeAsync(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = null) {
        pipelineConfig ??= defaultPipelineConfig;
        var tokens = Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), pipelineConfig.PreprocessText);
        var segments = pipelineConfig.SegmentationFunc(tokens);
        var job = EnqueueJob(KokoroJob.Create(segments, voice, pipelineConfig.Speed, null));

        List<byte> bytes = [];

        var phonemesCache = segments.Count > 1 ? new List<char>() : null;
        foreach (var step in job.Steps) {
            step.OnStepComplete = (samples) => {
                Debug.WriteLine($"[{job.Steps.IndexOf(step) + 1}/{job.Steps.Count}] Retrieved {samples.Length} samples.");
                bytes.AddRange(KokoroPlayback.GetBytes(KokoroPlayback.PostProcessSamples(samples)));
                if (!Tokenizer.PunctuationTokens.Contains(step.Tokens[^1])) { return; }
                var secondsToWait = pipelineConfig.SecondsOfPauseBetweenProperSegments[Tokenizer.TokenToChar[step.Tokens[^1]]];
                bytes.AddRange(KokoroPlayback.GetBytes(new float[(int) (secondsToWait * KokoroPlayback.waveFormat.SampleRate)]));
            };
        }
        while (!job.isDone) { await Task.Delay(10); }
        return [.. bytes];
    }

    /// <summary> Inferences with the model to speak the text with specified voice after segmenting it, and notifies back with the given callback. </summary>
    /// <param name="text"> The text to speak. </param>
    /// <param name="voice"> The voice that will speak it. </param>
    /// <param name="OnProgress"> Will be invoked with the model's outputs (audio samples) the moment they're ready. Note that these are not ALL samples, but only samples of the segment. </param>
    /// <param name="OnComplete"> Will be invoked once all segments have been translated to audio samples. </param>
    public void Synthesize(string text, KokoroVoice voice, Action<float[]> OnProgress, Action OnComplete, KokoroTTSPipelineConfig pipelineConfig = null) {
        pipelineConfig ??= defaultPipelineConfig;
        var tokens = Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), pipelineConfig.PreprocessText);
        var segments = pipelineConfig.SegmentationFunc(tokens);
        var job = EnqueueJob(KokoroJob.Create(segments, voice, pipelineConfig.Speed, null));

        var phonemesCache = segments.Count > 1 ? new List<char>() : null;
        foreach (var step in job.Steps) {
            step.OnStepComplete = (samples) => {
                OnProgress?.Invoke(samples);
                if (step == job.Steps[^1]) { OnComplete?.Invoke(); }
            };
        }
    }

    /// <summary> Saves the specified audio bytes to the specified file path. </summary>
    public void SaveAudioToFile(byte[] audioBytes, string filePath) {
        using var writer = new WaveFileWriter(filePath, KokoroPlayback.waveFormat);
        writer.Write(audioBytes, 0, audioBytes.Length);
    }
}

using BenchmarkDotNet.Attributes;

using KokoroSharp;
using KokoroSharp.Core;

using System;
using System.Collections.Generic;

namespace Benchmark;

[InProcess]
public class Inference {
    const string text = "This is a performance benchmark of Kokoro.";
    static readonly Dictionary<(KModel, bool UseCuda), KokoroModel> models = [];
    static int[] tokens;
    static KokoroVoice voice;

    [GlobalSetup]
    public void Setup() {
        tokens = KokoroSharp.Processing.Tokenizer.Tokenize(text);
        voice = KokoroVoiceManager.GetVoice("af_heart");
        foreach (var model in Enum.GetValues<KModel>()) {
            if (!KokoroTTS.IsDownloaded(model))
                KokoroTTS.LoadModel(model).Dispose(); // downloads the model if not already present.
            var options = new Microsoft.ML.OnnxRuntime.SessionOptions();
            models[(model, false)] = new KokoroModel(KokoroTTS.ModelNamesMap[model], options);
            var options2 = new Microsoft.ML.OnnxRuntime.SessionOptions();
            options2.AppendExecutionProvider_CUDA(); // Use CUDA for GPU inference.
            models[(model, true)] = new KokoroModel(KokoroTTS.ModelNamesMap[model], options2);
        }
    }

    [ParamsAllValues]
    public KModel Model { get; set; }

    [Benchmark] public float[] CPU() => models[(Model, false)].Infer(tokens, voice!.Features);
    [Benchmark] public float[] CUDA() => models[(Model, true)].Infer(tokens, voice!.Features);
}

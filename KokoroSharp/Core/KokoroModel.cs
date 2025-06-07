namespace KokoroSharp.Core;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using System.Collections.Generic;
using System.Diagnostics;

/// <summary> An instance of the model in the ONNX runtime. For a higher level module, see <see cref="KokoroTTS"/>. </summary>
/// <remarks> Once instantiated, the model will remain loaded and be ready to be reused for inference with new parameters. </remarks>
public sealed class KokoroModel : IDisposable {
    readonly InferenceSession session;
    readonly SessionOptions defaultOptions = new() { EnableMemoryPattern = true, InterOpNumThreads = 8, IntraOpNumThreads = 8 };

    public const int maxTokens = 510;

    public KokoroModel(string modelPath, SessionOptions options = null) {
        session = new InferenceSession(modelPath, options ?? defaultOptions);
    }

    /// <summary> Requests inference with the Model via the ONNX runtime, with specified tokens, style, and speed. </summary>
    /// <remarks> Synchronously waits for the output (audio samples), and returns them when ready. Best used in async context. </remarks>
    public float[] Infer(int[] tokens, float[,,] voiceStyle, float speed = 1) {
        var (B, T, C) = (1, tokens.Length, voiceStyle.GetLength(2));
        if (tokens.Length == 0) {
            Debug.WriteLine("Received empty input token array. Returning empty float array.");
            return [];
        }
        if (tokens.Length > maxTokens) {
            Debug.WriteLine($"Max token count the model supports is {maxTokens}, but got {tokens.Length}. Please segment your input when passing longer sequences. Trimming to {maxTokens}.");
            Array.Resize(ref tokens, T = maxTokens);
        }

        var tokenTensor = new DenseTensor<long>([B, T + 2]); // <start>{text}<end>
        var styleTensor = new DenseTensor<float>([B, C]); // Voice features
        var speedTensor = new DenseTensor<float>(new[] { speed }, [B]);

        // Form Kokoro's input (<start>{text}<end>)
        var inputTokens = new int[T + 2]; // Initialized with all zeroes (<pad>).
        Array.Copy(tokens, 0, inputTokens, 1, T); // [0] and [^1] stay as zeroes.

        for (int j = 0; j < C; j++) { styleTensor[0, j] = voiceStyle[T - 1, 0, j]; }
        for (int i = 0; i < inputTokens.Length; i++) { tokenTensor[0, i] = (inputTokens[i] >= 0 ? inputTokens[i] : 4); } // [unk] --> '.'

        var inputs = new List<NamedOnnxValue> { GetOnnxValue("tokens", tokenTensor), GetOnnxValue("style", styleTensor), GetOnnxValue("speed", speedTensor) };
        lock (session) {
            using var results = session.Run(inputs);
            return [.. results[0].AsTensor<float>()];
        }
        NamedOnnxValue GetOnnxValue<T>(string name, DenseTensor<T> val) => NamedOnnxValue.CreateFromTensor(name, val);
    }

    public void Dispose() {
        lock (session) { session.Dispose(); }
    }
}

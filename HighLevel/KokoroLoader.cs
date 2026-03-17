namespace KokoroSharp;

using Microsoft.ML.OnnxRuntime;

using static KokoroSharp.KModel;

/// <summary> All available V1 releases of the model in ONNX form, including Full Precision and Quantized forms. </summary>
public enum KModel { float32, float16, int8 }

public partial class KokoroTTS {
    static IReadOnlyDictionary<KModel, string> ModelNamesMap { get; } = new Dictionary<KModel, string>() {
        { float32, "kokoro.onnx" },
        { float16, "kokoro-quant.onnx" },
        { int8,    "kokoro-quant-convinteger.onnx" },
    };
    static string URL(KModel quant) => $"https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/{ModelNamesMap[quant]}";

    static KokoroTTS() {
        try { _ = new SessionOptions(); }
        catch {
            throw new("This version of KokoroSharp does not come with a runtime supported by your system. For the previous plug & play package, use `KokoroSharp.CPU` (which works as-is for all platforms).\n" +
                "NOTE: This change happened because KokoroSharp now supports running on GPU. Refer to the project's README for more info: https://github.com/Lyrcaxis/KokoroSharp.");
        }
    }

    /// <summary> Returns 'true' if the specific model is already downloaded, otherwise 'false'. </summary>
    public static bool IsDownloaded(KModel model) => File.Exists(ModelNamesMap[model]);

    /// <summary> Asynchronously Loads or Downloads the model and returns a <see cref="KokoroTTS"/> instance, with specified ONNX session options. Optional callbacks for notifications. </summary>
    /// <remarks> If the model file is not found on disk, a background download will be triggered. Default session options use 8 CPU threads. </remarks>
    /// <param name="OnDownloadProgress"> Gets called when download progress was made. Returns a percentage of the current download to help update any UIs that happen to need it. </param>
    public static async Task<KokoroTTS> LoadModelAsync(KModel model = float32, Action<float> OnDownloadProgress = null, SessionOptions sessionOptions = null) {
        // If the model already exists on disk, just use that.
        if (IsDownloaded(model)) { return new KokoroTTS(ModelNamesMap[model], options: sessionOptions); }

        // Otherwise, download it to disk.
        using var client = new HttpClient();
        using var response = await client.GetAsync(URL(model), HttpCompletionOption.ResponseHeadersRead);
        using var responseStream = await response.Content.ReadAsStreamAsync();

        var fileSize = response.Content.Headers.ContentLength ?? 400_000_000L;
        var (buffer, bytesRead, totalRead) = (new byte[8192], 0, 0L);
        using var ms = new MemoryStream();
        while ((bytesRead = await responseStream.ReadAsync(buffer)) > 0) {
            totalRead += bytesRead;
            ms.Write(buffer, 0, bytesRead);
            OnDownloadProgress?.Invoke(totalRead / (float) fileSize);
        }
        ms.Position = 0;
        using (var fs = new FileStream(ModelNamesMap[model], FileMode.Create, FileAccess.Write)) { ms.CopyTo(fs); }
        return new KokoroTTS(ModelNamesMap[model], options: sessionOptions);
    }

    /// <summary> Dispatches an asynchronous request to Load or Download the model. The 'OnComplete' callback will be dispatched when the model is fully loaded. </summary>
    /// <remarks> If the model file is not found on disk, a background download will be triggered. Default session options use 8 CPU threads. </remarks>
    /// <param name="OnDownloadProgress"> Gets called when download progress was made. Returns a percentage of the current download to help update any UIs that happen to need it. </param>
    /// <param name="OnComplete"> Gets called at the end of download with the created <see cref="KokoroTTS"/> instance for the specified model type with specified ONNX session options. </param>
    public static void LoadModel(KModel model, Action<KokoroTTS> OnComplete, Action<float> OnDownloadProgress = null, SessionOptions sessionOptions = null) {
        LoadAsyncWithCallback(); // Let this run on the background, and invoke the callback when load is complete.
        async void LoadAsyncWithCallback() => OnComplete?.Invoke(await LoadModelAsync(model, OnDownloadProgress, sessionOptions));
    }

    /// <summary> Initiates a synchronous request to Load or Download the model and returns a <see cref="KokoroTTS"/> instance, with specified ONNX session options. Default session options use 8 CPU threads. </summary>
    /// <remarks> <b>Note that this will occupy/FREEZE the thread during the download if this is the first time the method is called. Consider using the async method, or the overload with callbacks.</b> </remarks>
    /// <returns> A <see cref="KokoroTTS"/> instance for the specified model type with specified ONNX session options. </returns>
    public static KokoroTTS LoadModel(KModel model = float32, SessionOptions sessionOptions = null) => Task.Run(() => LoadModelAsync(model, sessionOptions: sessionOptions)).Result;

    /// <summary>
    /// Creates a new Kokoro TTS Engine instance, loading the model into memory and initializing a background worker thread to continuously scan for newly queued jobs, dispatching them in order, when it's free.
    /// <para> If 'options' is specified, the model will be loaded with them. This is particularly useful when needing to run on non-CPU backends, as the default backend otherwise is the CPU with 8 threads. </para>
    /// <para> The model(s) can be found at https://github.com/taylorchu/kokoro-onnx/releases/tag/v0.2.0. </para>
    /// </summary>
    public static KokoroTTS LoadModel(string path, SessionOptions sessionOptions = null) => new KokoroTTS(path, sessionOptions);
}

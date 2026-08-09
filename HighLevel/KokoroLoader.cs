namespace KokoroSharp;

using KokoroSharp.Core;

using Microsoft.ML.OnnxRuntime;

using static KokoroSharp.KModel;

/// <summary> All available V1 and V1.1 releases of the model in ONNX form, including Full Precision and Quantized forms. </summary>
public enum KModel { float32, float16, zh_float32, zh_float16 }

/// <summary> Retrieves the model weights that back any <see cref="KokoroEngine"/>, downloading them on-demand. </summary>
public static class KokoroLoader {
    static IReadOnlyDictionary<KModel, string> ModelNamesMap { get; } = new Dictionary<KModel, string>() {
        { float32, "kokoro.onnx" },
        { float16, "kokoro-fp16.onnx" },
        { zh_float32, "kokoro-zh.onnx" },
        { zh_float16, "kokoro-zh-fp16.onnx" },
    };
    static string URL(KModel model) => $"https://github.com/Lyrcaxis/KokoroSharpBinaries/releases/download/v2.0.0/{ModelNamesMap[model]}";

    static KokoroLoader() {
        try { _ = new SessionOptions(); }
        catch {
            throw new("This version of KokoroSharp does not come with a runtime supported by your system. For the previous plug & play package, use `KokoroSharp.CPU` (which works as-is for all platforms).\n" +
                "NOTE: This change happened because KokoroSharp now supports running on GPU. Refer to the project's README for more info: https://github.com/Lyrcaxis/KokoroSharp.");
        }
    }

    /// <summary> Returns 'true' if the specific model is already downloaded, otherwise 'false'. </summary>
    public static bool IsDownloaded(KModel model) => File.Exists(ModelNamesMap[model]);

    /// <summary> Returns the local path of the specified model, first downloading it from KokoroSharpBinaries' releases if it's not already on disk. </summary>
    /// <param name="OnDownloadProgress"> Gets called when download progress was made. Returns a percentage of the current download to help update any UIs that happen to need it. </param>
    public static async Task<string> DownloadModelAsync(KModel model = float32, Action<float> OnDownloadProgress = null) {
        // If the model already exists on disk, just use that.
        var path = ModelNamesMap[model];
        if (File.Exists(path)) { return path; }

        // Otherwise, download it to disk.
        using var client = new HttpClient();
        using var response = await client.GetAsync(URL(model), HttpCompletionOption.ResponseHeadersRead);
        using var responseStream = await response.Content.ReadAsStreamAsync();

        var fileSize = response.Content.Headers.ContentLength ?? 400_000_000L;
        var (buffer, bytesRead, totalRead) = (new byte[8192], 0, 0L);
        using (var fs = new FileStream($"{path}.tmp", FileMode.Create, FileAccess.Write)) {
            while ((bytesRead = await responseStream.ReadAsync(buffer)) > 0) {
                totalRead += bytesRead;
                fs.Write(buffer, 0, bytesRead);
                OnDownloadProgress?.Invoke(totalRead / (float) fileSize);
            }
        }
        File.Move($"{path}.tmp", path, overwrite: true);
        return path;
    }
}

public partial class KokoroTTS {
    /// <summary> Returns 'true' if the specific model is already downloaded, otherwise 'false'. </summary>
    public static bool IsDownloaded(KModel model) => KokoroLoader.IsDownloaded(model);

    /// <summary> Asynchronously Loads or Downloads the model and returns a <see cref="KokoroTTS"/> instance, with specified ONNX session options. Optional callbacks for notifications. </summary>
    /// <remarks> If the model file is not found on disk, a background download will be triggered. Default session options use 8 CPU threads. </remarks>
    /// <param name="OnDownloadProgress"> Gets called when download progress was made. Returns a percentage of the current download to help update any UIs that happen to need it. </param>
    public static async Task<KokoroTTS> LoadModelAsync(KModel model = float32, Action<float> OnDownloadProgress = null, SessionOptions sessionOptions = null)
        => new(await KokoroLoader.DownloadModelAsync(model, OnDownloadProgress), sessionOptions);

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
    /// <para> The model(s) can be found at https://github.com/Lyrcaxis/KokoroSharpBinaries/releases. </para>
    /// </summary>
    public static KokoroTTS LoadModel(string path, SessionOptions sessionOptions = null) => new(path, sessionOptions);
}

public partial class KokoroWavSynthesizer {
    /// <summary> Returns 'true' if the specific model is already downloaded, otherwise 'false'. </summary>
    public static bool IsDownloaded(KModel model) => KokoroLoader.IsDownloaded(model);

    /// <summary> Asynchronously Loads or Downloads the model and returns a <see cref="KokoroWavSynthesizer"/> instance, with specified ONNX session options. Optional callbacks for notifications. </summary>
    /// <remarks> If the model file is not found on disk, a background download will be triggered. Default session options use 8 CPU threads. </remarks>
    /// <param name="OnDownloadProgress"> Gets called when download progress was made. Returns a percentage of the current download to help update any UIs that happen to need it. </param>
    public static async Task<KokoroWavSynthesizer> LoadModelAsync(KModel model = float32, Action<float> OnDownloadProgress = null, SessionOptions sessionOptions = null)
        => new(await KokoroLoader.DownloadModelAsync(model, OnDownloadProgress), sessionOptions);

    /// <summary> Dispatches an asynchronous request to Load or Download the model. The 'OnComplete' callback will be dispatched when the model is fully loaded. </summary>
    /// <remarks> If the model file is not found on disk, a background download will be triggered. Default session options use 8 CPU threads. </remarks>
    /// <param name="OnDownloadProgress"> Gets called when download progress was made. Returns a percentage of the current download to help update any UIs that happen to need it. </param>
    /// <param name="OnComplete"> Gets called at the end of download with the created <see cref="KokoroWavSynthesizer"/> instance for the specified model type with specified ONNX session options. </param>
    public static void LoadModel(KModel model, Action<KokoroWavSynthesizer> OnComplete, Action<float> OnDownloadProgress = null, SessionOptions sessionOptions = null) {
        LoadAsyncWithCallback(); // Let this run on the background, and invoke the callback when load is complete.
        async void LoadAsyncWithCallback() => OnComplete?.Invoke(await LoadModelAsync(model, OnDownloadProgress, sessionOptions));
    }

    /// <summary> Initiates a synchronous request to Load or Download the model and returns a <see cref="KokoroWavSynthesizer"/> instance, with specified ONNX session options. Default session options use 8 CPU threads. </summary>
    /// <remarks> <b>Note that this will occupy/FREEZE the thread during the download if this is the first time the method is called. Consider using the async method, or the overload with callbacks.</b> </remarks>
    public static KokoroWavSynthesizer LoadModel(KModel model = float32, SessionOptions sessionOptions = null) => Task.Run(() => LoadModelAsync(model, sessionOptions: sessionOptions)).Result;

    /// <summary> Creates a new instance that allows synthesizing audio without speaking it, loading the model from the specified path. </summary>
    public static KokoroWavSynthesizer LoadModel(string path, SessionOptions sessionOptions = null) => new(path, sessionOptions);
}

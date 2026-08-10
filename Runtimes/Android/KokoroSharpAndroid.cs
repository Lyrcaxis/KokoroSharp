namespace KokoroSharp;

using Android.Content.Res;

using KokoroSharp.Core;
using KokoroSharp.Utilities;

/// <summary> The Android entry point of KokoroSharp. Call <see cref="Init"/> once on startup, before creating any engine or playback instance. </summary>
public static class KokoroSharpAndroid {
    /// <summary> Prepares KokoroSharp for Android: extracts the APK-bundled voices to app storage, loads them, and registers the <see cref="AndroidAudioTrackPlayer"/>. </summary>
    /// <remarks> Also moves the working directory to app storage, so the model downloads land somewhere writable. </remarks>
    public static void Init() {
        CrossPlatformHelper.CustomAudioPlayer = new AndroidAudioTrackPlayer();
        var filesDir = Android.App.Application.Context.FilesDir.AbsolutePath;
        Directory.SetCurrentDirectory(filesDir);

        var assets = Android.App.Application.Context.Assets;
        var voicesDir = Path.Combine(filesDir, "voices");
        ExtractAssetFolder(assets, "voices", voicesDir);
        KokoroVoiceManager.LoadVoicesFromPath(voicesDir);
    }

    /// <summary> Copies an APK asset folder (and its subfolders) to disk, skipping files that were extracted on a previous run. </summary>
    static void ExtractAssetFolder(AssetManager assets, string assetPath, string targetDir) {
        Directory.CreateDirectory(targetDir);
        foreach (var name in assets.List(assetPath)) {
            var (childAssetPath, childTargetPath) = ($"{assetPath}/{name}", Path.Combine(targetDir, name));
            if (assets.List(childAssetPath) is { Length: > 0 }) { ExtractAssetFolder(assets, childAssetPath, childTargetPath); continue; }
            if (File.Exists(childTargetPath)) { continue; }
            using var source = assets.Open(childAssetPath);
            using var target = File.Create(childTargetPath);
            source.CopyTo(target);
        }
    }
}

namespace KokoroSharp.Utilities;

using KokoroSharp.Core;

using System.Runtime.InteropServices;

/// <summary> Contains functionality regarding cross-platform compatibility, like setting up the correct audio player. </summary>
/// <remarks> All platform-specific functionality splits will go through this class. </remarks>
public static class CrossPlatformHelper {

    /// <summary> Could be set to make 'KokoroPlayback' use a custom audio player. </summary>
    public static KokoroWaveOutEvent CustomAudioPlayer { get; set; }

    /// <summary> Retrieves the built-in audio player for Windows, Linux, or macOS. </summary>
    /// <remarks> Unsupported systems must register <see cref="CustomAudioPlayer"/> before initializing playback. </remarks>
    public static KokoroWaveOutEvent GetAudioPlayer() {
        if (CustomAudioPlayer != null) { return CustomAudioPlayer; }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { return new WindowsAudioPlayer(); }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { return new MacOSAudioPlayer(); }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { return new LinuxAudioPlayer(); }
        throw new PlatformNotSupportedException($"No built-in audio player is available for this platform. Assign {nameof(CrossPlatformHelper)}.{nameof(CustomAudioPlayer)} before initializing playback.");
    }
}

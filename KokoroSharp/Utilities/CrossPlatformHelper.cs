namespace KokoroSharp.Utilities;

using KokoroSharp.Core;

/// <summary> Contains functionality regarding cross-platform compatibility, like providing the path to the appropriate binaries, and setting up the correct audio player. </summary>
/// <remarks> All platform-specific functionality splits will go through this class. </remarks>
public static class CrossPlatformHelper {
    /// <summary> Retrieves the appropriate audio player for the running system: <b>NAudio.WaveOutEvent wrapper</b> for Windows, or <b>AL wrapper</b> for other OS. </summary>
    public static KokoroWaveOutEvent GetAudioPlayer() {
        if (OperatingSystem.IsWindows()) { return new WindowsAudioPlayer(); }
        if (OperatingSystem.IsMacOS()) { return new MacOSAudioPlayer(); }
        if (OperatingSystem.IsMacCatalyst()) { return new MacOSAudioPlayer(); }
        if (OperatingSystem.IsLinux()) { return new LinuxAudioPlayer(); }

        // Fallback. Might work for Android/iOS too?
        return new LinuxAudioPlayer(); // Who knows!
    }
}

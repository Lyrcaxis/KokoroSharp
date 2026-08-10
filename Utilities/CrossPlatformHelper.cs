namespace KokoroSharp.Utilities;

using KokoroSharp.Core;

/// <summary> Contains functionality regarding cross-platform compatibility, like setting up the correct audio player. </summary>
/// <remarks> All platform-specific functionality splits will go through this class. </remarks>
public static class CrossPlatformHelper {

    /// <summary> Could be set to make 'KokoroPlayback' use a custom audio player. </summary>
    public static KokoroWaveOutEvent CustomAudioPlayer { get; set; }

    /// <summary> Retrieves the appropriate audio player for the running system: <b>NAudio.WaveOutEvent wrapper</b> for Windows, or <b>AL wrapper</b> for other OS. </summary>
    public static KokoroWaveOutEvent GetAudioPlayer() {
        if (CustomAudioPlayer != null) { return CustomAudioPlayer; }
        if (OperatingSystem.IsWindows()) { return new WindowsAudioPlayer(); }
        if (OperatingSystem.IsMacOS()) { return new MacOSAudioPlayer(); }
        if (OperatingSystem.IsMacCatalyst()) { return new MacOSAudioPlayer(); }
        if (OperatingSystem.IsLinux()) { return new LinuxAudioPlayer(); }

        // Fallback. Might work for iOS too? Who knows!
        return new LinuxAudioPlayer(); // ..probably not though.
    }
}

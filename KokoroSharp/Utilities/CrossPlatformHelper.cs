namespace KokoroSharp.Utilities;

using KokoroSharp.Core;
using KokoroSharp.Processing;

using System.Runtime.InteropServices;

/// <summary> Contains functionality regarding cross-platform compatibility, like providing the path to the appropriate binaries, and setting up the correct audio player. </summary>
/// <remarks> All platform-specific functionality splits will go through this class. </remarks>
public static class CrossPlatformHelper {

    /// <summary> Retrieves the path for the appropriate espeak-ng binaries based on the platform and architecture. </summary>
    /// <remarks> In case there was no matching platform/architecture combo found for the running system, will fallback to "espeak-ng". </remarks>
    public static string GetEspeakBinariesPath() {
        // On non-desktop platforms, fallback to hopefully pre-installed version of espeak-ng for versions not supported out-of-the-box by KokoroSharp.
        if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())) { return "espeak-ng"; }

        // Otherwise, build the path to the binary based on PC's specs.
        var espeak_cli_path = @$"{Tokenizer.eSpeakNGPath}/espeak-ng-";
        if (OperatingSystem.IsWindows()) { espeak_cli_path += "win-"; }
        else if (OperatingSystem.IsLinux()) { espeak_cli_path += "linux-"; }
        else if (OperatingSystem.IsMacOS()) { espeak_cli_path += "macos-"; }
        else if (OperatingSystem.IsMacCatalyst()) { espeak_cli_path += "macos-"; }
        espeak_cli_path += (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64.dll" : "amd64.dll");

        return File.Exists(espeak_cli_path) ? espeak_cli_path : "espeak-ng"; // In case developers did not include the espeak folder at all.
    }

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

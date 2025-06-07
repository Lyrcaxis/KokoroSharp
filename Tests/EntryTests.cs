using NumSharp;

using Xunit;

namespace KokoroSharp.Tests;

public class EntryTests {
    [Fact]
    public async Task SpeechTest() {
        using KokoroTTS tts = KokoroTTS.LoadModel();
        var handle = tts.SpeakFast("Hello world.", KokoroVoiceManager.GetVoice("af_heart"));
        bool isWaiting = true;
        handle.OnSpeechCompleted += (_) => isWaiting = false;
        while (isWaiting) { await Task.Delay(1); }
        // sometimes throws on windows from KokoroWaveOut -- test thread exits before it can dispose
    }
}

namespace KokoroSharp;

using KokoroSharp.Core;
using KokoroSharp.Processing;

using System.Diagnostics;
internal class Program {
    static async Task Main(string[] _) {
        await Tokenizer.InitializeAsync();

        using KokoroTTS tts = KokoroTTS.LoadModel();
        KokoroVoice sarah = KokoroVoiceManager.GetVoice("af_sarah");
        
        tts.OnSpeechStarted    += (s) => Debug.WriteLine($"Started:   {new string(s.PhonemesToSpeak)}");
        tts.OnSpeechProgressed += (p) => Debug.WriteLine($"Progress:  {new string(p.SpokenText_BestGuess)}");
        tts.OnSpeechCompleted  += (c) => Debug.WriteLine($"Completed: {new string(c.PhonemesSpoken)}");
        tts.OnSpeechCanceled   += (c) => Debug.WriteLine($"Canceled:  {new string(c.SpokenText_BestGuess)}");

        while (true) {
            Console.Write("Type text to speak: ");
            string txt = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(txt)) { return; }
            tts.SpeakFast(txt, sarah);
        }
    }
}

namespace KokoroSharp;

using KokoroSharp.Core;
using KokoroSharp.Processing;

using System.Diagnostics;

/// <summary> Sample test program that reads the console line, then plays it back with the voice. </summary>
/// <remarks> Intended to act as an introduction to the lower-level parts for users interested in advanced tasks. </remarks>
internal class Program {

    // Mixing voice A with B in this example, but you can mix numerous voices together.
    // .. keeping this outside the 'Main' method for hot-reload support.
    static (int a, int b, int c) Mix => (2, 10, 5);

    static void Main(string[] _) {
        // You'll need to download the model first. You can find it in https://github.com/taylorchu/kokoro-onnx/releases/tag/v0.2.0.
        using KokoroTTS tts = KokoroTTS.LoadModel(); // The high level inference engine provided by KokoroSharp. We instantiate once, cache it, and reuse it.
        //KokoroVoiceManager.LoadVoicesFromPath("voices"); // The voices are pre-bundled with the package in "/voices", but can still be loaded manually from a different path if needed.
        KokoroVoice sarah = KokoroVoiceManager.GetVoice("af_sarah"); // Once the voices are loaded, they can be retrieved instantly from memory.
        KokoroVoice nicole = KokoroVoiceManager.GetVoice("af_nicole"); // Kokoro always needs a voice for inference.

        // You can check out the available/loaded voices by iterating through them:
        foreach (var voice in KokoroVoiceManager.Voices) { Debug.WriteLine(voice.Name); }
        foreach (var voice in KokoroVoiceManager.GetVoices(KokoroLanguage.AmericanEnglish)) { Debug.WriteLine(voice.Name); }
        tts.Speak("Welcome.", KokoroVoiceManager.GetVoice("af_heart")); // ..and synthesize speech with one line of code!

        // You can access and subscribe to various callbacks regarding speech to stay informed:
        tts.OnSpeechStarted    += (s) => Debug.WriteLine($"Started:   {new string(s.PhonemesToSpeak)}");
        tts.OnSpeechProgressed += (p) => Debug.WriteLine($"Progress:  {new string(p.SpokenText_BestGuess)}");
        tts.OnSpeechCompleted  += (c) => Debug.WriteLine($"Completed: {new string(c.PhonemesSpoken)}");
        tts.OnSpeechCanceled   += (c) => Debug.WriteLine($"Canceled:  {new string(c.SpokenText_BestGuess)}");

        while (true) {
            Console.Write("Type text to speak: ");
            string txt = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(txt)) { return; }

            // The easiest way to do text-to-speech with Kokoro is by invoking `tts.Speak()`/`tts.SpeakFast()` directly with input text.
            tts.SpeakFast(txt, KokoroVoiceManager.Mix([(sarah, Mix.a), (nicole, Mix.b)])); // Segmented with various rules (see `Segmentation.cs`). Getting an ~instant response, with a potential quality hit.
            //tts.Speak(txt, KokoroVoiceManager.Mix([(sarah, mix.a), (nicole, mix.b)]));   // Without segmentation; increasing the playback response time, but may offer increased quality.
            continue; // Comment out this line to proceed.


            // Although, what's MORE SUITABLE for more advanced tasks, is the `tts.EnqueueJob` method,
            // .. because it allows queueing up multiple *inference jobs* to the engine asynchronously,
            // .. and when, in order, one gets completed, the audio is also being played back in order.
            tts.StopPlayback(); // Immediately stops any ongoing playbacks and jobs invoked via `Speak`/`SpeakFast`.

            // Note that the `KokoroTTS` instance hosts its own instance of `KokoroPlayback`, for convenience,
            // .. but for anything more advanced than `SpeakFast`, you'll need to provide your own, or an alternative.
            KokoroPlayback playback = new KokoroPlayback();
            // *KokoroPlayback* equivalent of `tts.StopPlayback()` is 'playback.StopPlayback()'.
            playback.NicifySamples = true; // Optionally, trim the otherwise silent samples, for even faster responses.
            var segmentationStrategy = new KokoroTTSPipelineConfig() { SecondsOfPauseBetweenProperSegments = new(CommaPause: 0f) };

            // From here on, these will enqueue to the same `playback` instance, ensuring audio will not overlap.
            // Also, the callbacks are built-in inside `KokoroTTS`, so if you want them, you'd have to create your own.
            // Feel free to check out how it's done there, use it as an example, and tweak it to your liking!
            int[] tokens = Tokenizer.Tokenize(txt); // (1D array)
            List<int[]> ttokens = SegmentationSystem.SplitToSegments(tokens, new DefaultSegmentationConfig() { MaxFirstSegmentLength = 100 }); // (2D array)


            // Mixing voices is easy, and you can mix as many as you want together, even ones intended for different languages!
            // .. Note that doing that might result in potential artifacts on the spoken text when the mixed weight is high.
            var mixedVoice = KokoroVoiceManager.Mix([(sarah, Mix.a), (nicole, Mix.b), (KokoroVoiceManager.GetVoice("hf_beta"), Mix.c)]);

            // The library will try to infer the desired language, but if you wanna be sure the language does indeed match, you need to specify so.
            mixedVoice.Rename("Mixed Voice", KokoroLanguage.BritishEnglish, KokoroGender.Female);

            // You can inference with a 1D token array, waiting until the full inference completes before hearing back (up to 510 tokens).
            tts.EnqueueJob(KokoroJob.Create(tokens, mixedVoice, speed:1f, playback.Enqueue));

            // Or with 2D token array, processing them segment-by-segment, hearing back as quickly as possible (same with `tts.SpeakFast()`).
            // .. 2D arrays are not restricted by the 510 token limit, because none of the segments will surpass that.
            tts.EnqueueJob(KokoroJob.Create(ttokens, sarah, speed:1f, playback.Enqueue));

            // BTW, you can customize the pipeline in any way you want. Here's an example on how to add a 2.5 second pause.
            { tts.EnqueueJob(KokoroJob.Create(Tokenizer.Tokenize("Pausing for 2 sec"), nicole, 1, playback.Enqueue)); } // just for clarity to know when the pause occurs.
            tts.EnqueueJob(new KokoroPauseJob() { PauseTime = 2f, OnComplete = playback.Enqueue });

            // And can also manually load the voice from the path you want, as a float array...
            float[,,] michaelNPY = NumSharp.np.Load<float[,,]>(@"voices/am_michael.npy");
            tts.EnqueueJob(KokoroJob.Create(ttokens, michaelNPY, speed:0.8f, playback.Enqueue));

            // ...or as a KokoroVoice. Those types are fully interchangeable with each other.
            KokoroVoice onyxVoice = KokoroVoice.FromPath(@"voices/am_onyx.npy");
            tts.EnqueueJob(KokoroJob.Create(ttokens, onyxVoice, speed:1.2f, playback.Enqueue));
        }
    }

    /// <summary> Simple example of a "Pause" job that will cause the playback to wait for a fixed amount of seconds before playing back the next audio in queue. </summary>
    /// <remarks> Of course, this pause could have happened mid-segment if we chose to, but for the sake of simplicity, it'll just delay the next speaker. </remarks>
    internal sealed class KokoroPauseJob : KokoroJob {
        public float PauseTime { get; init; }

        public Action<float[]> OnComplete { get; init; }

        /// <summary> Instantly responds with an empty array for 'PauseTime' amount of seconds, causing the playback buffer to play some empty audio. </summary>
        public override void Progress(KokoroModel model) {
            OnComplete?.Invoke(new float[(int) Math.Round(KokoroPlayback.waveFormat.SampleRate * PauseTime)]);
            State = KokoroJobState.Completed;
        }
    }

}

namespace KokoroSharp;

using KokoroSharp.Core;
using KokoroSharp.Processing;

using Microsoft.ML.OnnxRuntime;

using System.Diagnostics;

/// <summary> Highest level module that allows easy inference with the model. </summary>
/// <remarks> Contains a background worker thread that dispatches queued jobs/actions linearly. </remarks>
public sealed partial class KokoroTTS : KokoroEngine {
    /// <summary> Callback raised when playback for given speech request just started. </summary>
    /// <remarks> Can be used to retrieve info about the original task, including spoken text, and phonemes. </remarks>
    public event Action<SpeechStartPacket> OnSpeechStarted;

    /// <summary> Callback raised when a text segment was spoken successfully, progressing the speech to the next segment. </summary>
    /// <remarks> Note that some contents of this packet are GUESSED, which means they might not be accurate. </remarks>
    public event Action<SpeechProgressPacket> OnSpeechProgressed;

    /// <summary> Callback raised when the whole given text was spoken successfully. </summary>
    /// <remarks> Can be used to retrieve info about the original task, including spoken text, and phonemes. </remarks>
    public event Action<SpeechCompletionPacket> OnSpeechCompleted;

    /// <summary> Callback raised when the playback was stopped amidst speech. Can retrieve which parts were spoken, in part or in full. </summary>
    /// <remarks> Note that "Cancel" will NOT BE CALLED for packets whose playback never ever started. </remarks>
    public event Action<SpeechCancellationPacket> OnSpeechCanceled;

    /// <summary> If true, the output audio of the model will be *nicified* before being played back. </summary>
    /// <remarks> Nicification includes trimming silent start and finish, and attempting to reduce noise. </remarks>
    public bool NicifyAudio {
        get => playbackInstance.NicifySamples;
        set => playbackInstance.NicifySamples = value;
    }

    KokoroPlayback playbackInstance = new();
    SynthesisHandle currentHandle = new();
    KokoroTTSPipelineConfig defaultPipelineConfig = new();

    /// <summary>
    /// Creates a new Kokoro TTS Engine instance, loading the model into memory and initializing a background worker thread to continuously scan for newly queued jobs, dispatching them in order, when it's free.
    /// <para> If 'options' is specified, the model will be loaded with them. This is particularly useful when needing to run on non-CPU backends, as the default backend otherwise is the CPU with 8 threads. </para>
    /// <para> The model(s) can be found at https://github.com/taylorchu/kokoro-onnx/releases/tag/v0.2.0. </para>
    /// </summary>
    public KokoroTTS(string modelPath, SessionOptions options = null) : base(modelPath, options) { }

    /// <summary> Speaks the text with the specified voice, without segmenting it (max 510 tokens), resulting in a slower, yet potentially higher quality response. </summary>
    /// <remarks> This is the simplest, highest-level interface of the library. For more fine-grained controls, see <see cref="KokoroEngine"/>. </remarks>
    /// <param name="text"> The text to speak. </param>
    /// <param name="voice"> The voice that will speak it. Can be loaded via <see cref="KokoroVoiceManager.GetVoice(string)"/>. </param>
    /// <returns> A handle with delegates regarding speech progress. Those can be subscribed to for updates regarding the lifetime of the synthesis. </returns>
    public SynthesisHandle Speak(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = default)
        => Speak_Phonemes(text, Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), pipelineConfig?.PreprocessText ?? true), voice, pipelineConfig, fast: false);

    /// <summary> Segments the text before speaking it with the specified voice, resulting in an almost immediate response for the first chunk, with a potential hit in quality. </summary>
    /// <remarks> This is the simplest, highest-level interface of the library. For more fine-grained controls, see <see cref="KokoroEngine"/>. </remarks>
    /// <param name="text"> The text to speak. </param>
    /// <param name="voice"> The voice that will speak it. Can be loaded via <see cref="KokoroVoiceManager.GetVoice(string)"/>. </param>
    /// <returns> A handle with delegates regarding speech progress. Those can be subscribed to for updates regarding the lifetime of the synthesis. </returns>
    public SynthesisHandle SpeakFast(string text, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = default)
        => Speak_Phonemes(text, Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), pipelineConfig?.PreprocessText ?? true), voice, pipelineConfig, fast: true);

    /// <summary> Optional way to speak a pre-phonemized input. For actual <b>"text"</b>-to-speech inference, use <b>Speak(..)</b> and <b>SpeakFast(..)</b>. </summary>
    /// <remarks> Specifying 'fast = true' will segment the audio before speaking it. Token arrays of length longer than the model's max (510 tokens) will be trimmed otherwise. </remarks>
    /// <returns> A handle with delegates regarding speech progress. Those can be subscribed to for updates regarding the lifetime of the synthesis. </returns>
    public SynthesisHandle Speak_Phonemes(string text, int[] tokens, KokoroVoice voice, KokoroTTSPipelineConfig pipelineConfig = null, bool fast = true) {
        StopPlayback();
        pipelineConfig ??= defaultPipelineConfig;
        var ttokens = fast ? pipelineConfig.SegmentationFunc(tokens) : [tokens];
        var job = EnqueueJob(KokoroJob.Create(ttokens, voice, pipelineConfig.Speed, null));

        var phonemesCache = ttokens.Count > 1 ? new List<char>() : null;
        currentHandle = new SynthesisHandle() { Job = job, TextToSpeak = text };
        foreach (var step in job.Steps) {
            step.OnStepStarted  = () => currentHandle.OnInferenceStepStarted?.Invoke(step);
            step.OnStepComplete = (samples) => EnqueueWithCallbacks(samples, text, ttokens, step, job, currentHandle, pipelineConfig, phonemesCache);
            Debug.WriteLine($"[step {job.Steps.IndexOf(step)}: {new string(step.Tokens.Select(x => Tokenizer.TokenToChar[x]).ToArray())}]".Replace("\n", "®"));
        }
        return currentHandle;
    }


    /// <summary> Immediately cancels any ongoing playbacks and requests triggered by any of the "Speak" methods. </summary>
    public void StopPlayback() {
        currentHandle.Job?.Cancel();
        currentHandle.ReadyPlaybackHandles.ForEach(x => x.Abort());
    }

    /// <inheritdoc/>
    public override void Dispose() {
        StopPlayback();
        playbackInstance.Dispose();
        base.Dispose();
    }

    /// <summary> This is a callback that gets invoked with the model's outputs (/audio samples) as parameters, once an inference job's step is complete. </summary>
    /// <remarks> It in turn relays those samples to the <see cref="KokoroPlayback"/> instance, and sets up follow-up callbacks regarding playback progress. </remarks>
    void EnqueueWithCallbacks(float[] samples, string text, List<int[]> allTokens, KokoroJob.KokoroJobStep step, KokoroJob job, SynthesisHandle handle, KokoroTTSPipelineConfig pipelineConfig, List<char> phonemesCache = null) {
        phonemesCache ??= [];
        var allPhonemesToSpeak = job.Steps.SelectMany(x => x.Tokens ?? []).Select(x => Tokenizer.TokenToChar[x]).ToArray();
        var playbackHandle = playbackInstance.Enqueue(samples, OnStartedCallback, OnCompleteCallback, OnCanceledCallback);
        handle.ReadyPlaybackHandles.Add(playbackHandle); // Mark the inference as "completed" and register the playback handle as "ready".
        handle.OnInferenceStepCompleted?.Invoke(step, playbackHandle); // Notify end users that the step is complete, and pass the handle.

        // Finally, if the segment is gracefully ended with a punctuation token, add some pause to it, to emulate natural pause.
        bool shouldAddPause = NicifyAudio && pipelineConfig != null && Tokenizer.PunctuationTokens.Contains(step.Tokens[^1]);
        if (shouldAddPause) {
            var secondsToWait = pipelineConfig.SecondsOfPauseBetweenProperSegments[Tokenizer.TokenToChar[step.Tokens[^1]]];
            var pauseHandle = playbackInstance.Enqueue(new float[(int) (secondsToWait * KokoroPlayback.waveFormat.SampleRate)], null, null, null);
            playbackHandle.OnCanceled += (_) => pauseHandle.Abort(); // Last but not least, register the cancel/abort callbacks for the pause, so the playback buffer won't bloat.
            playbackHandle.OnAborted += () => pauseHandle.Abort();   // The users don't need to be bothered with having access to these, but it's important for us to handle them.
        }


        // Callbacks
        void OnStartedCallback() { // We need to add the SpeechStarted callback, but only to the very first segment.
            if ((OnSpeechStarted == null && handle.OnSpeechStarted == null) || step != job.Steps[0]) { return; }
            var startPacket = new SpeechStartPacket() {
                RelatedJob = job,
                TextToSpeak = text,
                PhonemesToSpeak = allPhonemesToSpeak,
            };
            OnSpeechStarted?.Invoke(startPacket);
            handle.OnSpeechStarted?.Invoke(startPacket);
        }
        void OnCompleteCallback() {
            if (OnSpeechProgressed == null && handle.OnSpeechProgressed == null && OnSpeechCompleted == null && handle.OnSpeechCompleted == null) { return; }

            var phonemes = step.Tokens.Select(x => Tokenizer.TokenToChar[x]).ToArray();
            phonemesCache.AddRange(phonemes);

            // After each segment is complete, invoke the SpeechProgressed callback.
            if (OnSpeechProgressed != null || handle.OnSpeechProgressed != null) {
                var progressPacket = new SpeechProgressPacket() {
                    RelatedJob = job,
                    RelatedStep = step,
                    SpokenText_BestGuess = step == job.Steps[^1] ? text : MakeBestGuess(1, phonemes),
                    PhonemesSpoken = phonemes,
                };
                OnSpeechProgressed?.Invoke(progressPacket);
                handle.OnSpeechProgressed?.Invoke(progressPacket);
            }

            // We also need to add the SpeechCompletion callback, but only to the very last segment.
            if ((OnSpeechCompleted != null || handle.OnSpeechCompleted != null) && step == job.Steps[^1]) {
                var completionPacket = new SpeechCompletionPacket() {
                    RelatedJob = job,
                    RelatedStep = step,
                    PhonemesSpoken = [.. phonemesCache],
                    SpokenText = text,
                };
                OnSpeechCompleted?.Invoke(completionPacket);
                handle.OnSpeechCompleted?.Invoke(completionPacket);
            }
        }
        void OnCanceledCallback((float time, float percentage) t) {
            if (OnSpeechCanceled == null && handle.OnSpeechCanceled == null) { return; }
            // Let's assume the amount of spoken phonemes linearly matches the percentage.
            var T = (int) Math.Round(step.Tokens.Length * t.percentage); // L * t
            var phonemesSpokenGuess = step.Tokens.Take(T).Select(x => Tokenizer.TokenToChar[x]);
            var cancellationPacket = new SpeechCancellationPacket() {
                RelatedJob = job,
                RelatedStep = step,
                SpokenText_BestGuess = MakeBestGuess(t.percentage, step.Tokens.Select(x => Tokenizer.TokenToChar[x]).ToArray()),
                PhonemesSpoken_BestGuess = [.. phonemesCache, .. phonemesSpokenGuess],
                PhonemesSpoken_PrevSegments_Certain = [.. phonemesCache],
                PhonemesSpoken_LastSegment_BestGuess = [.. phonemesSpokenGuess]
            };
            OnSpeechCanceled?.Invoke(cancellationPacket);
            handle.OnSpeechCanceled?.Invoke(cancellationPacket);
            phonemesCache.AddRange(phonemesSpokenGuess);
        }

        string MakeBestGuess(float percentage, char[] segmentPhonemes) {
            var packet = new SpeechInfoPacket() {
                OriginalText = text,
                AllTokens = allTokens,
                AllPhonemes = allPhonemesToSpeak,
                PreSpokenPhonemes = [.. phonemesCache],
                SegmentPhonemes = segmentPhonemes,
                SegmentIndex = job.Steps.IndexOf(step),
                SegmentCutT = percentage
            };
            return SpeechGuesser.GuessSpeech_LowEffort(packet, pipelineConfig);
        }
    }
}

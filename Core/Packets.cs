namespace KokoroSharp.Core;

using KokoroSharp.Processing;

/// <summary> Callback packet that gets sent when the speech playback starts. Called only once, regardless of segmentation. </summary>
/// <remarks> Contains info about the full text that is to be spoken, and its phonemized form. </remarks>
public struct SpeechStartPacket {
    /// <summary> The full list of phonemes that started being spoken. </summary>
    public char[] PhonemesToSpeak;

    /// <summary> The full text that started being spoken. </summary>
    public string TextToSpeak;

    /// <summary> The Kokoro Job this speech packet is connected to. </summary>
    public KokoroJob RelatedJob;
}

/// <summary> Callback packet that gets sent when part of the speech playback was completed. </summary>
/// <remarks> Contains info about the part that was spoken since the previous packet was sent. </remarks>
public struct SpeechProgressPacket {
    /// <summary> The phonemes that were spoken since the previous "SpeechProgress" packet was sent. </summary>
    /// <remarks> Note that unlike <b>SpokenText_BestGuess</b>, these will be 100% accurate. </remarks>
    public char[] PhonemesSpoken;

    /// <summary> The text that was spoken since the beginning of this speech/KokoroJob... <b>probably (!)</b> </summary>
    /// <remarks> <b>NOTE:</b> It might not be accurate because Kokoro doesn't provide per-spoken-phoneme info to ONNX, so we can only infer segments. </remarks>
    public string SpokenText;

    /// <summary> The Kokoro Job this speech packet is connected to. </summary>
    public KokoroJob RelatedJob;

    /// <summary> The Kokoro Job Step this speech packet is connected to. </summary>
    public KokoroJob.KokoroJobStep RelatedStep;
}

/// <summary> Callback packet that gets sent when the speech playback was interrupted. </summary>
/// <remarks> Note that "Cancel" will be SKIPPED for packets whose playback was aborted without ever starting. </remarks>
public struct SpeechCancellationPacket {

    /// <summary> The phonemes that were spoken since the beginning of this speech/KokoroJob... <b>probably (!)</b> </summary>
    public char[] PhonemesSpoken;

    /// <summary> The text that was spoken since the beginning of this speech/KokoroJob... <b>probably (!)</b> </summary>
    public string SpokenText;

    /// <summary> The Kokoro Job this speech packet is connected to. </summary>
    public KokoroJob RelatedJob;

    /// <summary> The Kokoro Job Step this speech packet is connected to. </summary>
    public KokoroJob.KokoroJobStep RelatedStep;
}

/// <summary> Callback packet that gets sent when the speech playback completes successfully. </summary>
public struct SpeechCompletionPacket {
    /// <summary> The phonemes that were spoken during this speech/KokoroJob. </summary>
    public char[] PhonemesSpoken;

    /// <summary> The text that was spoken during this speech/KokoroJob. </summary>
    public string SpokenText;

    /// <summary> The Kokoro Job this speech packet is connected to. </summary>
    public KokoroJob RelatedJob;

    /// <summary> The Kokoro Job Step this speech packet is connected to. </summary>
    public KokoroJob.KokoroJobStep RelatedStep;
}

/// <summary> A packet that contains info regarding the current state of the speech, helpful for guessing the spoken parts. </summary>
public struct SpeechInfoPacket {
    /// <summary> The whole text that the speech job of interest has to speak. </summary>
    public string OriginalText;

    /// <summary> ALL tokens of phonemes that the speech job of interest has to speak, nicely segmented. </summary>
    public IReadOnlyList<int[]> AllTokens;

    /// <summary> The phonemes of segment that have been already spoken. </summary>
    public char[] PreSpokenPhonemes;

    /// <summary> The phonemes of the current segment. </summary>
    public char[] SegmentPhonemes;

    /// <summary> Exact timings for the current segment's phonemes. Null if the model doesn't output durations. </summary>
    public PhonemeTimestamp[] SegmentTimestamps;

    /// <summary> ALL phonemes that the speech job of interest has to speak. </summary>
    public char[] AllPhonemes;

    /// <summary> The index of the segment we're trying to guess spoken text for. </summary>
    public int SegmentIndex;

    /// <summary> The percentage in which the current segment was cut. [0, 1]. </summary>
    /// <remarks> If the speech was NOT canceled, this should have a value of '1'. </remarks>
    public float SegmentCutT;

    /// <summary> Seconds of the current segment's raw audio played so far. Lines up with <see cref="SegmentTimestamps"/>. </summary>
    public float SegmentPlayedSeconds;
}

/// <summary> Callback packet that gets sent in real time when a phoneme starts being spoken during playback. </summary>
/// <remarks> Useful for lip-sync (visemes) and word highlighting. </remarks>
public struct PhonemeReachedPacket {
    /// <summary> The phoneme that just started being spoken, with its exact timing within the current segment's raw audio. </summary>
    public PhonemeTimestamp Timestamp;

    /// <summary> The Kokoro Job this speech packet is connected to. </summary>
    public KokoroJob RelatedJob;

    /// <summary> The Kokoro Job Step this speech packet is connected to. </summary>
    public KokoroJob.KokoroJobStep RelatedStep;
}

/// <summary> The exact time span a single phoneme occupies within its segment's audio. </summary>
/// <remarks> Produced from the model's 'durations' output. </remarks>
public readonly struct PhonemeTimestamp {
    /// <summary> The phoneme character, as found in <see cref="Tokenizer.Vocab"/>. </summary>
    public char Phoneme { get; init; }

    /// <summary> The second (relative to the segment's raw audio) this phoneme starts being spoken. </summary>
    public float StartSecond { get; init; }

    /// <summary> The second (relative to the segment's raw audio) this phoneme stops being spoken. </summary>
    public float EndSecond { get; init; }

    /// <summary> Maps the model's padded per-token durations onto the step's tokens, distributing the audio's actual length proportionally. </summary>
    /// <remarks> Returns null when durations are unavailable or misaligned (models without a 'durations' output, or inputs that got trimmed to the token limit). </remarks>
    public static PhonemeTimestamp[] FromModelOutput(int[] tokens, int[] paddedDurations, int sampleCount) {
        if (paddedDurations == null || paddedDurations.Length != tokens.Length + 2 || sampleCount == 0) { return null; }
        var secondsPerUnit = sampleCount / (float) 24_000 / paddedDurations.Sum();
        var timestamps = new PhonemeTimestamp[tokens.Length];
        var elapsedUnits = paddedDurations[0]; // [0] and [^1] belong to the <start>/<end> padding tokens.
        for (int i = 0; i < tokens.Length; i++) {
            var startSecond = elapsedUnits * secondsPerUnit;
            elapsedUnits += paddedDurations[i + 1];
            timestamps[i] = new() { Phoneme = Tokenizer.TokenToChar.GetValueOrDefault(tokens[i]), StartSecond = startSecond, EndSecond = elapsedUnits * secondsPerUnit };
        }
        return timestamps;
    }
}

namespace KokoroSharp.Core;

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
    public string SpokenText_BestGuess;

    /// <summary> The text that was spoken since the previous "SpeechProgress" packet was sent... <b>probably (!)</b> </summary>
    /// <remarks> <b>NOTE:</b> It might not be accurate because Kokoro doesn't provide per-spoken-phoneme info to ONNX, so we can only infer segments. </remarks>
    //public string NewlySpokenText_BestGuess;

    /// <summary> The Kokoro Job this speech packet is connected to. </summary>
    public KokoroJob RelatedJob;

    /// <summary> The Kokoro Job Step this speech packet is connected to. </summary>
    public KokoroJob.KokoroJobStep RelatedStep;
}

/// <summary> Callback packet that gets sent when the speech playback was interrupted. </summary>
/// <remarks> Note that "Cancel" will be SKIPPED for packets whose playback was aborted without ever starting. </remarks>
public struct SpeechCancellationPacket {

    /// <summary> The phonemes that were spoken since the beginning of this speech/KokoroJob. </summary>
    /// <remarks> Note that these have <b>INDEED</b> been spoken but they do NOT include the last segment's phonemes. </remarks>
    public char[] PhonemesSpoken_PrevSegments_Certain;

    /// <summary> The phonemes that were spoken on the last segment before cancellation... <b>probably (!)</b> </summary>
    /// <remarks> Note that these ONLY include the last segment's <b>best guess</b> of phonemes, based on the percentage spoken. </remarks>
    public char[] PhonemesSpoken_LastSegment_BestGuess;

    /// <summary> The phonemes that were spoken since the beginning of this speech/KokoroJob... <b>probably (!)</b> </summary>
    /// <remarks> Note that ones on the last segment will likely NOT be accurate, as they're based on the percentage spoken. </remarks>
    public char[] PhonemesSpoken_BestGuess;

    /// <summary> The text that was spoken since the beginning of this speech/KokoroJob... <b>probably (!)</b> </summary>
    /// <remarks> <b>NOTE:</b> It might not be accurate because Kokoro doesn't provide per-spoken-phoneme info to ONNX, so we can only infer segments. </remarks>
    public string SpokenText_BestGuess;

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

    /// <summary> ALL phonemes that the speech job of interest has to speak. </summary>
    public char[] AllPhonemes;

    /// <summary> The index of the segment we're trying to guess spoken text for. </summary>
    public int SegmentIndex;

    /// <summary> The percentage in which the current segment was cut. [0, 1]. </summary>
    /// <remarks> If the speech was NOT canceled, this should have a value of '1'. </remarks>
    public float SegmentCutT;
}

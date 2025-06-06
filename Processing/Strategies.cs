namespace KokoroSharp.Processing;


/// <summary> A way for users to control how segmentation happens over the tokens, allowing 100% custom solutions. </summary>
/// <remarks> Essentially a <see cref="Func{T, TResult}"/> with input tokens as <b>T</b>, and list of segments (tokens) as <b>TResult</b>. </remarks>
/// <param name="tokens">The list of tokens to apply segmentation on.</param>
/// <returns>The segments the original tokens were chunked to. Each segment will be converted to audio separately, and will be played separately as well. </returns>
public delegate List<int[]> SegmentationDelegate(int[] tokens);

/// <summary>
/// <para> Allows defining various rules regarding the TTS pipeline. Has nice defaults, and is fully customizable. </para>
/// <para> <b>- Segmentation:</b> allows *chunking* the text so the first parts of it will be processed quicker. </para>
/// <para> <b>- Speech Pauses:</b> allows fine-tuning the pauses between segments, based on the punctuation character that separates the two segments. </para>
/// </summary>
public class KokoroTTSPipelineConfig {
    /// <summary> Single-handedly handles the "text chunking". Can be customized to handle specific use-cases and application needs dynamically. </summary>
    /// <remarks> Happens <b>ONCE per 'Speak'/'SpeakFast'</b> call. Defaults to <see cref="SegmentationSystem.SplitToSegments(int[], DefaultSegmentationConfig)"/>. </remarks>
    public SegmentationDelegate SegmentationFunc = (tokens) => {
        var segmentedTokens = SegmentationSystem.SplitToSegments(tokens, new());
        return segmentedTokens; // A List of token arrays.
    };

    /// <summary> Defines amount of seconds will be injected as empty audio between segments that end in a proper punctuation. </summary>
    /// <remarks> This will allow us to emulate natural pause even on the nicified audio (<see cref="KokoroPlayback.NicifySamples"/>). <b>NOTE:</b> Segments that end on a space or mid-word will <b>NOT</b> get any additional pause. </remarks>
    public PauseAfterSegmentStrategy SecondsOfPauseBetweenProperSegments = new();

    /// <summary> Control the 'Speed' of the speech. Recommended range: [0.5, 1.3]. </summary>
    /// <remarks> This DIRECTLY affects the generated samples -- not only the playback. </remarks>
    public float Speed { get; set; } = 1;

    /// <summary> Toggles whether the text should be pre-processed before tokenization. Can be toggled off if text is already pre-processed, or the default pre-processing is not ideal on a specific use-case. </summary>
    /// <remarks> Preprocessing includes various nicifications like preserving symbols that wouldn't be caught by espeak-ng, and some other speaking conveniences that usually apply <b>(e.g.: $1 to "one dollar" instead of "dollar one")</b>. </remarks>
    public bool PreprocessText { get; set; } = true;

    /// <summary> A pipeline config that uses the KokoroSharp defaults for the whole TTS pipeline. </summary>
    public KokoroTTSPipelineConfig() { }

    /// <summary> A pipeline config that uses a user-defined segmentation strategy. </summary>
    public KokoroTTSPipelineConfig(SegmentationDelegate SegmentationMethod) : this() => SegmentationFunc = SegmentationMethod;

    /// <summary> A pipeline config that uses the default segmentation strategy with custom segmentation parameters. </summary>
    public KokoroTTSPipelineConfig(DefaultSegmentationConfig segmentationConfig) : this() => SegmentationFunc = (t) => SegmentationSystem.SplitToSegments(t, segmentationConfig);
}

/// <summary>
/// <para> Helper class that allows defining amount of seconds will be injected as empty audio between segments that end in a proper punctuation. </para>
/// <para> This will allow us to emulate natural pause even on the nicified audio (<see cref="KokoroPlayback.NicifySamples"/>). </para>
/// <b>NOTE:</b> Only segments that END with one of the letters will receive artificial pauses. Segments that just "speak" one of the ending tokens will not be affected.
/// </summary>
public class PauseAfterSegmentStrategy {
    /// <summary> The amount of seconds that should be waited after a segment with specific punctuation on the end was spoken. </summary>
    public float this[char c] => endingPunctuationPauseSecondsMap.TryGetValue(c, out var p) ? p : endingPunctuationPauseSecondsMap['¿'];

    /// <summary> A map containing the amount of seconds that should be waited after a segment with specific punctuation on the end was spoken. </summary>
    IReadOnlyDictionary<char, float> endingPunctuationPauseSecondsMap { get; }

    public PauseAfterSegmentStrategy(float CommaPause = 0.1f, float PeriodPause = 0.5f, float QuestionMarkPause = 0.5f, float ExclamationMarkPause = 0.5f, float NewLinePause = 0.5f, float OthersPause = 0.5f) {
        endingPunctuationPauseSecondsMap = new Dictionary<char, float>() {
            { ',', CommaPause },
            { '.', PeriodPause },
            { '?', QuestionMarkPause },
            { '!', ExclamationMarkPause },
            { '\n', NewLinePause },
            { '¿', OthersPause }
        };
    }
}


/// <summary>
/// <para> Allows defining various rules helpful for customizing the default segmentation pipeline. Segmentation allows *chunking* the text so the first parts of it will be processed quicker. </para>
/// <para> This is crucial to allow seamless audio playback, because follow-up chunks can be processed in the background while the audio output from the previous chunks is playing. </para>
/// <para> <b>The general segmentation rules apply as follows:</b> </para>
/// <para> - First, the algorithm tries segmenting on exact <see cref="PunctuationTokens"/>, within the allowed limits. </para>
/// <para> - .. if there were no punctuation tokens available there, we try to segment on a <see cref="SegmentationSystem.spaceToken"/> that appears within the segment's range. </para>
/// <para> - .. and if no space tokens were found either, we cut at the maximum allowed length. This may cut words in the middle, so plan accordingly. </para>
/// <para> <i>Note: Do not use too small or too long numbers for these sequences. Usually the defaults will work for every machine with a small impact on initial response quality.</i> </para>
/// <para> <i>It is recommended to provide your users with an option on whether they want SUPER FAST, or SUPER GOOD response, and provide different strategies. </i> </para>
/// </summary>
public class DefaultSegmentationConfig {
    /// <summary> The minimum allowed length of the first segment. Ensures the first segment includes AT LEAST this many tokens. </summary>
    /// <remarks> Recommended to keep this small, to allow instant responses. </remarks>
    public int MinFirstSegmentLength = 10;

    /// <summary> The maximum allowed length of the first segment. *NOTE: Having this too small might cut words in the middle* </summary>
    /// <remarks> Recommended to keep this small, but not too small, to allow instant responses. </remarks>
    public int MaxFirstSegmentLength = 100;

    /// <summary> The maximum allowed length of the second segment. *NOTE: Having this too small might cut words in the middle* </summary>
    /// <remarks> Recommended to be a reasonable size based on the first segment's expected length, for seamless audio playback. </remarks>
    public int MaxSecondSegmentLength = 100;

    /// <summary> The minimum allowed length of follow-up segments. Any 100% valid punctuation found after THIS many tokens will mark a new segment. </summary>
    /// <remarks> These can be long since they'll be processed in the background while the audio is playing. *NOTE: Having this too high might delay "CANCEL" operations, since we can't cancel ongoing ONNX requests* </remarks>
    public int MinFollowupSegmentsLength = 200;
}

namespace KokoroSharp.Processing;

using KokoroSharp.Core;


/// <summary>
/// <para> System dedicated to <b>*guessing*</b> the text that has been spoken, given the phonemes that have been spoken, and additional info about the progress of the speech so far. </para>
/// <para> This is particularly useful when needing to synchronize a UI with the ongoing speech (e.g. when canceling or reading along). </para>
/// <para> It supports(*) guess modes of different efforts, selectable based on the amount of processing power you're willing to spare: </para>
/// <para> - The quickest, low-effort mode just returns an estimate based on the current progress percentage of the total text that needs to be spoken. </para>
/// <para> </para>
/// </summary>  
public static class SpeechGuesser {
    static char[] punctuation = [.. Tokenizer.punctuation];

    /// <summary> Guesses the spoken text, based on the phonemes that were spoken, with the algorithm: <b>charsSpoken = (TotalSpokenPercent * TotalCharLength)</b>. </summary>
    /// <remarks> It is quick and should cover most needs. For higher precision, consider [...other modes not implemented yet...]. </remarks>
    /// <returns> A quick guess regarding the text that was spoken. The low-effort mode will likely be inaccurate, but enough for most use-cases. </returns>
    public static string GuessSpeech_LowEffort(SpeechInfoPacket info, KokoroTTSPipelineConfig pipelineConfig) {
        var totalTokens = info.AllTokens.Sum(x => x.Length);
        var bigT = 0f; // The percentage of speech we're on for the TOTAL job.
        for (int i = 0; i < info.SegmentIndex; i++) { bigT += info.AllTokens[i].Length / (float) totalTokens; }
        bigT += info.SegmentCutT * (info.AllTokens[info.SegmentIndex].Length / (float) totalTokens);

        var roughSpokenCharsCountEstimate = (int) Math.Round(bigT * info.OriginalText.Length);
        return info.OriginalText[..roughSpokenCharsCountEstimate]; // very rough guess...
    }

    public static string GuessSpeech_MidEffort(SpeechInfoPacket info, KokoroTTSPipelineConfig pipelineConfig) => throw new NotImplementedException();
    public static string GuessSpeech_HighEffort(SpeechInfoPacket info, KokoroTTSPipelineConfig pipelineConfig) => throw new NotImplementedException();
}

namespace KokoroSharp.Processing;

using KokoroSharp.Core;

using System.Diagnostics;

using static Tokenizer;

/// <summary> Helper class that allows turning text tokens into segments, allowing us to get the first response of the model quicker. </summary>
/// <remarks> This allows us to begin playing back the audio of the first sentence, while the model processes the rest of the sequence on the background. </remarks>
public static class SegmentationSystem {
    static readonly int NLToken = Vocab['\n'];
    static readonly int SpaceToken = Vocab[' '];
    static readonly int[][] cutPreference = [[Vocab['.'], Vocab['!'], Vocab['?'], Vocab[':']], [Vocab[',']], [Vocab[' ']]];

    /// <summary> Turns the input tokens into multiple segments, then returns the segments in a list. Line breaks ALWAYS end a segment, and a line that fits its budget stays whole. </summary>
    /// <remarks> Oversized lines get cut at their latest sentence end within budget, then latest comma, then latest space. Only the first segment has a smaller budget, for quicker playback start. </remarks>
    public static List<int[]> SplitToSegments(int[] tokens, DefaultSegmentationConfig segmentationStrategy) {
        List<int[]> segments = [];
        for (int at = 0; at < tokens.Length;) {
            if (tokens[at] == SpaceToken) { at++; continue; }
            int newLine = Array.IndexOf(tokens, NLToken, at);
            int end = newLine >= 0 ? newLine + 1 : tokens.Length; // The line break rides along as the segment's last token, marking the pause.
            int budget = segments.Count == 0 ? segmentationStrategy.MaxFirstSegmentLength : KokoroModel.maxTokens;
            if (end - at > budget) {
                end = at + budget;
                foreach (var cuts in cutPreference) {
                    if (Array.FindLastIndex(tokens, at + budget - 1, budget, t => cuts.Contains(t)) is var found && found >= at) { end = found + 1; break; }
                }
                while (end - at < KokoroModel.maxTokens && tokens[end] != NLToken && PunctuationTokens.Contains(tokens[end])) { end++; }
            }
            while (end < tokens.Length && (tokens[end] == NLToken || tokens[end] == SpaceToken)) { end++; } // Paragraph gaps ride with the previous segment, as a longer pause.
            int stop = end;
            while (stop > at && tokens[stop - 1] == SpaceToken) { stop--; }
            if (stop > at && !tokens[at..stop].All(t => t == NLToken || t == SpaceToken)) { segments.Add(tokens[at..stop]); }
            Debug.WriteLine($"[{segments.Count}](+{end - at} [{at}/{tokens.Length}]): {new string(tokens[at..stop].Select(x => TokenToChar[x]).ToArray())}".Replace("\n", "®"));
            at = end;
        }
        return segments;
    }
}

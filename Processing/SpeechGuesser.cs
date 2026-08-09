namespace KokoroSharp.Processing;

using KokoroSharp.Core;


/// <summary>
/// <para> System dedicated to <b>*guessing*</b> the text that has been spoken, given the phonemes that have been spoken, and additional info about the progress of the speech so far. </para>
/// <para> This is particularly useful when needing to synchronize a UI with the ongoing speech (e.g. when canceling or reading along). </para>
/// </summary>
public static class SpeechGuesser {
    static HashSet<int> wordSeparatorTokens = [Tokenizer.Vocab[' '], Tokenizer.Vocab['/'], Tokenizer.Vocab['\n']];
    static char[] wordDecorations = [.. ",.!?;:…\"'`()[]{}*#-—«»“”‘’¡¿।॥"];
    static int newLineToken = Tokenizer.Vocab['\n'];

    /// <summary> Guesses the spoken text by counting the words spoken in phoneme space, and returning that many words from the original text. </summary>
    /// <remarks> Guaranteed accuracy up to spoken segment, and guess on the ongoing one. Chinese/Japanese/Korean characters count as one word each. </remarks>
    public static string GuessSpeech(SpeechInfoPacket info) {
        var text = info.OriginalText;
        var spokenInSegment = GuessSpokenTokenCount(info.AllTokens[info.SegmentIndex], info.SegmentTimestamps, info.SegmentCutT, info.SegmentPlayedSeconds);
        var spokenTokens = info.AllTokens.Take(info.SegmentIndex).SelectMany(x => x).Concat(info.AllTokens[info.SegmentIndex].Take(spokenInSegment)).ToList();

        int spokenLines = spokenTokens.Count(t => t == newLineToken);
        int spokenLineWords = CountWords(spokenTokens.Skip(spokenTokens.LastIndexOf(newLineToken) + 1));
        var preprocessed = Tokenizer.PreprocessText(text);
        var lineWordCounts = preprocessed.Split('\n').Select(line => GetWordSpans(line).Count()).ToList();
        int spokenWords = lineWordCounts.Take(Math.Min(spokenLines, lineWordCounts.Count)).Sum()
                        + (spokenLines < lineWordCounts.Count ? Math.Min(spokenLineWords, lineWordCounts[spokenLines]) : 0);
        return text[..EndOfWord(text, AlignSpokenWords(text, preprocessed, spokenWords))];



        static int CountWords(IEnumerable<int> tokens) {
            var (words, inWord) = (0, false);
            foreach (var token in tokens) {
                if (!wordSeparatorTokens.Contains(token) && !inWord) { words++; }
                inWord = !wordSeparatorTokens.Contains(token);
            }
            return words;
        }
        static int EndOfWord(string text, int words) { // char index after Nth word
            var end = 0;
            foreach (var span in GetWordSpans(text).Take(words)) { end = span.end; }
            return end;
        }
    }

    /// <summary> Counts how many of the segment's tokens have been spoken after the given playback progress. </summary>
    /// <remarks> Exact when the model provided timestamps, otherwise assumes the tokens linearly match the cut percentage. </remarks>
    public static int GuessSpokenTokenCount(int[] segmentTokens, PhonemeTimestamp[] timestamps, float segmentCutT, float playedSeconds) {
        if (timestamps is { Length: > 0 }) { return timestamps.Count(x => x.StartSecond < playedSeconds); }
        return (int) Math.Round(segmentTokens.Length * segmentCutT);
    }


    /// <summary> Finds how many original-text words correspond to the given count of spoken preprocessed words (e.g. "$5" -> "five dollars"). </summary>
    static int AlignSpokenWords(string text, string preprocessed, int prepSpoken) {
        var (orig, prep) = (WordsOf(text), WordsOf(preprocessed));

        var (oi, pj) = (0, 0);
        while (pj < prepSpoken && oi < orig.Count) {
            if (orig[oi] == prep[pj]) { (oi, pj) = (oi + 1, pj + 1); continue; }
            var (gapO, gapP) = NextMatchGap(orig, prep, oi, pj);
            if (pj + gapP >= prepSpoken) { return oi + (int) Math.Ceiling(gapO * ((prepSpoken - pj) / (double) Math.Max(1, gapP))); }
            (oi, pj) = (oi + gapO, pj + gapP);
        }
        return oi;



        // Fully-trimmed words ("##", "-") stay raw, so they can't match each other across lines.
        List<string> WordsOf(string t) => [.. GetWordSpans(t).Select(x => t[x.start..x.end].Trim(wordDecorations) is { Length: > 0 } word ? word.ToLowerInvariant() : t[x.start..x.end])];

        // Returns the distance to the closest pair of matching words ahead, preferring the smallest total displacement.
        static (int gapO, int gapP) NextMatchGap(List<string> orig, List<string> prep, int oi, int pj) {
            for (int distance = 1; distance <= 100; distance++) {
                for (int dO = Math.Max(0, distance - (prep.Count - 1 - pj)); dO <= Math.Min(distance, orig.Count - 1 - oi); dO++) {
                    if (orig[oi + dO] == prep[pj + distance - dO]) { return (dO, distance - dO); }
                }
            }
            return (orig.Count - oi, prep.Count - pj);
        }
    }

    /// <summary> Iterates the words of the text. Chinese/Japanese/Korean characters count as their own word. </summary>
    static IEnumerable<(int start, int end)> GetWordSpans(string text) {
        for (int i = 0; i < text.Length;) {
            while (i < text.Length && char.IsWhiteSpace(text[i])) { i++; }
            if (i >= text.Length) { yield break; }
            var start = i;
            if (IsCJK(text[i])) { i++; }
            else { while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsCJK(text[i])) { i++; } }
            yield return (start, i);
        }

        bool IsCJK(char c) => c >= 0x2E80;
    }
}

﻿namespace KokoroSharp.Tokenization;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

/// <summary> A static module responsible for tokenization converting plaintext to phonemes, and phonemes to tokens. </summary>
/// <remarks>
/// <para> Internally preprocesses and post-processes the input text to bring it closer to what the model expects to see. </para>
/// <para> Phonemization happens via the espeak-ng library: <b>https://github.com/espeak-ng/espeak-ng/blob/master/docs/guide.md</b> </para>
/// </remarks>
public static class Tokenizer {
    static string[] charsToReplace = ["\n", "(", ")", "[", "]"];  // We replace these characters with the ':' token, so they'll be caught by espeak-ng.
    internal static HashSet<char> superstopSymbols = [.. ":"];  // Perfect for segmentation.
    internal static HashSet<char> punctuation = [.. ":,.!?"];   // Lines split on any of these occurences, by design via espeak-ng.
    static Dictionary<char, string> currencies = new() { { '$', "dollar" }, { '€', "euro" }, { '£', "pound" }, { '¥', "yen" }, { '₹', "rupee" }, { '₽', "ruble" }, { '₩', "won" }, { '₺', "lira" }, { '₫', "dong" } };

    public static IReadOnlyDictionary<char, int> Vocab { get; }
    public static IReadOnlyDictionary<int, char> TokenToChar { get; }
    public static HashSet<int> PunctuationTokens { get; }

    static Tokenizer() {
        var symbols = new List<char>();
        symbols.Add('$'); // <pad> token
        symbols.AddRange(";:,.!?¡¿—…\"«»“” ".ToCharArray());
        symbols.AddRange("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray());
        symbols.AddRange("ɑɐɒæɓʙβɔɕçɗɖðʤəɘɚɛɜɝɞɟʄɡɠɢʛɦɧħɥʜɨɪʝɭɬɫɮʟɱɯɰŋɳɲɴøɵɸθœɶʘɹɺɾɻʀʁɽʂʃʈʧʉʊʋⱱʌɣɤʍχʎʏʑʐʒʔʡʕʢǀǁǂǃˈˌːˑʼʴʰʱʲʷˠˤ˞↓↑→↗↘'̩'ᵻ".ToCharArray());

        var (c2t, t2c) = (new Dictionary<char, int>(), new Dictionary<int, char>());
        for (int i = 0; i < symbols.Count; i++) {
            c2t[symbols[i]] = i;
            t2c[i] = symbols[i];
        }
        (Vocab, TokenToChar) = (c2t, t2c);
        PunctuationTokens = punctuation.Select(x => Vocab[x]).ToHashSet();
    }

    /// <summary> Tokenizes pre-phonemized input "as-is", mapping to a token array directly usable by Kokoro. </summary>
    /// <remarks> This is intended to act as a solution for platforms that do not support the eSpeak-NG backend. </remarks>
    public static int[] TokenizePhonemes(char[] phonemes) => phonemes.Select(x => Vocab[x]).ToArray();

    /// <summary>
    /// <para> Converts the input text to phoneme tokens, directly usable by Kokoro. </para>
    /// <para> Internally phonemizes the input text via eSpeak-NG, so this will not work on platforms like Android/iOS.</para>
    /// <para> For such platforms, developers are expected to use their own phonemization solution and tokenize using <see cref="TokenizePhonemes(char[])"/>.</para>
    /// </summary>
    public static int[] Tokenize(string inputText, string langCode = "en-us", bool preprocess = true) => Phonemize(inputText, langCode, preprocess).Select(x => Vocab[x]).ToArray();


    /// <summary> Converts the input text into the corresponding phonemes, with slight preprocessing and post-processing to preserve punctuation and other TTS essentials. </summary>
    static string Phonemize(string inputText, string langCode, bool preprocess = true) {
        var preprocessedText = preprocess ? PreprocessText(inputText) : inputText;
        var phonemeList = Phonemize_Internal(preprocessedText, out _, langCode).Split('\n');
        return PostProcessPhonemes(preprocessedText, phonemeList, langCode);
    }

    /// <summary> Invokes the espeak-ng via command line, to convert given text into phonemes. </summary>
    /// <remarks> Espeak will return a line ending when it meets any of the <see cref="PunctuationTokens"/> and gets rid of any punctuation, so these will have to be converted back to a single-line, with the punctuation restored. </remarks>
    static string Phonemize_Internal(string text, out string originalSegments, string langCode = "en-us") {
        var espeak_cli_path = OperatingSystem.IsWindows() ? @$"{Directory.GetCurrentDirectory()}\espeak\espeak-ng" : "espeak-ng";
        using var process = new Process() {
            StartInfo = new ProcessStartInfo() {
                FileName = espeak_cli_path,
                WorkingDirectory = null,
                Arguments = $"--ipa=3 -q -v {langCode} \"{text}\"",
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8
            }
        };
        process.StartInfo.EnvironmentVariables.Add("ESPEAK_DATA_PATH", @$"{Directory.GetCurrentDirectory()}\espeak\espeak-ng-data");
        process.Start();
        originalSegments = process.StandardOutput.ReadToEnd();
        process.StandardOutput.Close();

        return originalSegments.Replace("\r\n", "\n").Trim();
    }

    /// <summary> Normalizes the input text to what the Kokoro model would expect to see, preparing it for phonemization. </summary>
    internal static string PreprocessText(string text) {
        text = text.Normalize().Replace("\r\n", "\n").Replace("“", "").Replace("”", "").Replace("«", "").Replace("»", "").Replace("\"", "").Replace("**", "*");
        const string t = "l®fÆ22";
        foreach (var c in charsToReplace) { text = text.Replace(c, t); } // Replace chars espeak-ng wouldn't catch with the ':' character.
        while (text.Contains(t + t)) { text = text.Replace(t + t, t); }  // Then, try to remove all duplicates. This'll be just the first pass.
        text = text.Replace(t, ":"); // Now we have got rid of all the duplicate symbol punctuations, and we'll preserve them as ':' characters.

        text = Regex.Replace(text, @"[$€£¥₹₽₩₺₫]\d+(?:\.\d+)?", FlipMoneyMatch);
        for (int i = 0; i < 5; i++) {
            text = Regex.Replace(text, @"(\d)\.(\d)", m => m.Value.Replace(".", " point "));
            text = Regex.Replace(text, @"\bwww\.[a-zA-Z0-9]+\b|\b[a-zA-Z0-9]+\.(com|net|org|io|edu|gov|mil|info|biz|co|us|uk|ca|de|fr|jp|au|cn|ru|gr)\b", m => m.Value.Replace(".", " dot "));
        }
        text = Regex.Replace(text, @"\bD[Rr]\.(?= [A-Z])", "Doctor");
        text = Regex.Replace(text, @"\b(Mr|MR)\.(?= [A-Z])", "Mister");
        text = Regex.Replace(text, @"\b(Ms|MS)\.(?= [A-Z])", "Miss");
        text = Regex.Replace(text, @"\x20{2,}", " ");

        text = Regex.Replace(text, @"(?<!\:)\b([1-9]|1[0-2]):([0-5]\d)\b(?!\:)", m => $"{m.Groups[1].Value} {m.Groups[2].Value}");
        foreach (var punc in punctuation) { text = text.Replace(punc.ToString(), $"{punc} "); }

        while (text.Length > 0 && punctuation.Contains(text[0])) { text = text[1..]; }
        return text.Trim();


        // Helper methods
        static string FlipMoneyMatch(Match m) {
            var value = m.Value[1..].Replace(",", ".");
            return $"{value} {currencies[m.Value[0]]}{(value == "1" ? "" : "s")}";
        }
    }

    /// <summary> Post-processes the phonemes to Kokoro's specs, preparing them for tokenization. </summary>
    /// <remarks> We also use the initial text to restore the punctuation that was discarded by Espeak. </remarks>
    static string PostProcessPhonemes(string initialText, string[] phonemesArray, string lang = "en-us") {
        // Initial scan for punctuation and spacing, so they can later be restored.
        var puncs = new List<string>();
        for (int i = 0; i < initialText.Length; i++) {
            char c = initialText[i];
            if (punctuation.Contains(c)) {
                var punc = c.ToString();
                while (i < initialText.Length - 1 && (punctuation.Contains(initialText[++i]) || initialText[i] == ' ')) { punc += initialText[i]; }
                puncs.Add(punc);
            }
        }

        // Restoration of punctuation and spacing.
        var sb = new StringBuilder();
        for (int i = 0; i < phonemesArray.Length; i++) {
            // First, finalize the parenthesis retrieval hack -- remove any duplicate columns that may have sneaked in.
            while (phonemesArray[i].StartsWith("kˈoʊlən ")) { phonemesArray[i] = phonemesArray[i]["kˈoʊlən ".Length..]; }
            while (phonemesArray[i].StartsWith(" kˈoʊlən")) { phonemesArray[i] = phonemesArray[i][" kˈoʊlən".Length..]; }
            sb.Append(phonemesArray[i]);
            if (puncs.Count > i) { sb.Append(puncs[i]); }
        }
        var phonemes = sb.ToString().Trim();

        // Refinement of various phonemes and condensing of symbols.
        for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("  ", " "); }
        foreach (var f in punctuation) { phonemes = phonemes.Replace($" {f}", f.ToString()); }
        for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("!!", "!").Replace("!?!", "!?"); }
        phonemes = phonemes.Replace("ˈɛ", "ˌɛ");

        return new string(phonemes.Where(Vocab.ContainsKey).ToArray());
    }
}
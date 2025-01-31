﻿using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace KokoroSharp;

/// <summary> A static module responsible for tokenization converting plaintext to phonemes, and phonemes to tokens. </summary>
/// <remarks>
/// <para> Internally preprocesses and post-processes the input text to bring it closer to what the model expects to see. </para>
/// <para> Phonemization happens via the espeak-ng library: <b>https://github.com/espeak-ng/espeak-ng/blob/master/docs/guide.md</b> </para>
/// </remarks>
public static class Tokenizer {
    static HashSet<char> punctuation = [.. ":,.!?"]; // Lines split on any of these occurences, by design via espeak-ng.
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


    /// <summary> Converts the input text to phoneme tokens, directly usable by Kokoro. </summary>
    public static int[] Tokenize(string inputText, bool preprocess = true) => Phonemize(inputText, preprocess).Select(x => Vocab[x]).ToArray();

    /// <summary> Converts the input text into the corresponding phonemes, with slight preprocessing and post-processing to preserve punctuation and other TTS essentials. </summary>
    static string Phonemize(string inputText, bool preprocess = true) {
        var preprocessedText = preprocess ? PreprocessText(inputText) : inputText;
        var phonemeList = Phonemize_Internal(preprocessedText).Split('\n');
        return PostProcessPhonemes(preprocessedText, phonemeList);
    }

    /// <summary> Invokes the espeak-ng via command line, to convert given text into phonemes. </summary>
    /// <remarks> Espeak will return a line ending when it meets any of the <see cref="PunctuationTokens"/> and gets rid of any punctuation, so these will have to be converted back to a single-line, with the punctuation restored. </remarks>
    static string Phonemize_Internal(string text) {
        var targetWorkingDir = File.Exists("espeak/espeak-ng.exe") && OperatingSystem.IsWindows() ? "espeak" : null;
        using var process = new Process() {
            StartInfo = new ProcessStartInfo() {
                FileName = "espeak-ng",
                WorkingDirectory = targetWorkingDir,
                Arguments = $"--ipa=3 -q -v en-us \"{text}\"",
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8
            }
        };
        process.Start();
        var phonemeList = process.StandardOutput.ReadToEnd();
        process.StandardOutput.Close();

        return phonemeList.Replace("\r\n", "\n").Trim();
    }

    /// <summary> Normalizes the input text to what the Kokoro model would expect to see, preparing it for phonemization. </summary>
    static string PreprocessText(string text) {
        text = text.Normalize().Replace("“", "").Replace("”", "").Replace("«", "").Replace("»", "").Replace("\"", "").Replace("**", "*");
        foreach (var punc in punctuation) { text = text.Replace(punc.ToString(), $"{punc} "); }

        text = Regex.Replace(text, @"\bD[Rr]\.(?= [A-Z])", "Doctor");
        text = Regex.Replace(text, @"\b(Mr|MR)\.(?= [A-Z])", "Mister");
        text = Regex.Replace(text, @"\b(Ms|MS)\.(?= [A-Z])", "Miss");
        text = Regex.Replace(text, @"\x20{2,}", " ");

        text = Regex.Replace(text, @"(?<!\:)\b([1-9]|1[0-2]):([0-5]\d)\b(?!\:)", m => $"{m.Groups[1].Value} {m.Groups[2].Value}");
        text = Regex.Replace(text, @"[$€£¥₹₽₩₺₫]\d+(?:\.\d+)?", FlipMoneyMatch);
        text = Regex.Replace(text, @"\d+\.\d+", PointNumMatch);
        
        while (punctuation.Contains(text[0])) { text = text[1..]; }
        return text.Trim();



        static string FlipMoneyMatch(Match m) {
            var value = m.Value[1..].Replace(",", ".");
            var currency = currencies[m.Value[0]];
            return value.Contains('.') ? $"{value.Replace(".", " ")} {currency}s"
                 : value.EndsWith('1') ? $"{value} {currency}" : $"{value} {currency}s";
        }

        static string PointNumMatch(Match m) {
            var parts = m.Value.Split('.');
            return $"{parts[0]} point {string.Join(" ", parts[1].ToCharArray())}";
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
            sb.Append(phonemesArray[i]);
            if (puncs.Count > i) { sb.Append(puncs[i]); }
        }
        var phonemes = sb.ToString().Trim();

        // Refinement of various phonemes and condensing of symbols.
        phonemes = phonemes.Replace("ʲ", "j").Replace("r", "ɹ").Replace("x", "k").Replace("ɬ", "l");
        if (lang == "en-us") { phonemes = Regex.Replace(phonemes, @"(?<=nˈaɪn)ti(?!ː)", "di"); }
        for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("  ", " "); }
        foreach (var f in punctuation) { phonemes = phonemes.Replace($" {f}", f.ToString()); }
        for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("!!", "!").Replace("!?!", "!?"); }

        return new string(phonemes.Where(Vocab.ContainsKey).ToArray());
    }
}
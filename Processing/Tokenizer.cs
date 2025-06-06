namespace KokoroSharp.Processing;

using KokoroSharp.Utilities;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

/// <summary> A static module responsible for tokenization converting plaintext to phonemes, and phonemes to tokens. </summary>
/// <remarks>
/// <para> Internally preprocesses and post-processes the input text to bring it closer to what the model expects to see. </para>
/// <para> Phonemization happens via the espeak-ng library: <b>https://github.com/espeak-ng/espeak-ng/blob/master/docs/guide.md</b> </para>
/// </remarks>
public static partial class Tokenizer {
    static HashSet<char> spaceNeedingPhonemes = [.. "\"…<«“"];
    static HashSet<char> replaceablePhonemes = [.. "\n;:,.!?¡¿—…\"«»“”()"];
    internal static HashSet<char> punctuation = [.. ";:,.!?…¿\n"];   // Lines split on any of these occurrences, by design via espeak-ng.
    static Dictionary<char, string> currencies = new() { { '$', "dollar" }, { '€', "euro" }, { '£', "pound" }, { '¥', "yen" }, { '₹', "rupee" }, { '₽', "ruble" }, { '₩', "won" }, { '₺', "lira" }, { '₫', "dong" } };
    static char[] deletableCharacters = [.. "-`()[]{}"];
    //static int[] z ; // tokens that might be of interest later.

    /// <summary> Path to the folder in which the espeak-ng binaries and data reside. Defaults to the folder created by the NuGet package. </summary>
    /// <remarks> Can be overridden with a custom path if a use-case requires so. </remarks>
    public static string eSpeakNGPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "espeak");

    public static IReadOnlyDictionary<char, int> Vocab { get; }
    public static IReadOnlyDictionary<int, char> TokenToChar { get; }
    public static HashSet<int> PunctuationTokens { get; }
    static Tokenizer() {
        Dictionary<char, int> _vocabNew = new() { ['\n'] = -1, ['$'] = 0, [';'] = 1, [':'] = 2, [','] = 3, ['.'] = 4, ['!'] = 5, ['?'] = 6, ['¡'] = 7, ['¿'] = 8, ['—'] = 9, ['…'] = 10, ['\"'] = 11, ['('] = 12, [')'] = 13, ['“'] = 14, ['”'] = 15, [' '] = 16, ['\u0303'] = 17, ['ʣ'] = 18, ['ʥ'] = 19, ['ʦ'] = 20, ['ʨ'] = 21, ['ᵝ'] = 22, ['\uAB67'] = 23, ['A'] = 24, ['I'] = 25, ['O'] = 31, ['Q'] = 33, ['S'] = 35, ['T'] = 36, ['W'] = 39, ['Y'] = 41, ['ᵊ'] = 42, ['a'] = 43, ['b'] = 44, ['c'] = 45, ['d'] = 46, ['e'] = 47, ['f'] = 48, ['h'] = 50, ['i'] = 51, ['j'] = 52, ['k'] = 53, ['l'] = 54, ['m'] = 55, ['n'] = 56, ['o'] = 57, ['p'] = 58, ['q'] = 59, ['r'] = 60, ['s'] = 61, ['t'] = 62, ['u'] = 63, ['v'] = 64, ['w'] = 65, ['x'] = 66, ['y'] = 67, ['z'] = 68, ['ɑ'] = 69, ['ɐ'] = 70, ['ɒ'] = 71, ['æ'] = 72, ['β'] = 75, ['ɔ'] = 76, ['ɕ'] = 77, ['ç'] = 78, ['ɖ'] = 80, ['ð'] = 81, ['ʤ'] = 82, ['ə'] = 83, ['ɚ'] = 85, ['ɛ'] = 86, ['ɜ'] = 87, ['ɟ'] =  90, ['ɡ'] = 92, ['ɥ'] = 99, ['ɨ'] = 101, ['ɪ'] = 102, ['ʝ'] = 103, ['ɯ'] = 110, ['ɰ'] = 111, ['ŋ'] = 112, ['ɳ'] = 113, ['ɲ'] = 114, ['ɴ'] = 115, ['ø'] = 116, ['ɸ'] = 118, ['θ'] = 119, ['œ'] = 120, ['ɹ'] = 123, ['ɾ'] = 125, ['ɻ'] = 126, ['ʁ'] = 128, ['ɽ'] = 129, ['ʂ'] = 130, ['ʃ'] = 131, ['ʈ'] = 132, ['ʧ'] = 133, ['ʊ'] = 135, ['ʋ'] = 136, ['ʌ'] = 138, ['ɣ'] = 139, ['ɤ'] = 140, ['χ'] = 142, ['ʎ'] = 143, ['ʒ'] = 147, ['ʔ'] = 148, ['ˈ'] = 156, ['ˌ'] = 157, ['ː'] = 158, ['ʰ'] = 162, ['ʲ'] = 164, ['↓'] = 169, ['→'] = 171, ['↗'] = 172, ['↘'] = 173, ['ᵻ'] = 177 };

        var (c2t, t2c) = (new Dictionary<char, int>(), new Dictionary<int, char>());
        foreach (var (key, val) in _vocabNew) { (c2t[key], t2c[val]) = (val, key); }
        (Vocab, TokenToChar) = (c2t, t2c);
        //z = "ʼ↓↑→↗↘".Select(x => Vocab[x]).ToArray();
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
    public static string Phonemize(string inputText, string langCode, bool preprocess = true) {
        var preprocessedText = preprocess ? PreprocessText(inputText, langCode) : inputText;
        var phonemeList = Phonemize_Internal(CollectSymbols(preprocessedText), out _, langCode).Split('\n');
        return PostProcessPhonemes(preprocessedText, phonemeList, langCode);
    }

    /// <summary> Invokes the platform-appropriate espeak-ng executable via command line, to convert given text into phonemes. </summary>
    /// <remarks> eSpeak NG will return a line ending when it meets any of the <see cref="PunctuationTokens"/> and gets rid of any punctuation, so these will have to be converted back to a single-line, with the punctuation restored. </remarks>
    public static string Phonemize_Internal(string text, out string originalSegments, string langCode = "en-us") {
        using var process = new Process() {
            StartInfo = new ProcessStartInfo() {
                FileName = CrossPlatformHelper.GetEspeakBinariesPath(),
                WorkingDirectory = null,
                Arguments = $"--ipa=3 -q -v {langCode} \"{text}\"",
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8
            }
        };
        process.StartInfo.EnvironmentVariables.Add("ESPEAK_DATA_PATH", @$"{eSpeakNGPath}/espeak-ng-data");
        process.Start();
        originalSegments = process.StandardOutput.ReadToEnd();
        Debug.WriteLine($"org:\n{originalSegments}---");
        process.StandardOutput.Close();

        return originalSegments.Replace("\r\n", "\n").Trim();
    }

    /// <summary> Normalizes the input text to what the Kokoro model would expect to see, preparing it for phonemization. </summary>
    /// <remarks> In addition, converts various "written" text to "spoken" form (e.g. $1 --> "one dollar" instead of "dollar one". </remarks>
    internal static string PreprocessText(string text, string langCode) {
        text = HeaderLink().Replace(text, "$1"); // Discard links appearing in `[Header](link)` format.
        text = HeaderImgLink().Replace(text, "$1$2"); // And in [Header[(img](link)]
        for (int i = 0; i < 5; i++) {
            text = DecimalPoint().Replace(text, "$1 point $2");
            text = WebUrl().Replace(text, m => m.Value.Replace(".", " dot "));
        }
        text = text.Replace("\r\n", "\n");
        text = CodeBlock().Replace(text, m => {
            var lines = m.Groups[1].Value.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                int com = Math.Max(lines[i].IndexOf("//"), lines[i].IndexOf("#"));
                lines[i] = (com >= 0 ? lines[i][..com] : lines[i]).Replace(".", " dot ") + (com >= 0 ? lines[i][com..] : "");
            }
            return string.Join("\n", lines);
        });
        text = CodeBlock().Replace(text, m => m.Groups[1].Value.Replace("  dot ", ".").Replace("dot \n", ".\n"));
        text = TickQuote().Replace(text, m => m.Groups[1].Value.Replace(".", " dot "));
        text = text.Replace("C#", "C SHARP").Replace(".NET", "dot net").Replace("->", " to ");
        text = ByteNumber().Replace(text, m => {
            string u = m.Groups[2].Value switch {
                "KB" => " kilobyte",
                "MB" => " megabyte",
                "GB" => " gigabyte",
                "TB" => " terabyte",
                _ => m.Groups[2].Value
            };
            return $"{m.Groups[1].Value}{u}{m.Groups[3].Value}";
        });
        text = text.Replace("/", " slash ")
            .Replace("\n######", "\n Subnote: ")
            .Replace("\n#####", "\n Minor note: ")
            .Replace("\n####", "\n Note: ")
            .Replace("\n###", "\n Minor Header: ")
            .Replace("\n##", "\n Subheader: ")
            .Replace("\n#", "\n Header: ");
        text = text.Replace(".com", "dot com").Replace("https://", "https ");
        text = text.Replace("\r\n", "\n").Replace("**", "*").Replace("‘", "\"").Replace("’", "\"");
        text = Money().Replace(text, FlipMoneyMatch);
        text = Doctor().Replace(text, "Doctor");
        text = Mister().Replace(text, "Mister");
        text = Miss().Replace(text, "Miss");
        text = WhiteSpace().Replace(text," ");
        text = Time().Replace(text, "$1 $2");
        text = text.Replace("{", ",").Replace("}", ",").Replace("(", ",").Replace(")", ",");
        foreach (var c in deletableCharacters) { text = text.Replace(c.ToString(), " "); }
        foreach (var punc in punctuation) {
            while (text.Contains($" {punc}")) { text = text.Replace($" {punc}", $"{punc}"); }
            text = text.Replace($"{punc}", $"{punc} ");
        }
        while (text.Length > 0 && replaceablePhonemes.Contains(text[0]) || deletableCharacters.Any(text.StartsWith)) { text = text[1..]; }
        while (text.Contains("\n\n")) { text = text.Replace("\n\n", "\n"); }
        for (int i = 0; i < 10; i++) { text = text.Replace("  ", " "); }

        return text.Trim();


        // Helper methods
        static string FlipMoneyMatch(Match m) {
            var value = m.Value[1..].Replace(",", ".");
            return $"{value} {currencies[m.Value[0]]}{(value == "1" ? "" : "s")}";
        }
    }

    static string CollectSymbols(string text) {
        text = text.Replace("\n", "\n ");
        foreach (var c in replaceablePhonemes) { text = text.Replace(c, ','); }
        for (int i = 0; i < 10; i++) { text = text.Replace(" ,", ", "); }
        //Debug.WriteLine(text);
        return text;
    }

    /// <summary> Post-processes the phonemes to Kokoro's specs, preparing them for tokenization. </summary>
    /// <remarks> We also use the initial text to restore the punctuation that was discarded by Espeak. </remarks>
    static string PostProcessPhonemes(string initialText, string[] phonemesArray, string lang = "en-us") {
        // Initial scan for punctuation and spacing, so they can later be restored.
        var puncs = new List<string>();
        for (int i = 0; i < initialText.Length; i++) {
            char c = initialText[i];
            if (replaceablePhonemes.Contains(c)) {
                var punc = c.ToString();
                while (i < initialText.Length - 1 && (replaceablePhonemes.Contains(initialText[++i]) || initialText[i] == ' ')) { punc += initialText[i]; }
                puncs.Add(punc);
            }
        }

        // Restoration of punctuation and spacing.
        var sb = new StringBuilder();
        for (int i = 0; i < phonemesArray.Length; i++) {
            var vf = phonemesArray[i];
            if (vf.StartsWith("ˈɛ")) { vf = "ˌɛ" + vf[2..]; }
            sb.Append(vf);
            if (puncs.Count > i) { sb.Append(puncs[i]); }
        }
        var phonemes = sb.ToString().Trim();

        // Refinement of various phonemes and condensing of symbols.
        for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("  ", " "); }
        foreach (var f in punctuation) { phonemes = phonemes.Replace($" {f}", f.ToString()); }
        for (int i = 0; i < 5; i++) { phonemes = phonemes.Replace("!!", "!").Replace("!?!", "!?"); }

        for (int i = 1; i < phonemes.Length - 1; i++) {
            if (!spaceNeedingPhonemes.Contains(phonemes[i])) { continue; }
            if (phonemes[i - 1] != ' ') {
                var ph = phonemes[i];
                if (phonemes[i] == '"' && phonemes[i + 1] == ' ') { continue; }
                phonemes = phonemes.Insert(i, " ");
                i++;
            }
        }
        phonemes = phonemes.Replace("ː ", " ").Replace("ɔː", "ˌɔ").Replace("\n ", "\n");
        return new string(phonemes.Where(Vocab.ContainsKey).ToArray());
    }

    #region Regexes

    [GeneratedRegex(@"\b(https?://)?(www\.)?[a-zA-Z0-9]+\b|\b[a-zA-Z0-9]+\.(com|net|org|io|edu|gov|mil|info|biz|co|us|uk|ca|de|fr|jp|au|cn|ru|gr)\b")]
                                                                     private static partial Regex WebUrl();
    [GeneratedRegex(@"^```[A-Za-z]{0,10}\n([\s\S]*?)\n```(?:\n|$)", RegexOptions.Multiline)]
                                                                     private static partial Regex CodeBlock();
    [GeneratedRegex(@"\[(.*?)\]\(.*?\)")]                            private static partial Regex HeaderLink();
    [GeneratedRegex(@"\[.*?\[(.*?)\].*?\]\(.*?\)|\[(.*?)\]\(.*?\)")] private static partial Regex HeaderImgLink();
    [GeneratedRegex(@"(\d)(\.)(\d)")]                                private static partial Regex DecimalPoint();
    [GeneratedRegex(@"(?<!`)`([^`]+)`(?!`)")]                        private static partial Regex TickQuote();
    [GeneratedRegex(@"\b(\d+(?:\.\d+)?)(KB|MB|GB|TB)(\s)")]          private static partial Regex ByteNumber();
    [GeneratedRegex(@"[$€£¥₹₽₩₺₫]\d+(?:\.\d+)?")]                    private static partial Regex Money();
    [GeneratedRegex(@"\bD[Rr]\.(?= [A-Z])")]                         private static partial Regex Doctor();
    [GeneratedRegex(@"\b(Mr|MR)\.(?= [A-Z])")]                       private static partial Regex Mister();
    [GeneratedRegex(@"\b(Ms|MS)\.(?= [A-Z])")]                       private static partial Regex Miss();
    [GeneratedRegex(@"\x20{2,}")]                                    private static partial Regex WhiteSpace();
    [GeneratedRegex(@"(?<!\:)\b([1-9]|1[0-2]):([0-5]\d)\b(?!\:)")]   private static partial Regex Time();

    #endregion Regexes
}
namespace KokoroSharp.Processing;

using KokoroSharp.Utilities;

using MisakiSharp;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

/// <summary> A static module responsible for tokenization converting plaintext to phonemes, and phonemes to tokens. </summary>
/// <remarks>
/// <para> Internally preprocesses and post-processes the input text to bring it closer to what the model expects to see. </para>
/// <para> Phonemization happens natively in C# via <b>https://github.com/Lyrcaxis/MisakiSharp</b> for all nine Kokoro languages. </para>
/// </remarks>
public static partial class Tokenizer {
    static HashSet<char> replaceablePhonemes = [.. "\n;:,.!?¡¿—…\"«»“”()"];
    internal static HashSet<char> punctuation = [.. ";:,.!?…¿\n"];
    static Dictionary<char, string> currencies = new() { { '$', "dollar" }, { '€', "euro" }, { '£', "pound" }, { '¥', "yen" }, { '₹', "rupee" }, { '₽', "ruble" }, { '₩', "won" }, { '₺', "lira" }, { '₫', "dong" } };
    static char[] deletableCharacters = [.. "-`()[]{}~"];
    //static int[] z ; // tokens that might be of interest later.

    public static IReadOnlyDictionary<char, int> Vocab { get; }
    public static IReadOnlyDictionary<int, char> TokenToChar { get; }
    public static HashSet<int> PunctuationTokens { get; }

    static Tokenizer() {
        Dictionary<char, int> _vocabNew = new() { ['\n'] = -1, ['$'] = 0, [';'] = 1, [':'] = 2, [','] = 3, ['.'] = 4, ['!'] = 5, ['?'] = 6, ['¡'] = 7, ['¿'] = 8, ['—'] = 9, ['…'] = 10, ['\"'] = 11, ['('] = 12, [')'] = 13, ['“'] = 14, ['”'] = 15, [' '] = 16, ['\u0303'] = 17, ['ʣ'] = 18, ['ʥ'] = 19, ['ʦ'] = 20, ['ʨ'] = 21, ['ᵝ'] = 22, ['\uAB67'] = 23, ['A'] = 24, ['I'] = 25, ['O'] = 31, ['Q'] = 33, ['S'] = 35, ['T'] = 36, ['W'] = 39, ['Y'] = 41, ['ᵊ'] = 42, ['a'] = 43, ['b'] = 44, ['c'] = 45, ['d'] = 46, ['e'] = 47, ['f'] = 48, ['h'] = 50, ['i'] = 51, ['j'] = 52, ['k'] = 53, ['l'] = 54, ['m'] = 55, ['n'] = 56, ['o'] = 57, ['p'] = 58, ['q'] = 59, ['r'] = 60, ['s'] = 61, ['t'] = 62, ['u'] = 63, ['v'] = 64, ['w'] = 65, ['x'] = 66, ['y'] = 67, ['z'] = 68, ['ɑ'] = 69, ['ɐ'] = 70, ['ɒ'] = 71, ['æ'] = 72, ['β'] = 75, ['ɔ'] = 76, ['ɕ'] = 77, ['ç'] = 78, ['ɖ'] = 80, ['ð'] = 81, ['ʤ'] = 82, ['ə'] = 83, ['ɚ'] = 85, ['ɛ'] = 86, ['ɜ'] = 87, ['ɟ'] =  90, ['ɡ'] = 92, ['ɥ'] = 99, ['ɨ'] = 101, ['ɪ'] = 102, ['ʝ'] = 103, ['ɯ'] = 110, ['ɰ'] = 111, ['ŋ'] = 112, ['ɳ'] = 113, ['ɲ'] = 114, ['ɴ'] = 115, ['ø'] = 116, ['ɸ'] = 118, ['θ'] = 119, ['œ'] = 120, ['ɹ'] = 123, ['ɾ'] = 125, ['ɻ'] = 126, ['ʁ'] = 128, ['ɽ'] = 129, ['ʂ'] = 130, ['ʃ'] = 131, ['ʈ'] = 132, ['ʧ'] = 133, ['ʊ'] = 135, ['ʋ'] = 136, ['ʌ'] = 138, ['ɣ'] = 139, ['ɤ'] = 140, ['χ'] = 142, ['ʎ'] = 143, ['ʒ'] = 147, ['ʔ'] = 148, ['ˈ'] = 156, ['ˌ'] = 157, ['ː'] = 158, ['ʰ'] = 162, ['ʲ'] = 164, ['↓'] = 169, ['→'] = 171, ['↗'] = 172, ['↘'] = 173, ['ᵻ'] = 177 };

        Dictionary<char, int> _vocabZh = new() { ['/'] = 7, ['ㄓ'] = 23, ['ㄅ'] = 30, ['ㄆ'] = 32, ['R'] = 34, ['ㄇ'] = 37, ['ㄈ'] = 38, ['ㄉ'] = 40, ['ㄊ'] = 49, ['ㄋ'] = 73, ['ㄌ'] = 74, ['ㄍ'] = 79, ['ㄎ'] = 84, ['ㄦ'] = 85, ['ㄏ'] = 88, ['ㄐ'] = 89, ['ㄑ'] = 91, ['ㄒ'] = 93, ['ㄔ'] = 94, ['ㄕ'] = 95, ['ㄗ'] = 96, ['ㄘ'] = 97, ['ㄙ'] = 98, ['月'] = 99, ['ㄚ'] = 100, ['ㄛ'] = 104, ['ㄝ'] = 105, ['ㄞ'] = 106, ['ㄟ'] = 107, ['ㄠ'] = 108, ['ㄡ'] = 109, ['ㄢ'] = 117, ['ㄣ'] = 121, ['ㄤ'] = 122, ['ㄥ'] = 124, ['ㄖ'] = 126, ['ㄧ'] = 127, ['ㄨ'] = 134, ['ㄩ'] = 137, ['ㄜ'] = 140, ['ㄭ'] = 141, ['十'] = 144, ['压'] = 145, ['言'] = 146, ['阳'] = 149, ['要'] = 150, ['阴'] = 151, ['应'] = 152, ['用'] = 153, ['又'] = 154, ['中'] = 155, ['穵'] = 159, ['外'] = 160, ['万'] = 161, ['王'] = 163, ['为'] = 165, ['文'] = 166, ['瓮'] = 167, ['我'] = 168, ['3'] = 169, ['5'] = 170, ['1'] = 171, ['2'] = 172, ['4'] = 173, ['元'] = 175, ['云'] = 176 };

        var (c2t, t2c) = (new Dictionary<char, int>(), new Dictionary<int, char>());
        foreach (var (key, val) in _vocabNew) { (c2t[key], t2c[val]) = (val, key); }
        foreach (var (key, val) in _vocabZh) { c2t[key] = val; t2c.TryAdd(val, key); }
        (Vocab, TokenToChar) = (c2t, t2c);
        ChineseG2P.EnglishPhonemizer = text => Phonemize(text, "en-us"); // English fallback rather than becoming unk.
        //z = "ʼ↓↑→↗↘".Select(x => Vocab[x]).ToArray();
        PunctuationTokens = punctuation.Select(x => Vocab[x]).ToHashSet();
    }

    /// <summary> Tokenizes pre-phonemized input "as-is", mapping to a token array directly usable by Kokoro. </summary>
    /// <remarks> Useful for developers who bring their own phonemization solution. </remarks>
    public static int[] TokenizePhonemes(char[] phonemes) => phonemes.Select(x => Vocab[x]).ToArray();

    /// <summary> Converts the input text to phoneme tokens, directly usable by Kokoro. Phonemization is native C#, so it works on any .NET platform. </summary>
    public static int[] Tokenize(string inputText, string langCode = "en-us", bool preprocess = true) => Phonemize(inputText, langCode, preprocess).Select(x => Vocab[x]).ToArray();


    static readonly List<(string word, string phonemes)> customEnglishWords = [("kokoro", "kOkˈOɹO"), ("KokoroSharp", "kOkˈOɹO ʃˈɑɹp")]; // kokoro-specific.
    static readonly Lazy<EnglishG2P> americanG2P = new(() => CreateEnglishG2P(british: false));
    static readonly Lazy<EnglishG2P> britishG2P = new(() => CreateEnglishG2P(british: true));
    static readonly Dictionary<string, EspeakG2P> espeakG2Ps = [];
    static EnglishG2P CreateEnglishG2P(bool british) { var g2p = new EnglishG2P(EnglishG2P.DefaultTagger, british: british); g2p.AddWords(customEnglishWords); return g2p; }

    /// <summary> Teaches the English phonemizers custom pronunciations (e.g. brand names), applied to both dialects, winning over the lexicon and fallback. </summary>
    /// <remarks> Capitalization variants are covered automatically ("kokoro" also covers "Kokoro"). Phonemes use misaki's IPA, e.g. "kOkˈOɹO". </remarks>
    public static void AddWords(List<(string word, string phonemes)> dict) {
        customEnglishWords.AddRange(dict);
        foreach (var g2p in new[] { americanG2P, britishG2P }) { if (g2p.IsValueCreated) { g2p.Value.AddWords(dict); } }
    }


    /// <summary> Converts the input text into the corresponding phonemes, with slight preprocessing and post-processing to preserve punctuation and other TTS essentials. </summary>
    /// <remarks> Phonemization happens per line, so line breaks survive into the phonemes for <see cref="SegmentationSystem"/> to pause on. </remarks>
    public static string Phonemize(string inputText, string langCode = "en-us", bool preprocess = true) {
        if (preprocess) { inputText = string.Join(' ', PhonemeLiteral().Split(inputText).Select(part => PhonemeLiteral2().IsMatch(part) ? part : PreprocessText(part, langCode)).Where(part => part.Length > 0)); }
        return string.Join('\n', inputText.Split('\n').Select(PhonemizeLine));

        string PhonemizeLine(string line) {
            if (string.IsNullOrWhiteSpace(line)) { return ""; }
            if (langCode == "cmn") { return new string(ChineseG2P.Phonemize(line).Where(Vocab.ContainsKey).ToArray()); }
            if (langCode == "ja") { return new string(JapaneseG2P.Phonemize(line).Where(Vocab.ContainsKey).ToArray()); }
            if (langCode is "en-us" or "en-gb") {
                var g2p = langCode == "en-gb" ? britishG2P.Value : americanG2P.Value;
                return new string(g2p.Phonemize(line).Phonemes.Where(Vocab.ContainsKey).ToArray());
            }
            if (langCode is "es" or "fr" or "hi" or "it" or "pt-br") { // Kokoro's voices for these were trained on misaki's espeak pipeline, not raw espeak IPA.
                // Phonemized entirely from MisakiSharp's measured espeak dump, with a letter-to-sound model for unknown words -- espeak is never spawned.
                if (!espeakG2Ps.TryGetValue(langCode, out var g2p)) { espeakG2Ps[langCode] = g2p = new EspeakG2P(EspeakReplay.Provider(langCode)); }
                var espeakParts = PhonemeLiteral().Split(line).Select(part => PhonemeLiteral2().Match(part) is { Success: true } m ? m.Groups[1].Value : g2p.Phonemize(part));
                return new string(string.Join(' ', espeakParts.Where(part => part.Length > 0)).Where(Vocab.ContainsKey).ToArray());
            }
            Debug.WriteLine($"'{langCode}' is not one of Kokoro's nine languages, so there's nothing to phonemize with. Returning empty phonemes.");
            return "";
        }
    }

    /// <summary> Normalizes the input text to what the Kokoro model would expect to see, preparing it for phonemization. </summary>
    /// <remarks> In addition, converts various "written" text to "spoken" form (e.g. $1 --> "one dollar" instead of "dollar one". </remarks>
    internal static string PreprocessText(string text, string langCode = "en-us") {
        text = HeaderLink().Replace(text, "$1"); // Discard links appearing in `[Header](link)` format.
        text = HeaderImgLink().Replace(text, "$1$2"); // And in [Header[(img](link)]
        text = Money().Replace(text, "$2 $1 $3"); // Convert money amounts like "$1.50" to "1 $ 50".
        text = Money2().Replace(text, "$1 $3 $2"); // Convert money amounts like "1.50€" to "1 € 50".
        for (int i = 0; i < 5; i++) {
            text = DecimalPoint().Replace(text, m => $"{m.Groups[1]} point {string.Join<char>(' ', m.Groups[3].Value)}"); // Convert decimal points like "3.1415" to "3 point 1 4 1 5".
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
        text = Approximately().Replace(text, "about "); // Convert "~5" to "about 5".
        text = GroupingComma().Replace(text, ""); // Convert "15,000" to "15000"
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
        text = "\n" + text; // Lets headers at the very start of the text convert too.
        text = text.Replace("/", " slash ")
            .Replace("\n###### ", "\n Subnote: ")
            .Replace("\n##### ", "\n Minor note: ")
            .Replace("\n#### ", "\n Note: ")
            .Replace("\n### ", "\n Minor Header: ")
            .Replace("\n## ", "\n Subheader: ")
            .Replace("\n# ", "\n Header: ");
        text = text.Replace(".com", "dot com").Replace("https://", "https ");
        text = text.Replace("\r\n", "\n").Replace("**", "*").Replace("‘", "\"").Replace("’", "\"").Replace('।', '.').Replace('॥', '.');
        foreach (var c in currencies.Keys) { text = text.Replace(c.ToString(), $" {currencies[c]} "); } // Convert currency symbols to words (e.g., $ -> "dollar").
        text = Doctor().Replace(text, "Doctor");
        text = Mister().Replace(text, "Mister");
        text = Miss().Replace(text, "Miss");
        text = WhiteSpace().Replace(text," ");
        text = Time().Replace(text, "$1 $2");
        text = text.Replace("{", ",").Replace("}", ",").Replace("(", ",").Replace(")", ",");
        var deletable = langCode is "en-us" or "en-gb" ? deletableCharacters.Where(c => c != '-') : deletableCharacters; // Misaki reads English "voice-loading" compounds glued, like espeak did.
        foreach (var c in deletable) { text = text.Replace(c.ToString(), " "); }
        foreach (var punc in punctuation) {
            while (text.Contains($" {punc}")) { text = text.Replace($" {punc}", $"{punc}"); }
            text = text.Replace($"{punc}", $"{punc} ");
        }
        while (text.Length > 0 && replaceablePhonemes.Contains(text[0]) || deletableCharacters.Any(text.StartsWith)) { text = text[1..]; }
        while (text.Contains("\n\n")) { text = text.Replace("\n\n", "\n"); }
        for (int i = 0; i < 10; i++) { text = text.Replace("  ", " "); }

        return text.Trim();
    }

    #region Regexes

    [GeneratedRegex(@"\b(https?://)?(www\.)?[a-zA-Z0-9]+\b|\b[a-zA-Z0-9]+\.(com|net|org|io|edu|gov|mil|info|biz|co|us|uk|ca|de|fr|jp|au|cn|ru|gr)\b")]
                                                                     private static partial Regex WebUrl();
    [GeneratedRegex(@"^```[A-Za-z]{0,10}\n([\s\S]*?)\n```(?:\n|$)", RegexOptions.Multiline)]
                                                                     private static partial Regex CodeBlock();       // Markdown code blocks: ```csharp\ncode\n```
    [GeneratedRegex(@"\[(.*?)\]\(.*?\)")]                            private static partial Regex HeaderLink();      // Markdown links: [Header](link)
    [GeneratedRegex(@"\[.*?\[(.*?)\].*?\]\(.*?\)|\[(.*?)\]\(.*?\)")] private static partial Regex HeaderImgLink();   // Markdown image links: [Header[(img](link)]
    [GeneratedRegex(@"(\d)(\.)(\d+)")]                               private static partial Regex DecimalPoint();    // Decimal point: 3.1415
    [GeneratedRegex(@"(?<!`)`([^`]+)`(?!`)")]                        private static partial Regex TickQuote();       // Inline code: `code`
    [GeneratedRegex(@"\b(\d+(?:\.\d+)?)(KB|MB|GB|TB)(\s)")]          private static partial Regex ByteNumber();      // Byte numbers: 1KB, 2.5MB, etc.
    [GeneratedRegex(@"([$€£¥₹₽₩₺₫]) ?(\d+)(?:[\.,](\d+))?")]         private static partial Regex Money();           // Money amounts: $1, €2.50, etc.
    [GeneratedRegex(@"(\d+)(?:[\.,](\d+))? ?([$€£¥₹₽₩₺₫])")]         private static partial Regex Money2();          // Money amounts: 1€, 2,50€, etc.
    [GeneratedRegex(@"\bD[Rr]\.(?= [A-Z])")]                         private static partial Regex Doctor();          // Doctor: Dr. Smith
    [GeneratedRegex(@"\b(Mr|MR)\.(?= [A-Z])")]                       private static partial Regex Mister();          // Mister: Mr. Smith
    [GeneratedRegex(@"\b(Ms|MS)\.(?= [A-Z])")]                       private static partial Regex Miss();            // Miss: Ms. Smith
    [GeneratedRegex(@"\x20{2,}")]                                    private static partial Regex WhiteSpace();      // Multiple spaces: "  "
    [GeneratedRegex(@"(?<!\:)\b([1-9]|1[0-2]):([0-5]\d)\b(?!\:)")]   private static partial Regex Time();            // Time: 12:30, 9:45, etc.
    [GeneratedRegex(@"~\s*(?=\d)")]                                  private static partial Regex Approximately();   // Approximate numbers: ~320
    [GeneratedRegex(@"(?<=\d),(?=\d\d\d\b)")]                        private static partial Regex GroupingComma();   // Digit grouping: 15,000
    [GeneratedRegex(@"(\[[^\]]+\]\(/[^/]+/\))")]                     private static partial Regex PhonemeLiteral();  // Literal Pronunciation: [Kokoro](/kˈOkəɹO/). Captures the entire string
    [GeneratedRegex(@"\[[^\]]+\]\(/([^/]+)/\)")]                     private static partial Regex PhonemeLiteral2(); // Literal Pronunciation: [Kokoro](/kˈOkəɹO/). Captures only the phoneme part e.g. kˈOkəɹO

    #endregion Regexes
}
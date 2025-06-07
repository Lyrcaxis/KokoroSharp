namespace KokoroSharp.Core;

using System.Diagnostics;

/// <summary> Contains all available languages as per Kokoro v1.0. </summary>
/// <remarks> Each enum entry internally maps to a language-specific *char*, so voices can be name-filtered base on their first letter. </remarks>
public enum KokoroLanguage {
    AmericanEnglish     = 'a',
    BritishEnglish      = 'b',
    Japanese            = 'j',
    MandarinChinese     = 'z',
    Spanish             = 'e',
    French              = 'f',
    Hindi               = 'h',
    Italian             = 'i',
    BrazilianPortuguese = 'p'
}

/// <summary> Used to name-filter voices based on their second letter, which represents the speaker's gender. </summary>
/// <remarks> For example a voice of an <b>AmericanEnglish Male</b> speaker will start with <b>`am_`</b> whereas a <b>Hindi Female</b>'s will start with <b>`hf_`</b>. </remarks>
public enum KokoroGender { Both, Male = 'm', Female = 'f' }

public static class KokoroLangCodeHelper {
    /// <summary> Maps the Kokoro Language (inferred from the voice's name) to the eSpeak NG langCode needed for proper phonemization. </summary>
    public static IReadOnlyDictionary<KokoroLanguage, string> KokoroLangToESpeakLangCodeMap { get; } = new Dictionary<KokoroLanguage, string>() {
        { KokoroLanguage.AmericanEnglish    , "en-us" },
        { KokoroLanguage.BritishEnglish     , "en-gb" },
        { KokoroLanguage.Japanese           , "ja" },
        { KokoroLanguage.MandarinChinese    , "cmn" },
        { KokoroLanguage.Spanish            , "es" },
        { KokoroLanguage.French             , "fr" },
        { KokoroLanguage.Hindi              , "hi" },
        { KokoroLanguage.Italian            , "it" },
        { KokoroLanguage.BrazilianPortuguese, "pt-br" }
    };

    /// <summary> Extracts the intended language of a voice, from its name's first letter. </summary>
    /// <remarks> e.g.: "AmericanEnglish" voices start with "a", and "Mandarin/Chinese" voices start with "z". </remarks>
    public static KokoroLanguage GetLanguage(this KokoroVoice voice) {
        if (string.IsNullOrWhiteSpace(voice.Name)) {
            Debug.WriteLine("Specified voice is not named. Mixed voices of multiple languages have to be named explicitly. Defaulting to en-us.");
            return KokoroLanguage.AmericanEnglish;
        }
        if (voice.Name.Length > 2 && voice.Name[2] != '_') {
            Debug.WriteLine("Specified voice is not named properly. Make sure to follow naming conveniences (see KokoroLanguage.cs). Defaulting to en-us.");
            return KokoroLanguage.AmericanEnglish;
        }
        if (!Enum.IsDefined(typeof(KokoroLanguage), (int) voice.Name[0])) {
            Debug.WriteLine("Specified voice is not named properly, or language is not recognized. Make sure to follow naming conveniences (see KokoroLanguage.cs). Defaulting to en-us.");
            return KokoroLanguage.AmericanEnglish;
        }

        return (KokoroLanguage) voice.Name[0];
    }

    /// <summary> Retrieves the LangCode for eSpeak-NG, so text can be phonemized properly. </summary>
    /// <remarks> Note that this is inferred by the NAME of the voice, so make sure to follow naming conveniences. </remarks>
    public static string GetLangCode(this KokoroVoice voice) => KokoroLangToESpeakLangCodeMap[voice.GetLanguage()];

    /// <summary> Retrieves the LangCode for eSpeak-NG, so text can be phonemized properly. </summary>
    public static string GetLangCode(this KokoroLanguage language) => KokoroLangToESpeakLangCodeMap[language];
}

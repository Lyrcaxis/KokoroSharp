namespace KokoroSharp;

using NumSharp;
using KokoroSharp.Core;

/// <summary> Helper module responsible for holding Kokoro Voices and making them accessible for retrieval by name. </summary>
/// <remarks> Also contains methods that allows mixing voices with each other to create new voices with shared characteristics. </remarks>
public static class KokoroVoiceManager {
    public static List<KokoroVoice> Voices { get; } = [];
    static HashSet<string> loadedFilePaths = [];

    /// <summary> Gathers and loads all voices on the specified path. ("voices" is the default path the Nuget Package bundles the voices at). </summary>
    /// <remarks> This exists in case developers want to ship their project with custom paths or use custom voice loading logic. </remarks>
    public static void LoadVoicesFromPath(string voicesPath = "voices") {
        if (voicesPath == "voices")
            voicesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, voicesPath);

        if (!Directory.Exists(voicesPath)) { throw new DirectoryNotFoundException(); }
        var voiceFilePaths = Directory.GetFiles(voicesPath);

        foreach (var filePath in voiceFilePaths) {
            if (!loadedFilePaths.Add(filePath) || !filePath.EndsWith(".npy")) { continue; }
            var voiceName = Path.GetFileNameWithoutExtension(filePath);
            var voiceFeatures = np.Load<float[,,]>(filePath);
            Voices.Add(new() { Name = voiceName, Features = voiceFeatures });
        }
    }

    /// <summary> Retrieves a loaded voice by name, including the language and gender prefix. Use <b>GetVoices</b> to see the full list. </summary>
    /// <remarks> Customly mixed voices will not be considered unless named and added to <see cref="Voices"/>. </remarks>
    public static KokoroVoice GetVoice(string name) {
        if (Voices.Count == 0) { LoadVoicesFromPath(); }
        return Voices.First(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary> Allows retrieving voices that can speak fluently in the specified language. </summary>
    /// <remarks> If 'gender' is specified, voices of different genders than the one specified will be ignored. </remarks>
    public static List<KokoroVoice> GetVoices(KokoroLanguage language, KokoroGender gender = KokoroGender.Both) => GetVoices([language], gender);

    /// <summary> Allows retrieving voices that can speak fluently in the specified languages. </summary>
    /// <remarks> If 'gender' is specified, voices of different genders than the one specified will be ignored. </remarks>
    public static List<KokoroVoice> GetVoices(IEnumerable<KokoroLanguage> languages, KokoroGender gender = KokoroGender.Both) {
        if (Voices.Count == 0) { LoadVoicesFromPath(); }
        var selectedVoices = Voices.FindAll(x => languages.Contains(x.GetLanguage()));
        if (gender != KokoroGender.Both) { selectedVoices.RemoveAll(x => x.Name[1] != (char) gender); }
        return selectedVoices;
    }

    /// <summary>
    /// <para> Mixes the given voices together with their weights, to produce a new voice. There is no limit on how many voices this can accept. </para>
    /// <para> The *weight* for each voice will determine its contribution to the output voice, which will be normalized based on the sum of all weights. </para>
    /// <para> The formula that's used is <b>NewVoice = (λa * A) + (λb * B) + ... + (λn * N)</b>, where <b>λ</b> are the normalized weights. </para>
    /// </summary>
    /// <remarks> The output voice's name will be <b>"xx_mix"</b>, where <b>xx</b> is the prefix of the first voice's name. If you're going for a different language, rename the voice after the operation. </remarks>
    public static KokoroVoice Mix(params (KokoroVoice voice, float weight)[] voices) {
        var f = voices[0].voice.Features;
        var (w, h, d) = (f.GetLength(0), f.GetLength(1), f.GetLength(2));

        var summedWeights = voices.Sum(x => x.weight);
        var normedWeights = voices.Select(x => x.weight / summedWeights).ToArray();

        var newArray = np.zeros_like(voices[0].voice.Features);
        for (int i = 0; i < voices.Length; i++) { newArray += np.array(voices[i].voice.Features) * normedWeights[i]; }
        var newFeatures = newArray.reshape(new Shape(w, h, d)).ToMuliDimArray<float>() as float[,,];

        // Try to infer the name. This is crucial to preserve the speaker's language for the new voice.
        var name = (voices[0].voice.Name.Length >= 3) ? $"{voices[0].voice.Name[..2]}_mix" : "am_mix";
        return new KokoroVoice() { Name = name, Features = newFeatures };
    }

    /// <summary> Mixes the two voices with formula <b>NewVoice = (λa * A) + (λb * B)</b>, where <b>λ</b> are the normalized weights. </summary>
    /// <remarks> For more fine-grained control and mixing multiple voices in one go, see <b>KokoroVoiceManager.Mix(..)</b>. </remarks>
    public static KokoroVoice MixWith(this KokoroVoice A, KokoroVoice B, float wA = 0.5f, float wB = 0.5f) => Mix([(A, wA), (B, wB)]);
}

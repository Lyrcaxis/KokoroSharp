namespace KokoroSharp.Core;

/// <summary> A Kokoro Voice instance, holding the speaker embeddings that *colors* the spoken text with specific characteristics. </summary>
/// <remarks> You can use the <b>KokoroVoiceManager.Mix(...)</b> method to create new voices out of the existing ones. </remarks>
public class KokoroVoice {
    string name;

    /// <summary> The name of the voice, consisting of 'LG_name', where 'L' is the language code, and 'G' is the gender. </summary>
    /// <remarks> For example, a Female British voice named Amy should be named "bf_Amy". </remarks>
    public string Name {
        get => name;
        init => name = value;
    }

    /// <summary> Contains the speaker embeddings for this voice, in C# format, but otherwise representing a [510, 1, 256] Tensor. </summary>
    /// <remarks> Can initialize this via <see cref="FromPath(string)"/>. See the documentation for more information on how to prepare `.pt` voices for use in KokoroSharp. </remarks>
    public float[,,] Features { get; init; }

    /// <summary> The language this voice's speaker is intended to be speaking. </summary>
    /// <remarks> It is based on the first character of <see cref="Name"/>. </remarks>
    public KokoroLanguage Language => this.GetLanguage();

    /// <summary> The gender of this voice's speaker. </summary>
    /// <remarks> It is based on the second character of <see cref="Name"/>. </remarks>
    public KokoroGender Gender => (KokoroGender) name[1];

    /// <summary> Exports the voice on specified path. The voice can later be retrieved again with <see cref="FromPath(string)"/>. </summary>
    public void Export(string filePath) => NumSharp.np.Save(Features, filePath);

    /// <summary> Rename the following voice to have it adhere to specific language and gender pronunciation rules. </summary>
    public void Rename(string name, KokoroLanguage language, KokoroGender gender) => this.name = $"{(char) language}{(char) gender}_{name}";

    /// <summary> Loads an exported voice from specified file path. </summary>
    public static KokoroVoice FromPath(string filePath) {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return new() { Name = name, Features = NumSharp.np.Load<float[,,]>(filePath) };
    }

    /// <summary> Implicit conversion between <see cref="KokoroVoice"/> and a <b>3D <see cref="float"/> array</b> (float[,,] aka Tensor [510, 1, 256]) </summary>
    public static implicit operator float[,,](KokoroVoice voice) => voice.Features;

    /// <summary> Implicit conversion between <see cref="KokoroVoice"/> and a <b>3D <see cref="float"/> array</b> (float[,,] aka Tensor [510, 1, 256]) </summary>
    public static implicit operator KokoroVoice(float[,,] features) => new() { Name = "", Features = features };
}

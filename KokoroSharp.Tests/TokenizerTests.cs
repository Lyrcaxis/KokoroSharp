using KokoroSharp.Processing;

namespace KokoroSharp.Tests;

public class TokenizerTests {
    [Test]
    [Arguments("$1", "1 dollar")]
    [Arguments("$1.50", "1 dollar 50")]
    [Arguments("$ 1.50", "1 dollar 50")]
    [Arguments("1€", "1 euro")]
    [Arguments("1,75 €", "1 euro 75")]
    [Arguments("1,75€", "1 euro 75")]
    [Arguments("3.1415", "3 point 1 4 1 5")]
    public async Task PreprocessText(string input, string expected) {
        await Assert.That(Tokenizer.PreprocessText(input)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("[Misaki](/misˈɑki/) is a G2P engine designed for [Kokoro](/kˈOkəɹO/) models.", "misˈɑki ɪz ɐ dʒˈi tˈu pˈi ˈɛndʒɪn dɪzˈaɪnd fˌɔɹ kˈOkəɹO mˈɑːdəlz")]
    [Arguments("Brits say [tomato](/təmɑːtoʊ/) instead of [tomato](/təmeɪtoʊ/).", "bɹˈɪts sˈeɪ təmɑːtoʊ ɪnstˈɛd ʌv təmeɪtoʊ")]
    [Arguments("Here, in this sentence, there are some commas.\nThis sentence is on another line! There are two sentences on this line.",
        "hˈɪɹ, ɪn ðɪs sˈɛntəns, ðɛɹˌɑːɹ sˌʌm kˈɑːməz.\nðɪs sˈɛntəns ɪz ˌɔn ɐnˈʌðɚ lˈaɪn! ðɛɹˌɑːɹ tˈu sˈɛntənsᵻz ˌɔn ðɪs lˈaɪn.")]
    public async Task Phonemize(string input, string expected) {
        await Assert.That(Tokenizer.Phonemize(input)).IsEqualTo(expected);
    }
}
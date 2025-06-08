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
}
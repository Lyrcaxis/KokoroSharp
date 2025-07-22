using BenchmarkDotNet.Attributes;

using System.Runtime.CompilerServices;

namespace Benchmark;

[SimpleJob]
public class Tokenizer {
    static string? text;

    [GlobalSetup]
    public void Setup() {
        var filePath = Path.Combine(GetMyPath(), "../..", "README.md");
        text = File.ReadAllText(filePath);
        static string GetMyPath([CallerFilePath] string filePath = "") => filePath; // Get the path of this file
    }

    [Benchmark]
    public string PreprocessText() {
        return KokoroSharp.Processing.Tokenizer.PreprocessText(text);
    }

    [Benchmark]
    public string Phonemize() {
        return KokoroSharp.Processing.Tokenizer.Phonemize(text, "en-us");
    }

    [Benchmark]
    public int[] Tokenize() {
        return KokoroSharp.Processing.Tokenizer.Tokenize(text);
    }
}

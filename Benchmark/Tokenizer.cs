using BenchmarkDotNet.Attributes;

using System.IO;
using System.Runtime.CompilerServices;

namespace Benchmark;

[SimpleJob]
public class Tokenizer {
    static readonly string text = File.ReadAllText(Path.Join(GetMyPath(), "../../README.md"));
    static string GetMyPath([CallerFilePath] string filePath = "") => filePath;

    [Benchmark] public string PreprocessText() => KokoroSharp.Processing.Tokenizer.PreprocessText(text);
    [Benchmark] public string Phonemize() => KokoroSharp.Processing.Tokenizer.Phonemize(text, "en-us");
    [Benchmark] public int[] Tokenize() => KokoroSharp.Processing.Tokenizer.Tokenize(text);
}

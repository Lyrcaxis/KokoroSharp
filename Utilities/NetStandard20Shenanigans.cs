#if NETSTANDARD2_0
namespace KokoroSharp;

using System.Collections.Concurrent;

internal static class NetStandard20Shenanigans {
    public static bool StartsWith(this string text, char c) => text.Length > 0 && text[0] == c;
    public static bool EndsWith(this string text, char c) => text.Length > 0 && text[text.Length - 1] == c;
    public static bool Contains(this string text, char c) => text.IndexOf(c) >= 0;

    public static void Deconstruct<K, V>(this KeyValuePair<K, V> pair, out K key, out V value) { (key, value) = (pair.Key, pair.Value); }
    public static V GetValueOrDefault<K, V>(this IReadOnlyDictionary<K, V> dict, K key) => dict.TryGetValue(key, out var value) ? value : default;
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);
    public static bool TryAdd<K, V>(this Dictionary<K, V> dict, K key, V value) {
        if (dict.ContainsKey(key)) { return false; }
        dict.Add(key, value);
        return true;
    }
    public static void Clear<T>(this ConcurrentQueue<T> queue) { while (queue.TryDequeue(out _)) { } }
}
#endif

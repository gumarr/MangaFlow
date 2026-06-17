using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MangaFlow.AI;

public class TranslationCache
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public bool TryGet(string sourceText, out string cachedTranslation)
    {
        var key = ComputeKey(sourceText);
        return _cache.TryGetValue(key, out cachedTranslation!);
    }

    public void Set(string sourceText, string translation)
    {
        var key = ComputeKey(sourceText);
        _cache[key] = translation;
    }

    public int Count => _cache.Count;

    public void Clear() => _cache.Clear();

    private static string ComputeKey(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}

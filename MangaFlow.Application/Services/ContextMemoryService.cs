using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.Application.Services;

public class ContextMemoryService : IContextMemoryService
{
    private readonly ConcurrentDictionary<Guid, List<(string Source, string Target)>> _contexts = new();

    public void AddContext(Guid projectId, string sourceText, string translatedText)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(translatedText)) return;

        _contexts.AddOrUpdate(projectId,
            // Add factories
            _ => new List<(string, string)> { (sourceText.Trim(), translatedText.Trim()) },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add((sourceText.Trim(), translatedText.Trim()));
                    // Restrict context to 20 items max
                    if (list.Count > 20)
                    {
                        list.RemoveAt(0);
                    }
                }
                return list;
            });
    }

    public IEnumerable<(string SourceText, string TranslatedText)> GetContext(Guid projectId, int limit = 15)
    {
        if (!_contexts.TryGetValue(projectId, out var list))
        {
            return Enumerable.Empty<(string, string)>();
        }

        lock (list)
        {
            return list.TakeLast(limit).ToList();
        }
    }

    public void ClearContext(Guid projectId)
    {
        _contexts.TryRemove(projectId, out _);
    }

    public string BuildContextPrompt(Guid projectId, int limit = 15)
    {
        var recentItems = GetContext(projectId, limit).ToList();
        if (!recentItems.Any()) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Recent translation history context for flow/consistency reference:");
        foreach (var (source, target) in recentItems)
        {
            sb.AppendLine($"Original: {source}");
            sb.AppendLine($"Translated: {target}");
            sb.AppendLine("---");
        }

        return sb.ToString();
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MangaFlow.Application.Interfaces;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class BubbleMemoryService : IBubbleMemoryService
{
    private readonly List<Bubble> _bubbles = new();
    private readonly object _lock = new();
    private const int DefaultCapacity = 10;

    public void AddBubble(string ocrText, string translatedText, string sourceImageHash)
    {
        lock (_lock)
        {
            var bubble = new Bubble
            {
                OcrText = ocrText?.Trim() ?? string.Empty,
                TranslatedText = translatedText?.Trim() ?? string.Empty,
                Timestamp = DateTime.UtcNow,
                SourceImageHash = sourceImageHash ?? string.Empty
            };

            _bubbles.Add(bubble);

            // Keep only the last N bubbles
            while (_bubbles.Count > DefaultCapacity)
            {
                _bubbles.RemoveAt(0);
            }
        }
    }

    public IEnumerable<Bubble> GetRecentBubbles(int limit = 10)
    {
        lock (_lock)
        {
            return _bubbles.AsEnumerable().Reverse().Take(limit).Reverse().ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _bubbles.Clear();
        }
    }
}

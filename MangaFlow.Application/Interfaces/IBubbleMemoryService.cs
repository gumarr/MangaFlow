using System.Collections.Generic;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Interfaces;

public interface IBubbleMemoryService
{
    void AddBubble(string ocrText, string translatedText, string sourceImageHash);
    IEnumerable<Bubble> GetRecentBubbles(int limit = 10);
    void Clear();
}

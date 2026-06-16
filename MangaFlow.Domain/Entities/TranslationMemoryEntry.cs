using System;

namespace MangaFlow.Domain.Entities;

public class TranslationMemoryEntry
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; } // Null if global memory, Guid if project memory
    public string SourceText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}

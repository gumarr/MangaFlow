using System;

namespace MangaFlow.Domain.Entities;

public class TranslationHistoryItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string ContextSnapshot { get; set; } = string.Empty; // Serialized context (e.g. JSON of last few segments)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

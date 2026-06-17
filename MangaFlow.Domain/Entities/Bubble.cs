using System;

namespace MangaFlow.Domain.Entities;

public class Bubble
{
    public string OcrText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SourceImageHash { get; set; } = string.Empty;
}

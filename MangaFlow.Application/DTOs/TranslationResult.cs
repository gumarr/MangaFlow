using System;

namespace MangaFlow.Application.DTOs;

public class TranslationResult
{
    public string TranslatedText { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public long ElapsedMilliseconds { get; set; }
}

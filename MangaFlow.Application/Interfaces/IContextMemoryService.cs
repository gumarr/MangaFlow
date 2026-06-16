using System;
using System.Collections.Generic;

namespace MangaFlow.Application.Interfaces;

public interface IContextMemoryService
{
    void AddContext(Guid projectId, string sourceText, string translatedText);
    IEnumerable<(string SourceText, string TranslatedText)> GetContext(Guid projectId, int limit = 15);
    void ClearContext(Guid projectId);
    string BuildContextPrompt(Guid projectId, int limit = 15);
}

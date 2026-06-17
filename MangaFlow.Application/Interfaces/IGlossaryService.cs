using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Interfaces;

public interface IGlossaryService
{
    Task<IEnumerable<GlossaryTerm>> GetProjectGlossaryAsync(Guid projectId);
    Task<IEnumerable<GlossaryTerm>> GetGlobalGlossaryAsync();
    Task AddTermAsync(Guid? projectId, string sourceText, string targetText, bool isLocked, int priority = 0);
    Task UpdateTermAsync(GlossaryTerm term);
    Task DeleteTermAsync(Guid id);
    Task<string> BuildGlossaryPromptAsync(Guid projectId, string text);
    Task ImportGlossaryAsync(Guid? projectId, string filePath);
    Task ExportGlossaryAsync(Guid? projectId, string filePath);
}

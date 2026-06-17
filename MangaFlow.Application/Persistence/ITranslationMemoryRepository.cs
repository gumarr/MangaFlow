using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Persistence;

public interface ITranslationMemoryRepository
{
    Task<TranslationMemoryEntry?> GetByIdAsync(Guid id);
    Task<IEnumerable<TranslationMemoryEntry>> GetByProjectIdAsync(Guid projectId);
    Task<IEnumerable<TranslationMemoryEntry>> GetGlobalEntriesAsync();
    Task<TranslationMemoryEntry?> FindMatchAsync(string sourceText, Guid? projectId);
    Task AddAsync(TranslationMemoryEntry entry);
    Task UpdateAsync(TranslationMemoryEntry entry);
    Task DeleteAsync(Guid id);
    Task ClearAllAsync();
}

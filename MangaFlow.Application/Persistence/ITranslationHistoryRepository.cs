using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Persistence;

public interface ITranslationHistoryRepository
{
    Task<TranslationHistoryItem?> GetByIdAsync(Guid id);
    Task<IEnumerable<TranslationHistoryItem>> GetByProjectIdAsync(Guid projectId);
    Task AddAsync(TranslationHistoryItem item);
    Task DeleteAsync(Guid id);
    Task ClearProjectHistoryAsync(Guid projectId);
}

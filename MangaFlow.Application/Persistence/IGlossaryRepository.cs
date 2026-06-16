using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Persistence;

public interface IGlossaryRepository
{
    Task<GlossaryTerm?> GetByIdAsync(Guid id);
    Task<IEnumerable<GlossaryTerm>> GetByProjectIdAsync(Guid projectId);
    Task<IEnumerable<GlossaryTerm>> GetGlobalTermsAsync();
    Task AddAsync(GlossaryTerm term);
    Task UpdateAsync(GlossaryTerm term);
    Task DeleteAsync(Guid id);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Persistence.Repositories;

public class GlossaryRepository : IGlossaryRepository
{
    private readonly MangaFlowDbContext _context;

    public GlossaryRepository(MangaFlowDbContext context)
    {
        _context = context;
    }

    public async Task<GlossaryTerm?> GetByIdAsync(Guid id)
    {
        return await _context.GlossaryTerms.FindAsync(id);
    }

    public async Task<IEnumerable<GlossaryTerm>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.GlossaryTerms
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.SourceText)
            .ToListAsync();
    }

    public async Task<IEnumerable<GlossaryTerm>> GetGlobalTermsAsync()
    {
        return await _context.GlossaryTerms
            .Where(t => t.ProjectId == null)
            .OrderBy(t => t.SourceText)
            .ToListAsync();
    }

    public async Task AddAsync(GlossaryTerm term)
    {
        await _context.GlossaryTerms.AddAsync(term);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(GlossaryTerm term)
    {
        _context.GlossaryTerms.Update(term);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var term = await GetByIdAsync(id);
        if (term != null)
        {
            _context.GlossaryTerms.Remove(term);
            await _context.SaveChangesAsync();
        }
    }
}

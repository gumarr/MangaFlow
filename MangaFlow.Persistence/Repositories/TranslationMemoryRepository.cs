using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Persistence.Repositories;

public class TranslationMemoryRepository : ITranslationMemoryRepository
{
    private readonly MangaFlowDbContext _context;

    public TranslationMemoryRepository(MangaFlowDbContext context)
    {
        _context = context;
    }

    public async Task<TranslationMemoryEntry?> GetByIdAsync(Guid id)
    {
        return await _context.TranslationMemoryEntries.FindAsync(id);
    }

    public async Task<IEnumerable<TranslationMemoryEntry>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.TranslationMemoryEntries
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.LastUsedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TranslationMemoryEntry>> GetGlobalEntriesAsync()
    {
        return await _context.TranslationMemoryEntries
            .Where(e => e.ProjectId == null)
            .OrderByDescending(e => e.LastUsedAt)
            .ToListAsync();
    }

    public async Task<TranslationMemoryEntry?> FindMatchAsync(string sourceText, Guid? projectId)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return null;

        var normalizedText = sourceText.Trim();

        return await _context.TranslationMemoryEntries
            .FirstOrDefaultAsync(e => e.SourceText == normalizedText && e.ProjectId == projectId);
    }

    public async Task AddAsync(TranslationMemoryEntry entry)
    {
        entry.SourceText = entry.SourceText.Trim();
        await _context.TranslationMemoryEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(TranslationMemoryEntry entry)
    {
        entry.SourceText = entry.SourceText.Trim();
        _context.TranslationMemoryEntries.Update(entry);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await GetByIdAsync(id);
        if (entry != null)
        {
            _context.TranslationMemoryEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Persistence.Repositories;

public class TranslationHistoryRepository : ITranslationHistoryRepository
{
    private readonly MangaFlowDbContext _context;

    public TranslationHistoryRepository(MangaFlowDbContext context)
    {
        _context = context;
    }

    public async Task<TranslationHistoryItem?> GetByIdAsync(Guid id)
    {
        return await _context.TranslationHistoryItems.FindAsync(id);
    }

    public async Task<IEnumerable<TranslationHistoryItem>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.TranslationHistoryItems
            .Where(h => h.ProjectId == projectId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(TranslationHistoryItem item)
    {
        await _context.TranslationHistoryItems.AddAsync(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var item = await GetByIdAsync(id);
        if (item != null)
        {
            _context.TranslationHistoryItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public async Task ClearProjectHistoryAsync(Guid projectId)
    {
        var items = await _context.TranslationHistoryItems
            .Where(h => h.ProjectId == projectId)
            .ToListAsync();
        
        _context.TranslationHistoryItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }
}

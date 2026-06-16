using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Persistence.Repositories;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly MangaFlowDbContext _context;

    public AppSettingsRepository(MangaFlowDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        var settings = await _context.Settings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AppSettings
            {
                Id = Guid.NewGuid(),
                OcrLanguage = "Japanese",
                OcrEngine = "RapidOCR",
                OcrModelPath = string.Empty,
                SelectedLlmModel = "Qwen 3 8B GGUF",
                LlmModelPath = string.Empty,
                CpuThreads = 4,
                Temperature = 0.3,
                UseGpu = true,
                GlobalHotkey = "Alt+Q",
                DefaultSourceLanguage = "Japanese",
                DefaultTargetLanguage = "English",
                ShowCapturePreview = false,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Settings.AddAsync(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var existing = await _context.Settings.FirstOrDefaultAsync(s => s.Id == settings.Id);
        if (existing == null)
        {
            await _context.Settings.AddAsync(settings);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(settings);
        }
        await _context.SaveChangesAsync();
    }
}

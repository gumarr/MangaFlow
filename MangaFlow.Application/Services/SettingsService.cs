using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(IAppSettingsRepository settingsRepository, ILogger<SettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        _logger.LogInformation("Retrieving application settings");
        try
        {
            return await _settingsRepository.GetSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve application settings");
            throw;
        }
    }

    public async Task UpdateSettingsAsync(AppSettings settings)
    {
        _logger.LogInformation("Saving application settings");
        try
        {
            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.SaveSettingsAsync(settings);
            _logger.LogInformation("Successfully saved application settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application settings");
            throw;
        }
    }
}

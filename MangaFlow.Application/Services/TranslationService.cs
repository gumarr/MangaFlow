using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class TranslationService : ITranslationService
{
    private readonly ITranslationMemoryRepository _tmRepository;
    private readonly ITranslationHistoryRepository _historyRepository;
    private readonly ITranslationContextService _contextService;
    private readonly ITranslationProvider _translationProvider;
    private readonly IBubbleMemoryService _bubbleMemoryService;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(
        ITranslationMemoryRepository tmRepository,
        ITranslationHistoryRepository historyRepository,
        ITranslationContextService contextService,
        ITranslationProvider translationProvider,
        IBubbleMemoryService bubbleMemoryService,
        ILogger<TranslationService> logger)
    {
        _tmRepository = tmRepository;
        _historyRepository = historyRepository;
        _contextService = contextService;
        _translationProvider = translationProvider;
        _bubbleMemoryService = bubbleMemoryService;
        _logger = logger;
    }

    public async Task<TranslationResult> TranslateAsync(Guid projectId, string text, string sourceLanguage, string targetLanguage, string sourceImageHash = "")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult
            {
                TranslatedText = string.Empty,
                ProviderName = _translationProvider.Name,
                IsSuccess = true,
                ElapsedMilliseconds = 0
            };
        }

        _logger.LogInformation("Starting translation pipeline for project {ProjectId}, text: '{Text}'", projectId, text);

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Layer 1: Series Translation Memory lookup
            _logger.LogDebug("Looking up in Series Translation Memory...");
            var match = await _tmRepository.FindMatchAsync(text, projectId);
            if (match != null)
            {
                _logger.LogInformation("Match found in Series Translation Memory: '{Translated}'", match.TranslatedText);
                match.LastUsedAt = DateTime.UtcNow;
                await _tmRepository.UpdateAsync(match);

                stopwatch.Stop();

                // Add to bubble memory
                _bubbleMemoryService.AddBubble(text, match.TranslatedText, sourceImageHash);

                // Add to history
                await SaveToHistoryAsync(projectId, text, match.TranslatedText);

                return new TranslationResult
                {
                    TranslatedText = match.TranslatedText,
                    ProviderName = "TranslationMemory",
                    IsSuccess = true,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            // Layer 2: Global Translation Memory lookup
            _logger.LogDebug("Looking up in Global Translation Memory...");
            match = await _tmRepository.FindMatchAsync(text, null);
            if (match != null)
            {
                _logger.LogInformation("Match found in Global Translation Memory: '{Translated}'", match.TranslatedText);
                match.LastUsedAt = DateTime.UtcNow;
                await _tmRepository.UpdateAsync(match);

                stopwatch.Stop();

                // Add to bubble memory
                _bubbleMemoryService.AddBubble(text, match.TranslatedText, sourceImageHash);

                // Add to history
                await SaveToHistoryAsync(projectId, text, match.TranslatedText);

                return new TranslationResult
                {
                    TranslatedText = match.TranslatedText,
                    ProviderName = "TranslationMemory",
                    IsSuccess = true,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            // Layer 3: Translation via Provider with TranslationContext
            _logger.LogDebug("No translation memory match. Proceeding to translation provider...");

            // Get translation context (recent bubbles + matched glossary terms)
            var context = await _contextService.GetTranslationContextAsync(projectId, text);

            // Call the provider
            var result = await _translationProvider.TranslateAsync(text, sourceLanguage, targetLanguage, context);

            if (result.IsSuccess)
            {
                result.TranslatedText = result.TranslatedText.Trim();

                // Save new translation to Series Translation Memory for future reuse
                var tmEntry = new TranslationMemoryEntry
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    SourceText = text.Trim(),
                    TranslatedText = result.TranslatedText,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };
                await _tmRepository.AddAsync(tmEntry);

                // Add to bubble memory
                _bubbleMemoryService.AddBubble(text, result.TranslatedText, sourceImageHash);

                // Add to history
                await SaveToHistoryAsync(projectId, text, result.TranslatedText);
            }

            _logger.LogInformation("Translation completed successfully: '{Translated}'", result.TranslatedText);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed translation pipeline for text: '{Text}'", text);
            return new TranslationResult
            {
                TranslatedText = string.Empty,
                ProviderName = _translationProvider.Name,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task SaveToHistoryAsync(Guid projectId, string sourceText, string translatedText)
    {
        var historyItem = new TranslationHistoryItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceText = sourceText,
            TranslatedText = translatedText,
            ContextSnapshot = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
        await _historyRepository.AddAsync(historyItem);
    }
}

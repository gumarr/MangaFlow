using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class TranslationService : ITranslationService
{
    private readonly ITranslationMemoryRepository _tmRepository;
    private readonly ITranslationHistoryRepository _historyRepository;
    private readonly IGlossaryService _glossaryService;
    private readonly IContextMemoryService _contextMemoryService;
    private readonly ITranslationEngine _translationEngine;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(
        ITranslationMemoryRepository tmRepository,
        ITranslationHistoryRepository historyRepository,
        IGlossaryService glossaryService,
        IContextMemoryService contextMemoryService,
        ITranslationEngine translationEngine,
        ILogger<TranslationService> logger)
    {
        _tmRepository = tmRepository;
        _historyRepository = historyRepository;
        _glossaryService = glossaryService;
        _contextMemoryService = contextMemoryService;
        _translationEngine = translationEngine;
        _logger = logger;
    }

    public async Task<string> TranslateAsync(Guid projectId, string text, string sourceLanguage, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        _logger.LogInformation("Starting translation pipeline for project {ProjectId}, text: '{Text}'", projectId, text);

        try
        {
            // Layer 1: Series Translation Memory lookup
            _logger.LogDebug("Looking up in Series Translation Memory...");
            var match = await _tmRepository.FindMatchAsync(text, projectId);
            if (match != null)
            {
                _logger.LogInformation("Match found in Series Translation Memory: '{Translated}'", match.TranslatedText);
                match.LastUsedAt = DateTime.UtcNow;
                await _tmRepository.UpdateAsync(match);

                // Add to recent context
                _contextMemoryService.AddContext(projectId, text, match.TranslatedText);

                // Add to history
                await SaveToHistoryAsync(projectId, text, match.TranslatedText);

                return match.TranslatedText;
            }

            // Layer 2: Global Translation Memory lookup
            _logger.LogDebug("Looking up in Global Translation Memory...");
            match = await _tmRepository.FindMatchAsync(text, null);
            if (match != null)
            {
                _logger.LogInformation("Match found in Global Translation Memory: '{Translated}'", match.TranslatedText);
                match.LastUsedAt = DateTime.UtcNow;
                await _tmRepository.UpdateAsync(match);

                // Add to recent context
                _contextMemoryService.AddContext(projectId, text, match.TranslatedText);

                // Add to history
                await SaveToHistoryAsync(projectId, text, match.TranslatedText);

                return match.TranslatedText;
            }

            // Layer 3: Translation via LLM Engine with Glossary & Context Memory
            _logger.LogDebug("No translation memory match. Proceeding to LLM translation...");

            // Get Glossary context prompt
            var glossaryPrompt = await _glossaryService.BuildGlossaryPromptAsync(projectId, text);

            // Get Context memory prompt
            var contextPrompt = _contextMemoryService.BuildContextPrompt(projectId);

            // Translate using the engine
            var translatedText = await _translationEngine.TranslateAsync(text, sourceLanguage, targetLanguage, glossaryPrompt, contextPrompt);
            translatedText = translatedText.Trim();

            // Save new translation to Series Translation Memory for future reuse
            var tmEntry = new TranslationMemoryEntry
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                SourceText = text.Trim(),
                TranslatedText = translatedText,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };
            await _tmRepository.AddAsync(tmEntry);

            // Add to recent context
            _contextMemoryService.AddContext(projectId, text, translatedText);

            // Add to history
            await SaveToHistoryAsync(projectId, text, translatedText);

            _logger.LogInformation("Translation completed successfully: '{Translated}'", translatedText);
            return translatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed translation pipeline for text: '{Text}'", text);
            throw;
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
            ContextSnapshot = string.Empty, // Could serialize recent context if needed
            CreatedAt = DateTime.UtcNow
        };
        await _historyRepository.AddAsync(historyItem);
    }
}

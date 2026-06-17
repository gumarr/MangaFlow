using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class TranslationContextService : ITranslationContextService
{
    private readonly IBubbleMemoryService _bubbleMemoryService;
    private readonly IGlossaryRepository _glossaryRepository;

    public TranslationContextService(IBubbleMemoryService bubbleMemoryService, IGlossaryRepository glossaryRepository)
    {
        _bubbleMemoryService = bubbleMemoryService;
        _glossaryRepository = glossaryRepository;
    }

    public async Task<TranslationContext> GetTranslationContextAsync(Guid projectId, string text)
    {
        var context = new TranslationContext();

        // 1. Get recent bubbles (limit 3, as per requirement: "Given recent bubbles: Bubble 1, Bubble 2, Bubble 3")
        context.RecentBubbles = _bubbleMemoryService.GetRecentBubbles(3).ToList();

        // 2. Fetch matched glossary terms
        if (!string.IsNullOrWhiteSpace(text))
        {
            var projectTerms = await _glossaryRepository.GetByProjectIdAsync(projectId);
            var globalTerms = await _glossaryRepository.GetGlobalTermsAsync();
            var allTerms = projectTerms.Concat(globalTerms).ToList();

            foreach (var term in allTerms)
            {
                if (string.IsNullOrWhiteSpace(term.SourceText)) continue;

                if (text.Contains(term.SourceText, StringComparison.OrdinalIgnoreCase))
                {
                    context.GlossaryTerms.Add(term);
                }
            }

            // Sort terms by priority (highest first)
            context.GlossaryTerms = context.GlossaryTerms.OrderByDescending(t => t.Priority).ToList();
        }

        return context;
    }
}

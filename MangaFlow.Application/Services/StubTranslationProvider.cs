using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.Application.Services;

public class StubTranslationProvider : ITranslationProvider
{
    public string Name => "StubTranslator";

    public Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, TranslationContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new TranslationResult
            {
                TranslatedText = string.Empty,
                ProviderName = Name,
                IsSuccess = true,
                ElapsedMilliseconds = 0
            });
        }

        // Apply glossary translations from context if any match
        string translated = text;

        if (context.GlossaryTerms != null && context.GlossaryTerms.Count > 0)
        {
            foreach (var term in context.GlossaryTerms)
            {
                // Replace case-insensitive term with its target translation
                translated = System.Text.RegularExpressions.Regex.Replace(
                    translated, 
                    System.Text.RegularExpressions.Regex.Escape(term.SourceText), 
                    term.TargetText, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
        }

        // Simulating translation context / memory fallback if glossary didn't fully replace
        if (translated == text)
        {
            // Simple mock translations for testing
            if (text.Contains("Mana Crystal", StringComparison.OrdinalIgnoreCase))
            {
                translated = "Tinh Thạch Ma Lực";
            }
            else if (text.Contains("Hello", StringComparison.OrdinalIgnoreCase))
            {
                translated = "Xin chào Thế giới";
            }
            else
            {
                translated = $"[Vietnamese] {text}";
            }
        }

        stopwatch.Stop();

        return Task.FromResult(new TranslationResult
        {
            TranslatedText = translated,
            ProviderName = Name,
            IsSuccess = true,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        });
    }
}

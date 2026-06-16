using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.Services;

public class GlossaryService : IGlossaryService
{
    private readonly IGlossaryRepository _glossaryRepository;
    private readonly ILogger<GlossaryService> _logger;

    public GlossaryService(IGlossaryRepository glossaryRepository, ILogger<GlossaryService> logger)
    {
        _glossaryRepository = glossaryRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<GlossaryTerm>> GetProjectGlossaryAsync(Guid projectId)
    {
        _logger.LogInformation("Retrieving glossary for project: {ProjectId}", projectId);
        return await _glossaryRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<IEnumerable<GlossaryTerm>> GetGlobalGlossaryAsync()
    {
        _logger.LogInformation("Retrieving global glossary");
        return await _glossaryRepository.GetGlobalTermsAsync();
    }

    public async Task AddTermAsync(Guid? projectId, string sourceText, string targetText, bool isLocked)
    {
        _logger.LogInformation("Adding glossary term: '{SourceText}' -> '{TargetText}'", sourceText, targetText);
        var term = new GlossaryTerm
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceText = sourceText.Trim(),
            TargetText = targetText.Trim(),
            IsLocked = isLocked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _glossaryRepository.AddAsync(term);
    }

    public async Task UpdateTermAsync(GlossaryTerm term)
    {
        _logger.LogInformation("Updating glossary term ID: {TermId}", term.Id);
        term.UpdatedAt = DateTime.UtcNow;
        await _glossaryRepository.UpdateAsync(term);
    }

    public async Task DeleteTermAsync(Guid id)
    {
        _logger.LogInformation("Deleting glossary term ID: {TermId}", id);
        await _glossaryRepository.DeleteAsync(id);
    }

    public async Task<string> BuildGlossaryPromptAsync(Guid projectId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var projectTerms = await _glossaryRepository.GetByProjectIdAsync(projectId);
        var globalTerms = await _glossaryRepository.GetGlobalTermsAsync();
        var allTerms = projectTerms.Concat(globalTerms).ToList();

        var matchedTerms = new List<GlossaryTerm>();

        foreach (var term in allTerms)
        {
            if (string.IsNullOrWhiteSpace(term.SourceText)) continue;
            
            // Check if source text exists in target text block (case-insensitive)
            if (text.Contains(term.SourceText, StringComparison.OrdinalIgnoreCase))
            {
                matchedTerms.Add(term);
            }
        }

        if (!matchedTerms.Any()) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Glossary Terms to respect (Source -> Translation):");
        foreach (var term in matchedTerms)
        {
            sb.AppendLine($"- {term.SourceText} -> {term.TargetText} {(term.IsLocked ? "(LOCKED: MUST USE EXACTLY)" : "")}");
        }

        return sb.ToString();
    }

    public async Task ImportGlossaryAsync(Guid? projectId, string filePath)
    {
        _logger.LogInformation("Importing glossary terms from file: {FilePath}", filePath);
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Glossary import file not found", filePath);
            }

            var json = await File.ReadAllTextAsync(filePath);
            var termsDto = JsonSerializer.Deserialize<List<GlossaryTermImportExportDto>>(json);
            if (termsDto == null) return;

            foreach (var dto in termsDto)
            {
                await AddTermAsync(projectId, dto.SourceText, dto.TargetText, dto.IsLocked);
            }

            _logger.LogInformation("Successfully imported {Count} terms", termsDto.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import glossary terms from: {FilePath}", filePath);
            throw;
        }
    }

    public async Task ExportGlossaryAsync(Guid? projectId, string filePath)
    {
        _logger.LogInformation("Exporting glossary terms to file: {FilePath}", filePath);
        try
        {
            IEnumerable<GlossaryTerm> terms;
            if (projectId.HasValue)
            {
                terms = await _glossaryRepository.GetByProjectIdAsync(projectId.Value);
            }
            else
            {
                terms = await _glossaryRepository.GetGlobalTermsAsync();
            }

            var dtos = terms.Select(t => new GlossaryTermImportExportDto
            {
                SourceText = t.SourceText,
                TargetText = t.TargetText,
                IsLocked = t.IsLocked
            }).ToList();

            var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Successfully exported {Count} terms", dtos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export glossary terms to: {FilePath}", filePath);
            throw;
        }
    }

    private class GlossaryTermImportExportDto
    {
        public string SourceText { get; set; } = string.Empty;
        public string TargetText { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
    }
}

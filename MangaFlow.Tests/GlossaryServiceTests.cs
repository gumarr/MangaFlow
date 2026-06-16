using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MangaFlow.Application.Persistence;
using MangaFlow.Application.Services;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Tests;

public class GlossaryServiceTests
{
    private readonly Mock<IGlossaryRepository> _glossaryRepoMock;
    private readonly Mock<ILogger<GlossaryService>> _loggerMock;
    private readonly GlossaryService _glossaryService;

    public GlossaryServiceTests()
    {
        _glossaryRepoMock = new Mock<IGlossaryRepository>();
        _loggerMock = new Mock<ILogger<GlossaryService>>();
        _glossaryService = new GlossaryService(_glossaryRepoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task BuildGlossaryPromptAsync_ShouldReturnEmpty_WhenNoTermsMatch()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var text = "これは無関係のテキストです。"; // unrelated text
        
        _glossaryRepoMock.Setup(repo => repo.GetByProjectIdAsync(projectId))
            .ReturnsAsync(new List<GlossaryTerm>());
        _glossaryRepoMock.Setup(repo => repo.GetGlobalTermsAsync())
            .ReturnsAsync(new List<GlossaryTerm>
            {
                new() { SourceText = "勇者", TargetText = "Hero" }
            });

        // Act
        var result = await _glossaryService.BuildGlossaryPromptAsync(projectId, text);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task BuildGlossaryPromptAsync_ShouldIncludeTerms_WhenTermsMatch()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var text = "勇者が現れた！"; // "The Hero appeared!"
        
        var projectTerms = new List<GlossaryTerm>
        {
            new() { SourceText = "勇者", TargetText = "Hero", IsLocked = true }
        };
        var globalTerms = new List<GlossaryTerm>
        {
            new() { SourceText = "魔王", TargetText = "Demon King", IsLocked = false }
        };

        _glossaryRepoMock.Setup(repo => repo.GetByProjectIdAsync(projectId))
            .ReturnsAsync(projectTerms);
        _glossaryRepoMock.Setup(repo => repo.GetGlobalTermsAsync())
            .ReturnsAsync(globalTerms);

        // Act
        var result = await _glossaryService.BuildGlossaryPromptAsync(projectId, text);

        // Assert
        Assert.Contains("勇者 -> Hero (LOCKED: MUST USE EXACTLY)", result);
        Assert.DoesNotContain("魔王 -> Demon King", result); // Because "魔王" is not in the text
    }
}

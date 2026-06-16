using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Application.Services;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Tests;

public class TranslationMemoryTests
{
    private readonly Mock<ITranslationMemoryRepository> _tmRepoMock;
    private readonly Mock<ITranslationHistoryRepository> _historyRepoMock;
    private readonly Mock<IGlossaryService> _glossaryServiceMock;
    private readonly Mock<IContextMemoryService> _contextMemoryServiceMock;
    private readonly Mock<ITranslationEngine> _translationEngineMock;
    private readonly Mock<ILogger<TranslationService>> _loggerMock;
    private readonly TranslationService _translationService;

    public TranslationMemoryTests()
    {
        _tmRepoMock = new Mock<ITranslationMemoryRepository>();
        _historyRepoMock = new Mock<ITranslationHistoryRepository>();
        _glossaryServiceMock = new Mock<IGlossaryService>();
        _contextMemoryServiceMock = new Mock<IContextMemoryService>();
        _translationEngineMock = new Mock<ITranslationEngine>();
        _loggerMock = new Mock<ILogger<TranslationService>>();

        _translationService = new TranslationService(
            _tmRepoMock.Object,
            _historyRepoMock.Object,
            _glossaryServiceMock.Object,
            _contextMemoryServiceMock.Object,
            _translationEngineMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task TranslateAsync_ShouldReturnSeriesMatch_WhenSeriesMatchExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceText = "こんにちは";
        var expectedTranslation = "Hello (Series TM)";
        
        var seriesMatch = new TranslationMemoryEntry
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceText = sourceText,
            TranslatedText = expectedTranslation
        };

        _tmRepoMock.Setup(repo => repo.FindMatchAsync(sourceText, projectId))
            .ReturnsAsync(seriesMatch);

        // Act
        var result = await _translationService.TranslateAsync(projectId, sourceText, "Japanese", "English");

        // Assert
        Assert.Equal(expectedTranslation, result);
        _tmRepoMock.Verify(repo => repo.UpdateAsync(seriesMatch), Times.Once);
        _contextMemoryServiceMock.Verify(cms => cms.AddContext(projectId, sourceText, expectedTranslation), Times.Once);
        _historyRepoMock.Verify(repo => repo.AddAsync(It.IsAny<TranslationHistoryItem>()), Times.Once);
        _translationEngineMock.Verify(engine => engine.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TranslateAsync_ShouldReturnGlobalMatch_WhenGlobalMatchExistsAndNoSeriesMatch()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceText = "こんにちは";
        var expectedTranslation = "Hello (Global TM)";
        
        var globalMatch = new TranslationMemoryEntry
        {
            Id = Guid.NewGuid(),
            ProjectId = null,
            SourceText = sourceText,
            TranslatedText = expectedTranslation
        };

        _tmRepoMock.Setup(repo => repo.FindMatchAsync(sourceText, projectId))
            .ReturnsAsync((TranslationMemoryEntry?)null);
        _tmRepoMock.Setup(repo => repo.FindMatchAsync(sourceText, null))
            .ReturnsAsync(globalMatch);

        // Act
        var result = await _translationService.TranslateAsync(projectId, sourceText, "Japanese", "English");

        // Assert
        Assert.Equal(expectedTranslation, result);
        _tmRepoMock.Verify(repo => repo.UpdateAsync(globalMatch), Times.Once);
        _contextMemoryServiceMock.Verify(cms => cms.AddContext(projectId, sourceText, expectedTranslation), Times.Once);
        _translationEngineMock.Verify(engine => engine.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TranslateAsync_ShouldInvokeEngine_WhenNoMatchExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceText = "こんにちは";
        var expectedTranslation = "Hello (AI)";

        _tmRepoMock.Setup(repo => repo.FindMatchAsync(sourceText, It.IsAny<Guid?>()))
            .ReturnsAsync((TranslationMemoryEntry?)null);

        _glossaryServiceMock.Setup(gs => gs.BuildGlossaryPromptAsync(projectId, sourceText))
            .ReturnsAsync("Glossary Context");

        _contextMemoryServiceMock.Setup(cms => cms.BuildContextPrompt(projectId, 15))
            .Returns("Context Context");

        _translationEngineMock.Setup(engine => engine.TranslateAsync(sourceText, "Japanese", "English", "Glossary Context", "Context Context"))
            .ReturnsAsync(expectedTranslation);

        // Act
        var result = await _translationService.TranslateAsync(projectId, sourceText, "Japanese", "English");

        // Assert
        Assert.Equal(expectedTranslation, result);
        _tmRepoMock.Verify(repo => repo.AddAsync(It.Is<TranslationMemoryEntry>(entry => entry.ProjectId == projectId && entry.SourceText == sourceText && entry.TranslatedText == expectedTranslation)), Times.Once);
        _contextMemoryServiceMock.Verify(cms => cms.AddContext(projectId, sourceText, expectedTranslation), Times.Once);
    }
}

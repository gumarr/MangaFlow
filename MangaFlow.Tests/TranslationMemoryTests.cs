using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Persistence;
using MangaFlow.Application.Services;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Tests;

public class TranslationMemoryTests
{
    private readonly Mock<ITranslationMemoryRepository> _tmRepoMock;
    private readonly Mock<ITranslationHistoryRepository> _historyRepoMock;
    private readonly Mock<ITranslationContextService> _contextServiceMock;
    private readonly Mock<ITranslationProvider> _translationProviderMock;
    private readonly Mock<IBubbleMemoryService> _bubbleMemoryServiceMock;
    private readonly Mock<ILogger<TranslationService>> _loggerMock;
    private readonly TranslationService _translationService;

    public TranslationMemoryTests()
    {
        _tmRepoMock = new Mock<ITranslationMemoryRepository>();
        _historyRepoMock = new Mock<ITranslationHistoryRepository>();
        _contextServiceMock = new Mock<ITranslationContextService>();
        _translationProviderMock = new Mock<ITranslationProvider>();
        _bubbleMemoryServiceMock = new Mock<IBubbleMemoryService>();
        _loggerMock = new Mock<ILogger<TranslationService>>();

        _translationProviderMock.Setup(p => p.Name).Returns("StubTranslator");

        _translationService = new TranslationService(
            _tmRepoMock.Object,
            _historyRepoMock.Object,
            _contextServiceMock.Object,
            _translationProviderMock.Object,
            _bubbleMemoryServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task TranslateAsync_ShouldReturnSeriesMatch_WhenSeriesMatchExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceText = "Hello";
        var expectedTranslation = "Xin chào (Series TM)";
        
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
        var result = await _translationService.TranslateAsync(projectId, sourceText, "English", "Vietnamese");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTranslation, result.TranslatedText);
        _tmRepoMock.Verify(repo => repo.UpdateAsync(seriesMatch), Times.Once);
        _bubbleMemoryServiceMock.Verify(b => b.AddBubble(sourceText, expectedTranslation, It.IsAny<string>()), Times.Once);
        _historyRepoMock.Verify(repo => repo.AddAsync(It.IsAny<TranslationHistoryItem>()), Times.Once);
        _translationProviderMock.Verify(provider => provider.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TranslationContext>()), Times.Never);
    }

    [Fact]
    public async Task TranslateAsync_ShouldReturnGlobalMatch_WhenGlobalMatchExistsAndNoSeriesMatch()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceText = "Hello";
        var expectedTranslation = "Xin chào (Global TM)";
        
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
        var result = await _translationService.TranslateAsync(projectId, sourceText, "English", "Vietnamese");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTranslation, result.TranslatedText);
        _tmRepoMock.Verify(repo => repo.UpdateAsync(globalMatch), Times.Once);
        _bubbleMemoryServiceMock.Verify(b => b.AddBubble(sourceText, expectedTranslation, It.IsAny<string>()), Times.Once);
        _translationProviderMock.Verify(provider => provider.TranslateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TranslationContext>()), Times.Never);
    }

    [Fact]
    public async Task TranslateAsync_ShouldInvokeProvider_WhenNoMatchExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceText = "Hello";
        var expectedTranslation = "Xin chào";
        var context = new TranslationContext();

        _tmRepoMock.Setup(repo => repo.FindMatchAsync(sourceText, It.IsAny<Guid?>()))
            .ReturnsAsync((TranslationMemoryEntry?)null);

        _contextServiceMock.Setup(c => c.GetTranslationContextAsync(projectId, sourceText))
            .ReturnsAsync(context);

        var providerResult = new TranslationResult
        {
            TranslatedText = expectedTranslation,
            ProviderName = "StubTranslator",
            IsSuccess = true
        };

        _translationProviderMock.Setup(provider => provider.TranslateAsync(sourceText, "English", "Vietnamese", context))
            .ReturnsAsync(providerResult);

        // Act
        var result = await _translationService.TranslateAsync(projectId, sourceText, "English", "Vietnamese");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTranslation, result.TranslatedText);
        _tmRepoMock.Verify(repo => repo.AddAsync(It.Is<TranslationMemoryEntry>(entry => entry.ProjectId == projectId && entry.SourceText == sourceText && entry.TranslatedText == expectedTranslation)), Times.Once);
        _bubbleMemoryServiceMock.Verify(b => b.AddBubble(sourceText, expectedTranslation, It.IsAny<string>()), Times.Once);
    }
}

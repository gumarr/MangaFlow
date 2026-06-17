using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;
using MangaFlow.Application.Services;

namespace MangaFlow.Tests;

public class OcrBenchmarkServiceTests
{
    [Fact]
    public async Task RunBenchmarkAsync_ShouldComputeCorrectMetricsAndGenerateReports()
    {
        // 1. Arrange
        var ocrServiceMock = new Mock<IOcrService>();
        var loggerMock = new Mock<ILogger<OcrBenchmarkService>>();

        string tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestDataset_" + Guid.NewGuid());
        Directory.CreateDirectory(tempFolder);

        try
        {
            // Create a test sample
            string imagePath1 = Path.Combine(tempFolder, "sample_001.png");
            string txtPath1 = Path.Combine(tempFolder, "sample_001.txt");

            await File.WriteAllBytesAsync(imagePath1, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // Fake PNG header
            await File.WriteAllTextAsync(txtPath1, "Hello World");

            // Mock OCR response
            var ocrResult = new OcrResult();
            ocrResult.Lines.Add(new OcrLine { Text = "Hello Word" });

            ocrServiceMock.Setup(o => o.RecognizeTextAsync(imagePath1, "English"))
                .ReturnsAsync(ocrResult);

            var benchmarkService = new OcrBenchmarkService(ocrServiceMock.Object, loggerMock.Object);

            // 2. Act
            var result = await benchmarkService.RunBenchmarkAsync(OcrProvider.PpOcrV5English, tempFolder);

            // 3. Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.TotalSamples);
            Assert.Single(result.SampleResults);

            var sample = result.SampleResults.First();
            Assert.Equal("sample_001.png", sample.SampleName);
            Assert.Equal("Hello World", sample.GroundTruth);
            Assert.Equal("Hello Word", sample.OcrText);

            // "Hello World" (len 11) vs "Hello Word" (len 10). Levenshtein distance = 1.
            // character accuracy: 1 - 1 / 11 = 10 / 11 = 0.909090...
            Assert.True(sample.CharacterAccuracy > 0.90 && sample.CharacterAccuracy < 0.91);
            Assert.True(sample.Cer > 0.09 && sample.Cer < 0.10);

            // Word level: "Hello", "World" vs "Hello", "Word". Levenshtein = 1.
            // word accuracy: 1 - 1 / 2 = 0.5.
            Assert.Equal(0.5, sample.WordAccuracy);
            Assert.Equal(0.5, sample.Wer);

            // Check reports
            Assert.True(File.Exists(result.ReportsPath));
            string reportContent = await File.ReadAllTextAsync(result.ReportsPath);
            Assert.Contains("# OCR Benchmark Report", reportContent);
            Assert.Contains("Avg Character Accuracy", reportContent);
            Assert.Contains("Hello World", reportContent);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }
}

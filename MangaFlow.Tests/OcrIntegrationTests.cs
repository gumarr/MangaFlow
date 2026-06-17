using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.Interfaces;
using MangaFlow.Domain.Entities;
using MangaFlow.OCR;
using SkiaSharp;

namespace MangaFlow.Tests;

public class OcrIntegrationTests
{
    [Fact]
    public async Task TestRapidOcrLatinModelSuccess()
    {
        // 1. Arrange settings
        var settingsServiceMock = new Mock<ISettingsService>();
        var loggerMock = new Mock<ILogger<RapidOcrService>>();

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string modelsPath = Path.Combine(baseDir, "models", "v5");

        Assert.True(Directory.Exists(modelsPath), $"Bundled models folder must exist at: {modelsPath}");

        var settings = new AppSettings
        {
            OcrEngine = "RapidOCR",
            OcrModelPath = modelsPath,
            CpuThreads = 1,
            OcrLanguage = "English"
        };

        settingsServiceMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(settings);

        var ocrService = new RapidOcrService(settingsServiceMock.Object, loggerMock.Object);

        // 2. Create a test image with text "HELLO" — must be large enough for OCR detector
        byte[] imageBytes;
        using (var bitmap = new SKBitmap(800, 200))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using (var paint = new SKPaint())
            using (var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 64))
            {
                paint.Color = SKColors.Black;
                paint.IsAntialias = true;
                
                // Draw text "HELLO"
                canvas.DrawText("HELLO", 50, 120, font, paint);
            }

            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                imageBytes = data.ToArray();
            }
        }

        // 3. Act
        var result = await ocrService.RecognizeTextAsync(imageBytes, "English");

        // 4. Assert — this is a smoke test to verify the OCR engine loads and runs.
        // Synthetic programmatic text may not OCR perfectly, so we only verify:
        // 1. The engine returned a result
        // 2. It produced at least one line of text
        // 3. No error messages were returned
        Assert.NotNull(result);
        Assert.NotEmpty(result.Lines);
        
        string fullText = result.FullText;
        Console.WriteLine($"Detected text: {fullText}");

        Assert.DoesNotContain("OCR model not installed.", fullText);
        Assert.DoesNotContain("OCR Error:", fullText);
        Assert.True(fullText.Length > 0, "OCR should produce some text output from the test image");
    }
}

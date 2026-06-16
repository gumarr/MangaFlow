using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MangaFlow.Infrastructure;
using Xunit;

namespace MangaFlow.Tests;

public class ScreenCaptureServiceTests
{
    [Fact]
    public async Task CaptureScreenRegionAsync_ShouldReturnByteArray_AndSaveTempFile()
    {
        // Arrange
        var service = new ScreenCaptureService(NullLogger<ScreenCaptureService>.Instance);
        double x = 10;
        double y = 20;
        double width = 100;
        double height = 50;

        // Act
        var result = await service.CaptureScreenRegionAsync(x, y, width, height);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Verify that a temporary file was saved in LocalApplicationData/MangaFlow/Temp
        var tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MangaFlow", "Temp");
        Assert.True(Directory.Exists(tempFolder), $"Temp folder at {tempFolder} does not exist.");
        
        var files = Directory.GetFiles(tempFolder, "capture_*.png");
        Assert.NotEmpty(files);
    }
}

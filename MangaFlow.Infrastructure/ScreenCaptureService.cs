using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using MangaFlow.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MangaFlow.Infrastructure;

public class ScreenCaptureService : IScreenCaptureService
{
    private readonly ILogger<ScreenCaptureService> _logger;

    public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> CaptureScreenRegionAsync(double x, double y, double width, double height)
    {
        int ix = (int)Math.Max(0, x);
        int iy = (int)Math.Max(0, y);
        int iw = (int)Math.Max(1, width);
        int ih = (int)Math.Max(1, height);

        _logger.LogInformation("CaptureScreenRegionAsync starting at coordinates ({X}, {Y}), size {Width}x{Height}", ix, iy, iw, ih);

        Bitmap? bmp = null;
        try
        {
            bmp = new Bitmap(iw, ih, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(ix, iy, 0, 0, new Size(iw, ih), CopyPixelOperation.SourceCopy);
            }
            _logger.LogInformation("Successfully captured physical screen region.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Physical screen capture failed (likely headless or no display session). Falling back to placeholder bitmap.");
            bmp?.Dispose();
            bmp = new Bitmap(iw, ih, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.DarkBlue);
                using (Font f = new Font("Arial", 12))
                {
                    g.DrawString($"Fallback Screen Capture\nSize: {iw}x{ih}", f, Brushes.White, 10, 10);
                }
            }
        }

        try
        {
            var tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MangaFlow", "Temp");
            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }
            var tempPath = Path.Combine(tempFolder, $"capture_{DateTime.UtcNow.Ticks}.png");
            bmp.Save(tempPath, ImageFormat.Png);
            _logger.LogInformation("Temporary screen capture image saved to: {Path}", tempPath);

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return Task.FromResult(ms.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save or encode captured image.");
            throw;
        }
        finally
        {
            bmp.Dispose();
        }
    }
}

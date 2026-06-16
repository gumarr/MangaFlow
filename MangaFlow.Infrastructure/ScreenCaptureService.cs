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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> CaptureFullScreenAsync()
    {
        _logger.LogInformation("Capturing entire virtual screen...");
        int vx = 0;
        int vy = 0;
        int vw = 0;
        int vh = 0;

        try
        {
            vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (vw == 0 || vh == 0)
            {
                vx = 0;
                vy = 0;
                vw = GetSystemMetrics(0); // SM_CXSCREEN
                vh = GetSystemMetrics(1); // SM_CYSCREEN
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve virtual screen metrics. Defaulting to 1920x1080.");
            vw = 1920;
            vh = 1080;
        }

        Bitmap? bmp = null;
        try
        {
            bmp = new Bitmap(vw, vh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(vx, vy, 0, 0, new Size(vw, vh), CopyPixelOperation.SourceCopy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Full screen copy failed (headless). Creating fallback slate.");
            bmp?.Dispose();
            bmp = new Bitmap(vw, vh, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.DarkSlateGray);
                using (Font f = new Font("Arial", 24))
                {
                    g.DrawString($"Headless Mock Screen ({vw}x{vh})", f, Brushes.White, 100, 100);
                }
            }
        }

        try
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return Task.FromResult(ms.ToArray());
            }
        }
        finally
        {
            bmp.Dispose();
        }
    }

    public Task<byte[]> CropImageAsync(byte[] imageBytes, int x, int y, int width, int height)
    {
        _logger.LogInformation("Cropping region at ({X}, {Y}), size {Width}x{Height}", x, y, width, height);
        
        if (imageBytes == null || imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes cannot be null or empty", nameof(imageBytes));
        }

        try
        {
            using (MemoryStream ms = new MemoryStream(imageBytes))
            {
                using (Bitmap srcBmp = new Bitmap(ms))
                {
                    int cx = Math.Max(0, Math.Min(x, srcBmp.Width - 1));
                    int cy = Math.Max(0, Math.Min(y, srcBmp.Height - 1));
                    int cw = Math.Max(1, Math.Min(width, srcBmp.Width - cx));
                    int ch = Math.Max(1, Math.Min(height, srcBmp.Height - cy));

                    using (Bitmap croppedBmp = srcBmp.Clone(new Rectangle(cx, cy, cw, ch), srcBmp.PixelFormat))
                    {
                        var tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MangaFlow", "Temp");
                        if (!Directory.Exists(tempFolder))
                        {
                            Directory.CreateDirectory(tempFolder);
                        }
                        var tempPath = Path.Combine(tempFolder, $"capture_{DateTime.UtcNow.Ticks}.png");
                        croppedBmp.Save(tempPath, ImageFormat.Png);
                        _logger.LogInformation("Cropped image saved to: {Path}", tempPath);

                        using (MemoryStream outMs = new MemoryStream())
                        {
                            croppedBmp.Save(outMs, ImageFormat.Png);
                            return Task.FromResult(outMs.ToArray());
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crop image.");
            throw;
        }
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

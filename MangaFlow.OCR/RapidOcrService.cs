using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;
using RapidOcrNet;
using SkiaSharp;

using OcrResultDto = MangaFlow.Application.DTOs.OcrResult;

namespace MangaFlow.OCR;

public class RapidOcrService : IOcrService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<RapidOcrService> _logger;
    private RapidOcr? _ocrEngine;
    private string? _loadedModelPath;
    private string? _loadedLanguage;
    private OcrDiagnostics? _diagnostics;
    private readonly object _lock = new();

    public RapidOcrService(ISettingsService settingsService, ILogger<RapidOcrService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<OcrResultDto> RecognizeTextAsync(byte[] imageBytes, string language)
    {
        return await RecognizeTextInternalAsync(imageBytes, language, isCapture: true);
    }

    public async Task<OcrResultDto> RecognizeTextAsync(string imagePath, string language)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image file not found", imagePath);
        }

        byte[] bytes = await File.ReadAllBytesAsync(imagePath);
        return await RecognizeTextInternalAsync(bytes, language, isCapture: false);
    }

    private static (float DpiX, float DpiY) GetImageDpi(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 30) return (96, 96);

        try
        {
            // Check PNG signature
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                int idx = 8;
                while (idx + 8 < bytes.Length)
                {
                    int len = (bytes[idx] << 24) | (bytes[idx + 1] << 16) | (bytes[idx + 2] << 8) | bytes[idx + 3];
                    if (idx + 4 + 4 > bytes.Length) break;
                    string chunkType = System.Text.Encoding.ASCII.GetString(bytes, idx + 4, 4);
                    if (chunkType == "pHYs" && idx + 8 + 9 <= bytes.Length)
                    {
                        int ppuX = (bytes[idx + 8] << 24) | (bytes[idx + 9] << 16) | (bytes[idx + 10] << 8) | bytes[idx + 11];
                        int ppuY = (bytes[idx + 12] << 24) | (bytes[idx + 13] << 16) | (bytes[idx + 14] << 8) | bytes[idx + 15];
                        int unit = bytes[idx + 16];
                        if (unit == 1)
                        {
                            float dpiX = (float)Math.Round(ppuX * 0.0254f);
                            float dpiY = (float)Math.Round(ppuY * 0.0254f);
                            if (dpiX > 0 && dpiY > 0)
                            {
                                return (dpiX, dpiY);
                            }
                        }
                    }
                    idx += 12 + len;
                }
            }
            // Check JPEG signature (JFIF)
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                int idx = 2;
                while (idx + 4 < bytes.Length)
                {
                    if (bytes[idx] == 0xFF)
                    {
                        byte marker = bytes[idx + 1];
                        if (marker == 0xD9 || marker == 0xDA) break; // End of image or start of scan

                        int len = (bytes[idx + 2] << 8) | bytes[idx + 3];
                        if (marker == 0xE0 && len >= 16) // APP0 / JFIF
                        {
                            if (bytes[idx + 4] == 'J' && bytes[idx + 5] == 'F' && bytes[idx + 6] == 'I' && bytes[idx + 7] == 'F' && bytes[idx + 8] == 0)
                            {
                                int units = bytes[idx + 11]; // 0 = no units, 1 = dpi, 2 = dpcm
                                int xdensity = (bytes[idx + 12] << 8) | bytes[idx + 13];
                                int ydensity = (bytes[idx + 14] << 8) | bytes[idx + 15];
                                if (units == 1)
                                {
                                    return (xdensity, ydensity);
                                }
                                else if (units == 2)
                                {
                                    return (xdensity * 2.54f, ydensity * 2.54f);
                                }
                            }
                        }
                        idx += 2 + len;
                    }
                    else
                    {
                        idx++;
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing error, fallback to 96
        }

        return (96, 96);
    }

    private async Task<OcrResultDto> RecognizeTextInternalAsync(byte[] imageBytes, string language, bool isCapture)
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings == null || settings.OcrEngine != "RapidOCR")
        {
            _logger.LogInformation("Using Stub OCR engine based on settings.");
            return CreateStubResult(language);
        }

        string modelPath = settings.OcrModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            _logger.LogWarning("OCR model directory is missing or empty.");
            LogToFile("OCR model directory is missing or empty.");
            return CreateModelNotInstalledResult();
        }

        if (!Directory.Exists(modelPath))
        {
            return CreateModelNotInstalledResult();
        }

        string detPath = FindFile(modelPath, "*det*.onnx");
        string clsPath = FindFile(modelPath, "*cls*.onnx");
        string recPath = FindFile(modelPath, "*latin*.onnx");
        string keysPath = FindFile(modelPath, "*latin*.txt");
        if (string.IsNullOrEmpty(keysPath))
        {
            keysPath = FindFile(modelPath, "*dict*.txt");
        }

        if (string.IsNullOrEmpty(detPath) || string.IsNullOrEmpty(recPath) || string.IsNullOrEmpty(keysPath))
        {
            _logger.LogWarning("Required OCR model files are missing in {ModelPath}", modelPath);
            LogToFile($"Required OCR model files are missing in: '{modelPath}'");
            return CreateModelNotInstalledResult();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            EnsureEngineInitialized(modelPath, settings.CpuThreads, "English");

            RapidOcrNet.OcrResult ocrResult;
            using (var ms = new MemoryStream(imageBytes))
            using (var originalBitmap = SKBitmap.Decode(ms))
            {
                if (originalBitmap == null)
                {
                    throw new InvalidOperationException("Failed to decode image bytes to SKBitmap");
                }

                int originalWidth = originalBitmap.Width;
                int originalHeight = originalBitmap.Height;

                // NO manual resize — let RapidOcrNet handle resizing internally via LimitSideLen.
                // Previously we had maxSide=1024 which caused DOUBLE resize (our code + RapidOcrNet),
                // resulting in text being too small for the detector to find, causing missing lines.
                SKBitmap processedBitmap = originalBitmap;

                try
                {
                    int processedWidth = processedBitmap.Width;
                    int processedHeight = processedBitmap.Height;

                    // Extract image DPI using our custom helper
                    var (originalDpiX, originalDpiY) = GetImageDpi(imageBytes);

                    string originalFormat = originalBitmap.ColorType.ToString();

                    // Logging details
                    if (isCapture)
                    {
                        _logger.LogInformation("--- OCR Diagnostic Log (Capture) ---");
                        _logger.LogInformation("Original Screenshot Crop details:");
                        _logger.LogInformation("- Dimensions: {Width}x{Height}", originalWidth, originalHeight);
                        _logger.LogInformation("- DPI: {DpiX}x{DpiY}", originalDpiX, originalDpiY);
                        _logger.LogInformation("- Pixel Format: {Format}", originalFormat);
                        _logger.LogInformation("- File Size: {Size} bytes", imageBytes.Length);
                        _logger.LogInformation("- Manual Resize: DISABLED (delegated to RapidOcrNet LimitSideLen)");
                        _logger.LogInformation("- Image Cropped Before OCR: True");
                    }
                    else
                    {
                        _logger.LogInformation("--- OCR Diagnostic Log (Playground) ---");
                        _logger.LogInformation("Original Playground Image details:");
                        _logger.LogInformation("- Dimensions: {Width}x{Height}", originalWidth, originalHeight);
                        _logger.LogInformation("- DPI: {DpiX}x{DpiY}", originalDpiX, originalDpiY);
                        _logger.LogInformation("- Pixel Format: {Format}", originalFormat);
                        _logger.LogInformation("- File Size: {Size} bytes", imageBytes.Length);
                        _logger.LogInformation("- Manual Resize: DISABLED (delegated to RapidOcrNet LimitSideLen)");
                        _logger.LogInformation("- Image Cropped Before OCR: False");
                    }

                    // Save the files to %LocalAppData%/MangaFlow/debug/
                    try
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string debugDir = Path.Combine(localAppData, "MangaFlow", "debug");
                        if (!Directory.Exists(debugDir))
                        {
                            Directory.CreateDirectory(debugDir);
                        }

                        // Save ocr_input.png as standard debug file
                        string debugPath = Path.Combine(debugDir, "ocr_input.png");
                        using (var image = SKImage.FromBitmap(processedBitmap))
                        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            using (var fs = new FileStream(debugPath, FileMode.Create, FileAccess.Write))
                            {
                                data.SaveTo(fs);
                            }
                        }

                        if (isCapture)
                        {
                            // Save capture_original.png
                            string captureOriginalPath = Path.Combine(debugDir, "capture_original.png");
                            await File.WriteAllBytesAsync(captureOriginalPath, imageBytes);
                            _logger.LogInformation("Saved debug file capture_original.png to {Path}", captureOriginalPath);

                            // Save capture_input.png
                            string captureInputPath = Path.Combine(debugDir, "capture_input.png");
                            using (var image = SKImage.FromBitmap(processedBitmap))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            {
                                using (var fs = new FileStream(captureInputPath, FileMode.Create, FileAccess.Write))
                                {
                                    data.SaveTo(fs);
                                }
                            }
                            _logger.LogInformation("Saved debug file capture_input.png to {Path}", captureInputPath);
                        }
                        else
                        {
                            // Save playground_input.png
                            string playgroundInputPath = Path.Combine(debugDir, "playground_input.png");
                            using (var image = SKImage.FromBitmap(processedBitmap))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            {
                                using (var fs = new FileStream(playgroundInputPath, FileMode.Create, FileAccess.Write))
                                {
                                    data.SaveTo(fs);
                                }
                            }
                            _logger.LogInformation("Saved debug file playground_input.png to {Path}", playgroundInputPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save debug images");
                    }

                    // Configure RapidOcrOptions:
                    // - LimitSideLen = 1536: Allow larger images for better text detection accuracy
                    // - BoxThresh = 0.3f: Lower threshold to detect more text regions (fewer missed lines)
                    var ocrOptions = new RapidOcrOptions
                    {
                        LimitSideLen = 1536,
                        BoxThresh = 0.3f
                    };
                    _logger.LogInformation("RapidOcr Options: LimitSideLen={LimitSideLen}, BoxThresh={BoxThresh}", 
                        ocrOptions.LimitSideLen, ocrOptions.BoxThresh);

                    lock (_lock)
                    {
                        if (_ocrEngine == null)
                        {
                            throw new InvalidOperationException("OCR engine is not initialized");
                        }
                        ocrResult = _ocrEngine.Detect(processedBitmap, ocrOptions);
                    }

                    int boxCount = ocrResult.TextBlocks?.Length ?? 0;
                    _logger.LogInformation("OCR Detection Info: Detected {Count} text boxes.", boxCount);
                    if (ocrResult.TextBlocks != null)
                    {
                        for (int i = 0; i < ocrResult.TextBlocks.Length; i++)
                        {
                            var block = ocrResult.TextBlocks[i];
                            float confidence = 0.9f;
                            if (block.CharScores != null && block.CharScores.Length > 0)
                            {
                                confidence = block.CharScores.Average();
                            }

                            string ptsStr = string.Empty;
                            if (block.BoxPoints != null)
                            {
                                ptsStr = string.Join(", ", block.BoxPoints.Select(p => $"({p.X}, {p.Y})"));
                            }
                            _logger.LogInformation("- Box #{Idx}: Points=[{Points}], Confidence={Conf:F4}, Text='{Text}'", i, ptsStr, confidence, block.Text);
                        }
                    }

                    try
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string debugDir = Path.Combine(localAppData, "MangaFlow", "debug");
                        string debugLayoutPath = Path.Combine(debugDir, "ocr_debug_layout.png");

                        var debugBitmap = new SKBitmap(processedBitmap.Info);
                        using (var canvas = new SKCanvas(debugBitmap))
                        {
                            canvas.DrawBitmap(processedBitmap, 0, 0);
                            using (var paint = new SKPaint())
                            {
                                paint.Style = SKPaintStyle.Stroke;
                                paint.Color = SKColors.Red;
                                paint.StrokeWidth = 2.0f;

                                if (ocrResult.TextBlocks != null)
                                {
                                    foreach (var block in ocrResult.TextBlocks)
                                    {
                                        if (block.BoxPoints != null && block.BoxPoints.Length >= 4)
                                        {
                                            using (var path = new SKPath())
                                            {
                                                path.MoveTo((float)block.BoxPoints[0].X, (float)block.BoxPoints[0].Y);
                                                path.LineTo((float)block.BoxPoints[1].X, (float)block.BoxPoints[1].Y);
                                                path.LineTo((float)block.BoxPoints[2].X, (float)block.BoxPoints[2].Y);
                                                path.LineTo((float)block.BoxPoints[3].X, (float)block.BoxPoints[3].Y);
                                                path.Close();
                                                canvas.DrawPath(path, paint);
                                            }
                                        }
                                    }
                                }
                            }

                            using (var image = SKImage.FromBitmap(debugBitmap))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            {
                                using (var fs = new FileStream(debugLayoutPath, FileMode.Create, FileAccess.Write))
                                {
                                    data.SaveTo(fs);
                                }
                            }
                        }
                        debugBitmap.Dispose();
                        _logger.LogInformation("Saved debug OCR layout image to {Path}", debugLayoutPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save debug OCR layout image with bounding boxes");
                    }
                }
                finally
                {
                    // No manual resize bitmap to dispose — RapidOcrNet handles resizing internally
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("OCR executed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            LogToFile($"OCR executed in {stopwatch.ElapsedMilliseconds}ms");

            var result = new OcrResultDto();
            if (ocrResult.TextBlocks != null)
            {
                foreach (var block in ocrResult.TextBlocks)
                {
                    float confidence = 0.9f;
                    if (block.CharScores != null && block.CharScores.Length > 0)
                    {
                        confidence = block.CharScores.Average();
                    }

                    var ocrLine = new OcrLine
                    {
                        Text = block.Text ?? string.Empty,
                        Confidence = confidence
                    };

                    if (block.BoxPoints != null && block.BoxPoints.Length >= 4)
                    {
                        double minX = double.MaxValue;
                        double minY = double.MaxValue;
                        double maxX = double.MinValue;
                        double maxY = double.MinValue;

                        foreach (var p in block.BoxPoints)
                        {
                            if (p.X < minX) minX = p.X;
                            if (p.Y < minY) minY = p.Y;
                            if (p.X > maxX) maxX = p.X;
                            if (p.Y > maxY) maxY = p.Y;
                        }

                        ocrLine.Box = new BoundingBox(minX, minY, maxX - minX, maxY - minY);
                    }

                    result.Lines.Add(ocrLine);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OCR execution failed");
            LogToFile($"OCR execution failed: {ex.Message}");
            return CreateErrorResult(ex.Message);
        }
    }

    public async Task<string?> ValidateAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings == null || settings.OcrEngine != "RapidOCR")
        {
            // Stub mode — always valid
            return null;
        }

        string modelPath = settings.OcrModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return "OCR model path is not configured. Go to Settings → OCR Model Path.";
        }

        if (!Directory.Exists(modelPath))
        {
            return $"OCR model directory not found: '{modelPath}'";
        }

        string detPath = FindFile(modelPath, "*det*.onnx");
        if (string.IsNullOrEmpty(detPath))
        {
            return "OCR detector model file (*det*.onnx) is missing.";
        }

        string recPath = FindFile(modelPath, "*latin*.onnx");
        string keysPath = FindFile(modelPath, "*latin*.txt");
        if (string.IsNullOrEmpty(keysPath))
        {
            keysPath = FindFile(modelPath, "*dict*.txt");
        }

        if (string.IsNullOrEmpty(recPath) || string.IsNullOrEmpty(keysPath))
        {
            return "English OCR model files are missing.";
        }

        _logger.LogInformation("OCR model validation passed. det={Det} rec={Rec} keys={Keys}", detPath, recPath, keysPath);
        return null;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (settings != null && settings.OcrEngine == "RapidOCR")
            {
                var validationError = await ValidateAsync();
                if (validationError != null)
                {
                    _logger.LogWarning("Skipping OCR pre-initialization: {Error}", validationError);
                    return;
                }

                _logger.LogInformation("Pre-initializing RapidOCR engine at startup...");
                EnsureEngineInitialized(settings.OcrModelPath, settings.CpuThreads, "English");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pre-initialize RapidOCR engine.");
        }
    }

    public OcrDiagnostics? GetDiagnostics()
    {
        lock (_lock)
        {
            return _diagnostics;
        }
    }

    private void EnsureEngineInitialized(string modelPath, int cpuThreads, string language)
    {
        lock (_lock)
        {
            if (_ocrEngine != null && _loadedModelPath == modelPath && _loadedLanguage == "English")
            {
                return;
            }

            if (_ocrEngine != null)
            {
                try
                {
                    _ocrEngine.Dispose();
                }
                catch { }
                _ocrEngine = null;
            }

            string detPath = FindFile(modelPath, "*det*.onnx");
            string clsPath = FindFile(modelPath, "*cls*.onnx");
            string recPath = FindFile(modelPath, "*latin*.onnx");
            string keysPath = FindFile(modelPath, "*latin*.txt");
            if (string.IsNullOrEmpty(keysPath))
            {
                keysPath = FindFile(modelPath, "*dict*.txt");
            }

            var ocr = new RapidOcr();
            ocr.InitModels(detPath, clsPath, recPath, keysPath, cpuThreads);

            _ocrEngine = ocr;
            _loadedModelPath = modelPath;
            _loadedLanguage = "English";

            _diagnostics = new OcrDiagnostics
            {
                DetectorPath = detPath,
                RecognizerPath = recPath,
                DictionaryPath = keysPath,
                Source = "Embedded",
                Language = "English"
            };

            var diagMsg = $"OCR Engine Initialized:\n\nDetector:\n{detPath}\n\nRecognizer:\n{recPath}\n\nDictionary:\n{keysPath}\n\nSource:\nEmbedded\n\nLanguage:\nEnglish";
            _logger.LogInformation("{DiagnosticsMessage}", diagMsg);
            LogToFile(diagMsg);
        }
    }

    private string FindFile(string directory, string searchPattern)
    {
        try
        {
            var files = Directory.GetFiles(directory, searchPattern);
            return files.FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void LogToFile(string message)
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MangaFlow", "activation.log");
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures
        }
    }

    private OcrResultDto CreateModelNotInstalledResult()
    {
        var result = new OcrResultDto();
        result.Lines.Add(new OcrLine
        {
            Text = "OCR model not installed.",
            Confidence = 0.0f,
            Box = new BoundingBox(0, 0, 0, 0)
        });
        return result;
    }

    private OcrResultDto CreateErrorResult(string message)
    {
        var result = new OcrResultDto();
        result.Lines.Add(new OcrLine
        {
            Text = $"OCR Error: {message}",
            Confidence = 0.0f,
            Box = new BoundingBox(0, 0, 0, 0)
        });
        return result;
    }

    private OcrResultDto CreateStubResult(string language)
    {
        var result = new OcrResultDto();
        result.Lines.Add(new OcrLine
        {
            Text = "This is mock OCR text.",
            Confidence = 0.95f,
            Box = new BoundingBox(10, 10, 200, 50)
        });
        return result;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_ocrEngine != null)
            {
                _ocrEngine.Dispose();
                _ocrEngine = null;
            }
        }
        GC.SuppressFinalize(this);
    }
}

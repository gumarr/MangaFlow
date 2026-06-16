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
    private readonly object _lock = new();

    public RapidOcrService(ISettingsService settingsService, ILogger<RapidOcrService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<OcrResultDto> RecognizeTextAsync(byte[] imageBytes, string language)
    {
        var settings = await _settingsService.GetSettingsAsync();
        if (settings == null || settings.OcrEngine != "RapidOCR")
        {
            _logger.LogInformation("Using Stub OCR engine based on settings.");
            return CreateStubResult(language);
        }

        string modelPath = settings.OcrModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !Directory.Exists(modelPath))
        {
            _logger.LogWarning("OCR model directory is missing or empty: {ModelPath}", modelPath);
            LogToFile($"OCR model directory is missing or empty: '{modelPath}'");
            return CreateModelNotInstalledResult();
        }

        string detPath = FindFile(modelPath, "*det*.onnx");
        string recPath = FindFile(modelPath, "*rec*.onnx");
        string keysPath = FindFile(modelPath, "*.txt");

        if (string.IsNullOrEmpty(detPath) || string.IsNullOrEmpty(recPath) || string.IsNullOrEmpty(keysPath))
        {
            _logger.LogWarning("Required OCR model files are missing in {ModelPath}", modelPath);
            LogToFile($"Required OCR model files are missing in: '{modelPath}'");
            return CreateModelNotInstalledResult();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            EnsureEngineInitialized(modelPath, settings.CpuThreads);

            RapidOcrNet.OcrResult ocrResult;
            using (var ms = new MemoryStream(imageBytes))
            using (var bitmap = SKBitmap.Decode(ms))
            {
                if (bitmap == null)
                {
                    throw new InvalidOperationException("Failed to decode image bytes to SKBitmap");
                }
                lock (_lock)
                {
                    if (_ocrEngine == null)
                    {
                        throw new InvalidOperationException("OCR engine is not initialized");
                    }
                    ocrResult = _ocrEngine.Detect(bitmap, new RapidOcrOptions());
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
                        Text = block.GetText() ?? string.Empty,
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

    public async Task<OcrResultDto> RecognizeTextAsync(string imagePath, string language)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image file not found", imagePath);
        }

        byte[] bytes = await File.ReadAllBytesAsync(imagePath);
        return await RecognizeTextAsync(bytes, language);
    }

    private void EnsureEngineInitialized(string modelPath, int cpuThreads)
    {
        lock (_lock)
        {
            if (_ocrEngine != null && _loadedModelPath == modelPath)
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

            _logger.LogInformation("Initializing RapidOCR engine with model path: {ModelPath}", modelPath);
            LogToFile($"Initializing RapidOCR engine with model path: '{modelPath}'");

            string detPath = FindFile(modelPath, "*det*.onnx");
            string recPath = FindFile(modelPath, "*rec*.onnx");
            string keysPath = FindFile(modelPath, "*.txt");
            string clsPath = FindFile(modelPath, "*cls*.onnx");

            var ocr = new RapidOcr();
            ocr.InitModels(detPath, clsPath, recPath, keysPath, cpuThreads);

            _ocrEngine = ocr;
            _loadedModelPath = modelPath;

            _logger.LogInformation("RapidOCR engine initialized successfully.");
            LogToFile("RapidOCR engine initialized successfully.");
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
            Text = language.Equals("Japanese", StringComparison.OrdinalIgnoreCase)
                ? "これはモックOCRテキストです。"
                : "This is mock OCR text.",
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

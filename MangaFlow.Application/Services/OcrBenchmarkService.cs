using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.Application.Services;

public class OcrBenchmarkService : IOcrBenchmarkService
{
    private readonly IOcrService _ocrService;
    private readonly ILogger<OcrBenchmarkService> _logger;

    public OcrBenchmarkService(IOcrService ocrService, ILogger<OcrBenchmarkService> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<BenchmarkResult> RunBenchmarkAsync(OcrProvider provider, string datasetFolder)
    {
        _logger.LogInformation("Starting OCR Benchmark for {Provider} on folder {Folder}", provider, datasetFolder);
        var totalStopwatch = Stopwatch.StartNew();

        long startMemory = GC.GetTotalMemory(true);

        var result = new BenchmarkResult
        {
            ModelName = "PP-OCRv5 English",
            Provider = provider.ToString(),
        };

        if (!Directory.Exists(datasetFolder))
        {
            throw new DirectoryNotFoundException($"Dataset folder not found: {datasetFolder}");
        }

        // Get all png and jpg files
        var imageFiles = Directory.GetFiles(datasetFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (imageFiles.Count == 0)
        {
            throw new FileNotFoundException($"No image files found in dataset folder: {datasetFolder}");
        }

        result.TotalSamples = imageFiles.Count;

        double totalCharAcc = 0;
        double totalWordAcc = 0;
        double totalCer = 0;
        double totalWer = 0;
        double totalInferenceTime = 0;

        foreach (var imagePath in imageFiles)
        {
            var txtPath = Path.ChangeExtension(imagePath, ".txt");
            string groundTruth = string.Empty;
            if (File.Exists(txtPath))
            {
                groundTruth = await File.ReadAllTextAsync(txtPath);
            }
            else
            {
                _logger.LogWarning("Ground truth file not found for {Image}", imagePath);
            }

            var sampleResult = new BenchmarkSampleResult
            {
                SampleName = Path.GetFileName(imagePath),
                GroundTruth = groundTruth.Trim()
            };

            var sampleStopwatch = Stopwatch.StartNew();
            try
            {
                // Run OCR with "English"
                var ocrResult = await _ocrService.RecognizeTextAsync(imagePath, "English");
                sampleStopwatch.Stop();

                sampleResult.OcrText = ocrResult?.FullText?.Trim() ?? string.Empty;
                sampleResult.InferenceTimeMs = sampleStopwatch.ElapsedMilliseconds;
                sampleResult.IsSuccess = true;

                // Calculate metrics
                var (charAcc, wordAcc, cer, wer) = CalculateMetrics(sampleResult.GroundTruth, sampleResult.OcrText);
                sampleResult.CharacterAccuracy = charAcc;
                sampleResult.WordAccuracy = wordAcc;
                sampleResult.Cer = cer;
                sampleResult.Wer = wer;

                totalCharAcc += charAcc;
                totalWordAcc += wordAcc;
                totalCer += cer;
                totalWer += wer;
                totalInferenceTime += sampleResult.InferenceTimeMs;
            }
            catch (Exception ex)
            {
                sampleStopwatch.Stop();
                sampleResult.IsSuccess = false;
                sampleResult.ErrorMessage = ex.Message;
                sampleResult.InferenceTimeMs = sampleStopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "Failed benchmarking sample: {Sample}", sampleResult.SampleName);
            }

            result.SampleResults.Add(sampleResult);
        }

        totalStopwatch.Stop();
        long endMemory = GC.GetTotalMemory(true);

        result.TotalDurationMs = totalStopwatch.ElapsedMilliseconds;
        result.MemoryUsageDeltaMb = Math.Max(0, (endMemory - startMemory) / (1024.0 * 1024.0));
        result.CPUUsageEstimate = $"{Environment.ProcessorCount} Cores Available";

        int successCount = result.SampleResults.Count(s => s.IsSuccess);
        if (successCount > 0)
        {
            result.AverageCharacterAccuracy = totalCharAcc / successCount;
            result.AverageWordAccuracy = totalWordAcc / successCount;
            result.AverageCer = totalCer / successCount;
            result.AverageWer = totalWer / successCount;
            result.AverageInferenceTimeMs = totalInferenceTime / successCount;
        }

        // Sort worst character accuracy first for failure analysis
        result.SampleResults = result.SampleResults
            .OrderBy(s => s.CharacterAccuracy)
            .ToList();

        // Write reports
        await WriteReportsAsync(result);

        return result;
    }

    private (double CharAcc, double WordAcc, double Cer, double Wer) CalculateMetrics(string gt, string ocr)
    {
        gt = gt?.Trim() ?? string.Empty;
        ocr = ocr?.Trim() ?? string.Empty;

        // Character level
        int dist = Levenshtein.Calculate(gt, ocr);
        int maxLen = Math.Max(gt.Length, ocr.Length);
        double charAcc = maxLen == 0 ? 1.0 : (1.0 - (double)dist / maxLen);
        double cer = gt.Length == 0 ? (ocr.Length == 0 ? 0.0 : 1.0) : (double)dist / gt.Length;

        // Word level (English split by whitespace)
        var gtWords = gt.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var ocrWords = ocr.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        int wordDist = Levenshtein.Calculate(gtWords, ocrWords);
        int maxWordLen = Math.Max(gtWords.Length, ocrWords.Length);
        double wordAcc = maxWordLen == 0 ? 1.0 : (1.0 - (double)wordDist / maxWordLen);
        double wer = gtWords.Length == 0 ? (ocrWords.Length == 0 ? 0.0 : 1.0) : (double)wordDist / gtWords.Length;

        return (charAcc, wordAcc, cer, wer);
    }

    private async Task WriteReportsAsync(BenchmarkResult result)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string reportsDir = Path.Combine(baseDir, "BenchmarkReports");
            if (!Directory.Exists(reportsDir))
            {
                Directory.CreateDirectory(reportsDir);
            }

            string timestamp = DateTime.UtcNow.ToString("yyyy_MM_dd_HHmmss");
            string jsonName = $"benchmark_{timestamp}_ppocrv5.json";
            string mdName = $"benchmark_{timestamp}_ppocrv5.md";

            string jsonPath = Path.Combine(reportsDir, jsonName);
            string mdPath = Path.Combine(reportsDir, mdName);

            // Save JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonContent = JsonSerializer.Serialize(result, options);
            await File.WriteAllTextAsync(jsonPath, jsonContent);

            // Save Markdown
            var sb = new StringBuilder();
            sb.AppendLine("# OCR Benchmark Report");
            sb.AppendLine($"**Date**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Model**: {result.ModelName}");
            sb.AppendLine($"**Provider**: {result.Provider}");
            sb.AppendLine();
            sb.AppendLine("## Summary Metrics");
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"| --- | --- |");
            sb.AppendLine($"| Total Samples | {result.TotalSamples} |");
            sb.AppendLine($"| Avg Character Accuracy | {result.AverageCharacterAccuracy:P2} |");
            sb.AppendLine($"| Avg Word Accuracy | {result.AverageWordAccuracy:P2} |");
            sb.AppendLine($"| Avg CER | {result.AverageCer:F4} |");
            sb.AppendLine($"| Avg WER | {result.AverageWer:F4} |");
            sb.AppendLine($"| Avg Inference Time | {result.AverageInferenceTimeMs:F1} ms |");
            sb.AppendLine($"| Total Duration | {result.TotalDurationMs} ms |");
            sb.AppendLine($"| Memory Usage Delta | {result.MemoryUsageDeltaMb:F2} MB |");
            sb.AppendLine($"| CPU Usage Estimate | {result.CPUUsageEstimate} |");
            sb.AppendLine();
            sb.AppendLine("## Failure Analysis (Sorted: Worst Accuracy First)");
            sb.AppendLine();
            sb.AppendLine("| Sample Name | Char Acc | Word Acc | CER | WER | Inference Time | Success |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var sample in result.SampleResults)
            {
                sb.AppendLine($"| {sample.SampleName} | {sample.CharacterAccuracy:P2} | {sample.WordAccuracy:P2} | {sample.Cer:F4} | {sample.Wer:F4} | {sample.InferenceTimeMs} ms | {sample.IsSuccess} |");
            }
            sb.AppendLine();
            sb.AppendLine("### Sample Details");
            foreach (var sample in result.SampleResults)
            {
                sb.AppendLine($"#### {sample.SampleName}");
                sb.AppendLine($"- **Character Accuracy**: {sample.CharacterAccuracy:P2}");
                sb.AppendLine($"- **Word Accuracy**: {sample.WordAccuracy:P2}");
                sb.AppendLine($"- **CER**: {sample.Cer:F4} | **WER**: {sample.Wer:F4}");
                sb.AppendLine($"- **Ground Truth**:");
                sb.AppendLine($"  ```text\n  {sample.GroundTruth}\n  ```");
                sb.AppendLine($"- **OCR Output**:");
                sb.AppendLine($"  ```text\n  {sample.OcrText}\n  ```");
                if (!sample.IsSuccess)
                {
                    sb.AppendLine($"- **Error**: {sample.ErrorMessage}");
                }
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(mdPath, sb.ToString());
            result.ReportsPath = mdPath;
            _logger.LogInformation("Saved benchmark reports: \nJSON: {Json}\nMD: {Md}", jsonPath, mdPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write benchmark reports");
        }
    }
}

internal static class Levenshtein
{
    public static int Calculate(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    public static int Calculate(string[] s, string[] t)
    {
        if (s == null || s.Length == 0) return t == null ? 0 : t.Length;
        if (t == null || t.Length == 0) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = string.Equals(t[j - 1], s[i - 1], StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}

using System;
using System.Collections.Generic;

namespace MangaFlow.Application.DTOs;

public class BenchmarkResult
{
    public string ModelName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int TotalSamples { get; set; }
    public double AverageCharacterAccuracy { get; set; }
    public double AverageWordAccuracy { get; set; }
    public double AverageCer { get; set; }
    public double AverageWer { get; set; }
    public double AverageInferenceTimeMs { get; set; }
    public long TotalDurationMs { get; set; }
    public double MemoryUsageDeltaMb { get; set; }
    public string CPUUsageEstimate { get; set; } = string.Empty;
    public string ReportsPath { get; set; } = string.Empty;
    public List<BenchmarkSampleResult> SampleResults { get; set; } = new();
}

public class BenchmarkSampleResult
{
    public string SampleName { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string OcrText { get; set; } = string.Empty;
    public double CharacterAccuracy { get; set; }
    public double WordAccuracy { get; set; }
    public double Cer { get; set; }
    public double Wer { get; set; }
    public long InferenceTimeMs { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

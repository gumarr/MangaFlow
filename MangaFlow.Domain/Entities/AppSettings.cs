using System;

namespace MangaFlow.Domain.Entities;

public class AppSettings
{
    public Guid Id { get; set; }
    public string OcrLanguage { get; set; } = "Japanese";
    public string OcrEngine { get; set; } = "RapidOCR";
    public string OcrModelPath { get; set; } = string.Empty;
    public string SelectedLlmModel { get; set; } = "Qwen 3 8B GGUF";
    public string LlmModelPath { get; set; } = string.Empty;
    public int CpuThreads { get; set; } = 4;
    public double Temperature { get; set; } = 0.3;
    public bool UseGpu { get; set; } = true;
    public string GlobalHotkey { get; set; } = "Alt + Q";
    public string DefaultSourceLanguage { get; set; } = "Japanese";
    public string DefaultTargetLanguage { get; set; } = "English";
    public bool ShowCapturePreview { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

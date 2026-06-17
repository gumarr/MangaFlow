using System;

namespace MangaFlow.Domain.Entities;

public class AppSettings
{
    public Guid Id { get; set; }
    public string OcrLanguage { get; set; } = "English";
    public string OcrEngine { get; set; } = "RapidOCR";
    public string OcrModelPath { get; set; } = string.Empty;
    public string SelectedLlmModel { get; set; } = "Qwen 3 8B GGUF";
    public string LlmModelPath { get; set; } = string.Empty;
    public int CpuThreads { get; set; } = 8;   // physical core count on typical 8-core CPUs
    public double Temperature { get; set; } = 0.3;
    public bool UseGpu { get; set; } = false;   // CPU backend by default (iGPU offload not wired)
    public int GpuLayerCount { get; set; } = 99; // layers to offload to GPU when UseGpu (99 = all)
    public int ContextSize { get; set; } = 2048; // KV-cache context window; manga bubbles are short
    public string GlobalHotkey { get; set; } = "Alt + Q";
    public string DefaultSourceLanguage { get; set; } = "English";
    public string DefaultTargetLanguage { get; set; } = "Vietnamese";
    public bool ShowCapturePreview { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

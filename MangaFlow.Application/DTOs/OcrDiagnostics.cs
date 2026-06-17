namespace MangaFlow.Application.DTOs;

public class OcrDiagnostics
{
    public string DetectorPath { get; set; } = string.Empty;
    public string RecognizerPath { get; set; } = string.Empty;
    public string DictionaryPath { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // "Embedded" or "Downloaded"
    public string Language { get; set; } = string.Empty;
}

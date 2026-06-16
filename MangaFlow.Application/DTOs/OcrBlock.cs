namespace MangaFlow.Application.DTOs;

public class OcrBlock
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBox Box { get; set; } = new(0, 0, 0, 0);
}

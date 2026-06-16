using System.Collections.Generic;

namespace MangaFlow.Application.DTOs;

public class OcrLine
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBox Box { get; set; } = new(0, 0, 0, 0);
    public List<OcrWord> Words { get; set; } = new();
}

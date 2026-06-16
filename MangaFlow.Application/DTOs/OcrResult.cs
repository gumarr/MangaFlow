using System.Collections.Generic;
using System.Text;

namespace MangaFlow.Application.DTOs;

public class OcrResult
{
    public List<OcrBlock> Blocks { get; set; } = new();
    public List<OcrLine> Lines { get; set; } = new();

    public string FullText
    {
        get
        {
            if (Lines.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var line in Lines)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }
                    sb.Append(line.Text);
                }
                return sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                foreach (var block in Blocks)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }
                    sb.Append(block.Text);
                }
                return sb.ToString();
            }
        }
    }
}

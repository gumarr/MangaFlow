using System.Collections.Generic;
using System.Text;

namespace MangaFlow.Application.DTOs;

public class OcrResult
{
    public List<OcrBlock> Blocks { get; set; } = new();

    public string FullText
    {
        get
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

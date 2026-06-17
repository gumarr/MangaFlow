using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MangaFlow.Application.DTOs;

public class OcrResult
{
    public List<OcrBlock> Blocks { get; set; } = new();
    public List<OcrLine> Lines { get; set; } = new();

    /// <summary>
    /// Raw OCR output with each detected line on its own line (\n separated).
    /// Useful for diagnostics / display where the original line layout matters.
    /// </summary>
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

    /// <summary>
    /// Text reflowed for translation: manga speech bubbles wrap lines purely to fit the
    /// bubble width, so a hard line break between "YOU'RE ALL" and "SO NICE" is NOT a
    /// sentence break. This joins wrapped lines with a space and only starts a new line
    /// when the previous line ends with sentence-ending punctuation, producing coherent
    /// sentences for the LLM to translate.
    /// </summary>
    public string MergedText => MergeLines(
        Lines.Count > 0 ? Lines.Select(l => l.Text) : Blocks.Select(b => b.Text));

    private static string MergeLines(IEnumerable<string> rawLines)
    {
        var lines = rawLines
            .Select(l => l?.Trim() ?? string.Empty)
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append(lines[i]);

            if (i == lines.Count - 1) break;

            // Decide the separator to the NEXT line.
            char lastChar = lines[i][^1];

            if (lastChar is '.' or '!' or '?' or '…' or ':' or '"' or '”' or '。' or '！' or '？')
            {
                // Sentence boundary — keep them on separate lines for readability.
                sb.Append('\n');
            }
            else if (lastChar == '-')
            {
                // Hyphenated word split across lines: glue directly (drop the hyphen).
                sb.Length -= 1;
            }
            else
            {
                // Mid-sentence wrap — join with a space.
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }
}

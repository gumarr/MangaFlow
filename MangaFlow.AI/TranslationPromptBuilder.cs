using System.Text;
using MangaFlow.Application.DTOs;
using MangaFlow.Domain.Entities;

namespace MangaFlow.AI;

public static class TranslationPromptBuilder
{
    public const string SystemPrompt =
        "You are an expert English-to-Vietnamese translator specializing in manga and webtoons.\n" +
        "Rules:\n" +
        "- Keep character names and proper nouns unchanged.\n" +
        "- Keep honorifics when contextually appropriate.\n" +
        "- Use glossary terms exactly as provided.\n" +
        "- Use recent conversation context to maintain consistency.\n" +
        "- Return the translation only — no explanations, notes, or commentary.\n" +
        "- Preserve line breaks from the source text.\n" +
        "- Maintain the dialogue tone (casual, formal, action, emotional).";

    public static string BuildUserPrompt(string text, TranslationContext context)
    {
        var sb = new StringBuilder();

        if (context.GlossaryTerms?.Count > 0)
        {
            sb.AppendLine("Glossary (use these exact translations):");
            foreach (var term in context.GlossaryTerms)
            {
                sb.AppendLine($"  {term.SourceText} => {term.TargetText}");
            }
            sb.AppendLine();
        }

        var recentBubbles = context.RecentBubbles;
        if (recentBubbles?.Count > 0)
        {
            sb.AppendLine("Recent dialogue context:");
            foreach (var bubble in recentBubbles)
            {
                sb.AppendLine($"  EN: {bubble.OcrText}");
                sb.AppendLine($"  VI: {bubble.TranslatedText}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Translate the following English text to Vietnamese:");
        sb.AppendLine(text);

        return sb.ToString().TrimEnd();
    }
}

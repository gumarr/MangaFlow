using System.Text;
using MangaFlow.Application.DTOs;
using MangaFlow.Domain.Entities;

namespace MangaFlow.AI;

public static class TranslationPromptBuilder
{
    // /no_think (at the very top) disables Qwen3 extended thinking — critical for speed,
    // it cuts the reasoning pass that was burning most of the inference time.
    public const string SystemPrompt =
        "/no_think\n" +
        "You are a professional manga translator. Translate English manga dialogue into natural, " +
        "fluent Vietnamese — the way a Vietnamese person actually speaks, not a literal word-for-word translation.\n" +
        "Rules:\n" +
        "- Output ONLY the Vietnamese translation. No English, no notes, no quotes around the whole line.\n" +
        "- Write natural Vietnamese sentence case. Do NOT shout in ALL CAPS even if the source is uppercase.\n" +
        "- Keep character names and proper nouns (people, places, titles) unchanged.\n" +
        "- Use the right Vietnamese pronouns for the situation (tôi/tớ/cậu/anh/em/cô…). Pick what sounds natural.\n" +
        "- Apply any glossary terms exactly as given.\n" +
        "- Match the tone: casual, emotional, formal, or action as appropriate.";

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

        sb.AppendLine("Translate this English manga line to natural Vietnamese:");
        sb.Append(text);

        return sb.ToString().TrimEnd();
    }
}

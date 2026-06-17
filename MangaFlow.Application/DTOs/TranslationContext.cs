using System.Collections.Generic;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Application.DTOs;

public class TranslationContext
{
    public List<Bubble> RecentBubbles { get; set; } = new();
    public List<GlossaryTerm> GlossaryTerms { get; set; } = new();
}

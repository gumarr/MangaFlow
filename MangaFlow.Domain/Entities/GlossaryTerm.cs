using System;

namespace MangaFlow.Domain.Entities;

public class GlossaryTerm
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; } // Null if it's a global term
    public string SourceText { get; set; } = string.Empty;
    public string TargetText { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

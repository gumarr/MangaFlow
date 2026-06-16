using Microsoft.EntityFrameworkCore;
using MangaFlow.Domain.Entities;

namespace MangaFlow.Persistence;

public class MangaFlowDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();
    public DbSet<TranslationHistoryItem> TranslationHistoryItems => Set<TranslationHistoryItem>();
    public DbSet<TranslationMemoryEntry> TranslationMemoryEntries => Set<TranslationMemoryEntry>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    public MangaFlowDbContext(DbContextOptions<MangaFlowDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entities
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SourceLanguage).HasMaxLength(50);
            entity.Property(e => e.TargetLanguage).HasMaxLength(50);
        });

        modelBuilder.Entity<GlossaryTerm>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceText).IsRequired().HasMaxLength(500);
            entity.Property(e => e.TargetText).IsRequired().HasMaxLength(500);
            
            entity.HasIndex(e => e.ProjectId);
        });

        modelBuilder.Entity<TranslationHistoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceText).IsRequired();
            entity.Property(e => e.TranslatedText).IsRequired();
            
            entity.HasIndex(e => e.ProjectId);
        });

        modelBuilder.Entity<TranslationMemoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceText).IsRequired();
            entity.Property(e => e.TranslatedText).IsRequired();
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.SourceText);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}

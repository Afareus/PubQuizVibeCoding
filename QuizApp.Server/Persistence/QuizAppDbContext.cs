using Microsoft.EntityFrameworkCore;
using QuizApp.Server.Domain.Entities;

namespace QuizApp.Server.Persistence;

public sealed class QuizAppDbContext : DbContext
{
    public QuizAppDbContext(DbContextOptions<QuizAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Quiz> Quizzes => Set<Quiz>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    public DbSet<QuizSession> Sessions => Set<QuizSession>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamAnswer> TeamAnswers => Set<TeamAnswer>();

    public DbSet<SessionResult> SessionResults => Set<SessionResult>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.HasKey(x => x.QuizId);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.DeletePasswordHash).IsRequired().HasMaxLength(512);
            entity.Property(x => x.QuizOrganizerTokenHash).IsRequired().HasMaxLength(512);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.IsDeleted).IsRequired();

            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.HasMany(x => x.Questions)
                .WithOne(x => x.Quiz)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Sessions)
                .WithOne(x => x.Quiz)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.AuditLogs)
                .WithOne(x => x.Quiz)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(x => x.QuestionId);
            entity.Property(x => x.OrderIndex).IsRequired();
            entity.Property(x => x.Text).IsRequired().HasMaxLength(1500);
            entity.Property(x => x.TimeLimitSec).IsRequired();
            entity.Property(x => x.QuestionType).IsRequired();
            entity.Property(x => x.CorrectOption);
            entity.Property(x => x.CorrectNumericValue).HasPrecision(18, 4);

            entity.HasIndex(x => new { x.QuizId, x.OrderIndex })
                .IsUnique();

            entity.HasMany(x => x.Options)
                .WithOne(x => x.Question)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(x => x.QuestionOptionId);
            entity.Property(x => x.OptionKey).IsRequired();
            entity.Property(x => x.Text).IsRequired().HasMaxLength(500);

            entity.HasIndex(x => new { x.QuestionId, x.OptionKey })
                .IsUnique();
        });

        modelBuilder.Entity<QuizSession>(entity =>
        {
            entity.HasKey(x => x.SessionId);
            entity.Property(x => x.JoinCode).IsRequired().HasMaxLength(16);
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.ConcurrencyToken).IsRequired().HasMaxLength(32).IsConcurrencyToken();

            entity.HasIndex(x => x.JoinCode)
                .IsUnique();

            entity.HasMany(x => x.Teams)
                .WithOne(x => x.Session)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Answers)
                .WithOne(x => x.Session)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Results)
                .WithOne(x => x.Session)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.AuditLogs)
                .WithOne(x => x.Session)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(x => x.TeamId);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(120);
            entity.Property(x => x.NormalizedTeamName).IsRequired().HasMaxLength(120);
            entity.Property(x => x.JoinedAtUtc).IsRequired();
            entity.Property(x => x.LastSeenAtUtc).IsRequired();
            entity.Property(x => x.TeamReconnectTokenHash).IsRequired().HasMaxLength(512);
            entity.Property(x => x.Status).IsRequired();

            entity.HasIndex(x => new { x.SessionId, x.NormalizedTeamName })
                .IsUnique();
        });

        modelBuilder.Entity<TeamAnswer>(entity =>
        {
            entity.HasKey(x => x.TeamAnswerId);
            entity.Property(x => x.SelectedOption);
            entity.Property(x => x.NumericValue).HasPrecision(18, 4);
            entity.Property(x => x.SubmittedAtUtc).IsRequired();
            entity.Property(x => x.IsCorrect).IsRequired();
            entity.Property(x => x.ResponseTimeMs).IsRequired();

            entity.HasIndex(x => new { x.SessionId, x.TeamId, x.QuestionId })
                .IsUnique();

            entity.HasOne(x => x.Team)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionResult>(entity =>
        {
            entity.HasKey(x => x.SessionResultId);
            entity.Property(x => x.Score).IsRequired();
            entity.Property(x => x.CorrectCount).IsRequired();
            entity.Property(x => x.TotalCorrectResponseTimeMs).IsRequired();
            entity.Property(x => x.Rank).IsRequired();

            entity.HasIndex(x => x.TeamId)
                .IsUnique();

            entity.HasOne(x => x.Team)
                .WithOne(x => x.SessionResult)
                .HasForeignKey<SessionResult>(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.AuditLogId);
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.Property(x => x.ActionType).IsRequired().HasMaxLength(64);
            entity.Property(x => x.PayloadJson).IsRequired();

            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.ActionType);
        });
    }
}

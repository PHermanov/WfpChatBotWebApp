using Microsoft.EntityFrameworkCore;
using WfpChatBotWebApp.Persistence.Entities;

namespace WfpChatBotWebApp.Persistence;

public partial class AppDbContext : DbContext
{
    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<TextMessage> TextMessages { get; set; }
    public virtual DbSet<Result> Results { get; set; }
    public virtual DbSet<BotUser> BotUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<TextMessage>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("PRIMARY");
            entity.ToTable("textmessages");
            entity.HasIndex(e => e.Name, "name_UNIQUE").IsUnique();
            entity.Property(e => e.Name)
                .HasMaxLength(45)
                .HasColumnName("name");
            entity.Property(e => e.Text)
                .HasColumnType("text")
                .HasColumnName("text");
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.HasKey(r => r.Id).HasName("PRIMARY");
            entity.ToTable("results");
            entity.Property(e => e.ChatId).HasColumnName("chatid");
            entity.Property(e => e.PlayedAt).HasColumnName("playedat");
            entity.Property(e => e.UserId).HasColumnName("userid");
        });

        modelBuilder.Entity<BotUser>(entity =>
        {
            entity.HasKey(r => r.Id).HasName("PRIMARY");
            entity.ToTable("users");
            entity.Property(e => e.ChatId).HasColumnName("chatid");
            entity.Property(e => e.Inactive).HasColumnName("inactive");
            entity.Property(e => e.UserId).HasColumnName("userid");
            entity.Property(e => e.UserName)
                .HasColumnType("text")
                .HasColumnName("username");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}


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
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Sticker> Stickers { get; set; }
    public virtual DbSet<ReplyMessage> ReplyMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReplyMessage>(entity =>
        {
            entity.HasKey(e => e.MessageKey)
                .HasName("PK__ReplyMes__E03734E19FB4DF73")
                .IsClustered(false);

            entity.HasIndex(e => e.MessageKey, "UQ__ReplyMes__E03734E05CAF3063")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.MessageKey).HasMaxLength(15);
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity
                .HasKey(e => e.Id)
                .HasName("PK__Results__3214EC07BF532091");
        });

        modelBuilder.Entity<Sticker>(entity =>
        {
            entity.HasKey(e => e.Name)
                .HasName("PK__Stickers__737584F653C9340A")
                .IsClustered(false);

            entity.HasIndex(e => e.Name, "UQ__Stickers__737584F7FFBBA1FD")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Name).HasMaxLength(45);
            entity.Property(e => e.StickerSet).HasMaxLength(10);
            entity.Property(e => e.Url).HasMaxLength(200);
        });

        modelBuilder.Entity<TextMessage>(entity =>
        {
            entity.HasKey(e => e.Name)
                .HasName("PK__TextMess__737584F68FABF3D1")
                .IsClustered(false);

            entity.HasIndex(e => e.Name, "UQ__TextMess__737584F72B75AE3E")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Name).HasMaxLength(45);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC07C6324134");

            entity.Property(e => e.Inactive)
                .IsRequired()
                .HasDefaultValueSql("('0')");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
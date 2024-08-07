﻿using Microsoft.EntityFrameworkCore;
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
    public virtual DbSet<StickerEntity> Stickers { get; set; }
    public virtual DbSet<ReplyMessage> ReplyMessages { get; set; }
    
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
            entity.Property(e => e.PlayedAt).HasColumnName("playdate");
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

        modelBuilder.Entity<StickerEntity>(entity =>
        {
            entity.HasKey(r => r.Name).HasName("PRIMARY");
            entity.ToTable("stickers");
            entity.HasIndex(e => e.Name, "name_UNIQUE").IsUnique();
            entity.Property(e => e.Name)
                .HasMaxLength(45)
                .HasColumnName("name");
            entity.Property(e => e.Set)
                .HasColumnType("text")
                .HasMaxLength(10)
                .HasColumnName("sticker_set");
            entity.Property(e => e.Url)
                .HasColumnType("text")
                .HasMaxLength(200)
                .HasColumnName("url");
        });

        modelBuilder.Entity<ReplyMessage>(entity =>
        {
            entity.HasKey(r => r.Key).HasName("PRIMARY");
            entity.ToTable("replymessages");
            entity.HasIndex(e => e.Key, "key_UNIQUE").IsUnique();
            entity.Property(e => e.Key)
                .HasMaxLength(15)
                .HasColumnName("key");
            entity.Property(e => e.Value)
                .HasColumnType("text")
                .HasColumnName("value");
        });
        
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}


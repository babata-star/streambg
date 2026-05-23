using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;

namespace StreamBG.Infrastructure.Data;

public static class VodModelConfig
{
    public static void ConfigureVod(ModelBuilder builder)
    {
        builder.Entity<VodVideo>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Title).HasMaxLength(140).IsRequired();
            e.HasIndex(v => v.UserId);
            e.HasIndex(v => v.Status);
            e.HasIndex(v => v.CreatedAt);
            e.HasOne(v => v.User)
             .WithMany()
             .HasForeignKey(v => v.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VodChapter>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.VodVideo)
             .WithMany(v => v.Chapters)
             .HasForeignKey(c => c.VodVideoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VodComment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Content).HasMaxLength(1000);
            e.HasOne(c => c.VodVideo)
             .WithMany(v => v.Comments)
             .HasForeignKey(c => c.VodVideoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WatchProgress>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => new { w.UserId, w.VodVideoId }).IsUnique();
        });
    }
}

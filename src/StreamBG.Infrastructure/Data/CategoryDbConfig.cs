using Microsoft.EntityFrameworkCore;
using StreamBG.Core.Entities;

namespace StreamBG.Infrastructure.Data;

public static class CategoryDbConfig
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(60).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(60).IsRequired();
            e.Property(c => c.Color).HasMaxLength(20).HasDefaultValue("#6441a5");
            e.HasIndex(c => c.Slug).IsUnique();
        });

        builder.Entity<Category>().HasData(
            new Category { Id = 1,  Name = "Игри",        Slug = "igri",         Icon = "🎮", Color = "#6441a5", SortOrder = 1 },
            new Category { Id = 2,  Name = "Музика",      Slug = "muzika",       Icon = "🎵", Color = "#e91e63", SortOrder = 2 },
            new Category { Id = 3,  Name = "Изкуство",    Slug = "izkustvo",     Icon = "🎨", Color = "#ff5722", SortOrder = 3 },
            new Category { Id = 4,  Name = "Спорт",       Slug = "sport",        Icon = "⚽", Color = "#4caf50", SortOrder = 4 },
            new Category { Id = 5,  Name = "Образование", Slug = "obrazovanie",  Icon = "📚", Color = "#2196f3", SortOrder = 5 },
            new Category { Id = 6,  Name = "Технологии",  Slug = "tehnologii",   Icon = "💻", Color = "#00bcd4", SortOrder = 6 },
            new Category { Id = 7,  Name = "Готвене",     Slug = "gotvene",      Icon = "🍳", Color = "#ff9800", SortOrder = 7 },
            new Category { Id = 8,  Name = "Пътешествия", Slug = "puteshestvia", Icon = "✈️", Color = "#795548", SortOrder = 8 },
            new Category { Id = 9,  Name = "Забавление",  Slug = "zabavlenie",   Icon = "🎭", Color = "#9c27b0", SortOrder = 9 },
            new Category { Id = 10, Name = "Друго",       Slug = "drugo",        Icon = "🔮", Color = "#607d8b", SortOrder = 10 }
        );
    }
}

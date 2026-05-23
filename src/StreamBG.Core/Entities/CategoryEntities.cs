namespace StreamBG.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = "#6441a5";
    public int SortOrder { get; set; }
    public ICollection<Stream> Streams { get; set; } = new List<Stream>();
}

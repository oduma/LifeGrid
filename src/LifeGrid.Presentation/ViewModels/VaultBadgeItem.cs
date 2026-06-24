namespace LifeGrid.Presentation.ViewModels;

public sealed class VaultBadgeItem
{
    public string   IconGlyph   { get; init; } = string.Empty;
    public string   Title       { get; init; } = string.Empty;
    public string   Description { get; init; } = string.Empty;
    public DateTime DateEarned  { get; init; }
    public Color    TierColor   { get; init; } = Colors.White;
}

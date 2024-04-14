namespace AniOrder.Core;

public class Media
{
    public readonly struct Titles
    {
        public string? Romaji { get; init; }
        public string? Native { get; init; }
        public string? English { get; init; }
    }

    public Titles Title { get; init; }
    public DateTime ReleaseDate { get; init; }
}
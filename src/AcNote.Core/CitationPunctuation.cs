namespace AcNote.Core;

/// <summary>Null follows language defaults; an empty string deliberately removes spacing/punctuation.</summary>
public sealed class CitationPunctuation
{
    public string? AuthorYearSeparator { get; set; }
    public string? NarrativeSeparator { get; set; }
    public string? OpenBracket { get; set; }
    public string? CloseBracket { get; set; }
    public string? EntrySeparator { get; set; }

    public void Merge(CitationPunctuation other)
    {
        AuthorYearSeparator = other.AuthorYearSeparator ?? AuthorYearSeparator;
        NarrativeSeparator = other.NarrativeSeparator ?? NarrativeSeparator;
        OpenBracket = other.OpenBracket ?? OpenBracket;
        CloseBracket = other.CloseBracket ?? CloseBracket;
        EntrySeparator = other.EntrySeparator ?? EntrySeparator;
    }
}

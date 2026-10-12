namespace AcNote.Core;

public static class TemplateEditing
{
    public static List<StyleToken> Copy(IEnumerable<StyleToken> tokens) => tokens.Select(t => new StyleToken
    { Field = t.Field, Literal = t.Literal, Prefix = t.Prefix, Suffix = t.Suffix }).ToList();

    public static void AddField(List<StyleToken> tokens, string field, bool doiLink = false)
    {
        var existing = tokens.FirstOrDefault(t => t.Field == field || (field is "doi" or "doi-url" && t.Field is "doi" or "doi-url"));
        if (existing is not null)
        {
            if (field is "doi" or "doi-url")
            {
                existing.Field = doiLink ? "doi-url" : "doi";
                existing.Prefix = doiLink ? " " : " DOI: ";
            }
            return;
        }
        tokens.Add(new StyleToken { Field = doiLink ? "doi-url" : field,
            Prefix = field is "doi" or "doi-url" ? (doiLink ? " " : " DOI: ") : " ", Suffix = "." });
    }
}

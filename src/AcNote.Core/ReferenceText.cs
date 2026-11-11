using System.Net;
using System.Text.RegularExpressions;

namespace AcNote.Core;

/// <summary>Plain reference text, including HTML formatting escaped by BibTeX exports.</summary>
public static class ReferenceText
{
    public static string Plain(string text)
    {
        var result = text;
        for (var pass = 0; pass < 3; pass++)
        {
            var next = WebUtility.HtmlDecode(result);
            next = Regex.Replace(next, @"\\textless(?:\{\})?", "<");
            next = Regex.Replace(next, @"\\textgreater(?:\{\})?", ">");
            // Only known presentation tags: scientific comparisons such as x < y
            // and unknown literal angle-bracket expressions must remain intact.
            next = Regex.Replace(next, "</?(?:span|font|i|b|em|strong|sub|sup|small|sc)\\b(?:[^>\"']|\"[^\"]*\"|'[^']*')*>", "", RegexOptions.IgnoreCase);
            next = Regex.Replace(next, @"<br\s*/?\s*>", " ", RegexOptions.IgnoreCase);
            if (next == result) break;
            result = next;
        }
        return result;
    }
}

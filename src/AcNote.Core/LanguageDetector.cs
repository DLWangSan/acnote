namespace AcNote.Core;

public static class LanguageDetector
{
    public static string Resolve(ReferenceItem item)
    {
        var explicitLanguage = Normalize(item.Language);
        if (explicitLanguage is not null)
            return explicitLanguage;

        var (cjk, latin) = CountScripts(item.Title);
        if (cjk == 0 && latin == 0)
        {
            var names = string.Concat(item.Authors.Select(author =>
                author.Literal + author.Family + author.Given));
            (cjk, latin) = CountScripts(names);
        }

        if (cjk == 0 && latin == 0)
            return "en";

        return cjk >= latin ? "zh" : "en";
    }

    public static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var value = language.Trim().ToLowerInvariant();
        if (value is "zh" or "zh-cn" or "zh-hans" or "cn" or "chinese" or "中文")
            return "zh";
        if (value is "en" or "en-us" or "en-gb" or "english" or "英文")
            return "en";
        return null;
    }

    public static bool ContainsCjk(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        foreach (var ch in text)
        {
            if (IsCjk(ch))
                return true;
        }
        return false;
    }

    private static (int Cjk, int Latin) CountScripts(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return (0, 0);

        var cjk = 0;
        var latin = 0;
        foreach (var ch in text)
        {
            if (IsCjk(ch))
                cjk++;
            else if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                latin++;
        }
        return (cjk, latin);
    }

    private static bool IsCjk(char ch) => ch is >= '\u4E00' and <= '\u9FFF';
}

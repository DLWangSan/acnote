using System.Text;
using System.Text.RegularExpressions;
using AcNote.Core;

namespace AcNote.Import.EndNote;

internal sealed class BibTexReader(string text)
{
    private int _position;
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _entries = new(StringComparer.Ordinal);
    private readonly ImportResult _result = new();

    public static ImportResult Read(string text) => new BibTexReader(text).Parse();

    private ImportResult Parse()
    {
        var types = new Dictionary<string, string>();
        while (_position < text.Length)
        {
            Skip();
            if (_position >= text.Length) break;
            if (text[_position++] != '@') continue;
            var type = Identifier().ToLowerInvariant(); Skip();
            if (_position >= text.Length || text[_position] is not ('{' or '(')) continue;
            var close = text[_position++] == '{' ? '}' : ')';
            if (type is "comment" or "preamble") { SkipBlock(close); continue; }
            if (type == "string")
            {
                var name = Identifier(); Expect('='); _strings[name] = Value(close); Skip();
                if (_position < text.Length && text[_position] == ',') _position++;
                Expect(close); continue;
            }
            Skip(); var start = _position;
            while (_position < text.Length && text[_position] != ',' && text[_position] != close) _position++;
            var key = text[start.._position].Trim();
            if (key.Length == 0) throw Error("缺少文献键");
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Skip();
            if (_position < text.Length && text[_position] == ',') _position++;
            while (true)
            {
                Skip(); if (_position >= text.Length) throw Error("记录未闭合");
                if (text[_position] == close) { _position++; break; }
                if (text[_position] == ',') { _position++; continue; }
                var name = Identifier(); if (name.Length == 0) throw Error("无效字段名");
                Expect('='); fields[name] = Value(close); Skip();
                if (_position < text.Length && text[_position] != ',' && text[_position] != close) throw Error("字段间缺少逗号");
            }
            if (_entries.ContainsKey(key)) _result.Warnings.Add($"重复 BibTeX 键 {key}，使用最后一条。");
            _entries[key] = fields; types[key] = type;
        }
        foreach (var (key, fields) in _entries)
        {
            var combined = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
            if (fields.TryGetValue("crossref", out var parent) && _entries.TryGetValue(parent, out var inherited))
            {
                foreach (var pair in inherited) if (pair.Key is not "title" and not "author") combined.TryAdd(pair.Key, pair.Value);
                if (inherited.TryGetValue("title", out var bookTitle)) combined.TryAdd("booktitle", bookTitle);
            }
            string Get(params string[] names) => Clean(names.Select(n => combined.GetValueOrDefault(n)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "");
            if (Get("title").Length == 0) { _result.Warnings.Add($"{key} 没有题名，已跳过。"); continue; }
            var item = new ReferenceItem { SourceKey = key, Type = ReferenceImporter.TypeOf(types[key]), Title = Get("title"),
                ContainerTitle = Get("journal", "journaltitle", "booktitle"), Year = ReferenceImporter.Year(Get("year", "date")),
                Volume = Get("volume"), Issue = Get("number", "issue"), Page = Get("pages").Replace("--", "-"),
                Publisher = Get("publisher", "school", "institution"), PublisherPlace = Get("address", "location"),
                Doi = Get("doi"), Url = Get("url"), Language = Get("language", "langid") };
            // Split at top-level 'and', keeping corporate authors inside braces intact.
            foreach (var name in SplitAuthors(combined.GetValueOrDefault("author") ?? ""))
                item.Authors.Add(name.StartsWith('{') && name.EndsWith('}') ? new Author { Literal = Clean(name) } : AuthorFormatter.Parse(Clean(name)));
            foreach (var pair in combined) item.Extra[pair.Key] = Clean(pair.Value);
            _result.Items.Add(item);
        }
        return _result;
    }

    private string Identifier()
    {
        Skip(); var start = _position;
        while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || "_-:.".Contains(text[_position]))) _position++;
        return text[start.._position];
    }
    private void Skip()
    {
        while (_position < text.Length)
        {
            if (char.IsWhiteSpace(text[_position])) { _position++; continue; }
            if (text[_position] == '%') { while (_position < text.Length && text[_position] != '\n') _position++; continue; }
            break;
        }
    }
    private void Expect(char c) { Skip(); if (_position >= text.Length || text[_position++] != c) throw Error($"缺少 {c}"); }
    private InvalidDataException Error(string message) => new($"BibTeX 第 {_position} 个字符附近：{message}。");
    private void SkipBlock(char close)
    {
        var open = close == '}' ? '{' : '('; var depth = 1;
        while (_position < text.Length && depth > 0)
        {
            var c = text[_position++]; if (c == '\\' && _position < text.Length) { _position++; continue; }
            if (c == open) depth++; if (c == close) depth--;
        }
        if (depth != 0) throw Error("块未闭合");
    }
    private string Value(char close)
    {
        var value = new StringBuilder();
        do
        {
            Skip(); if (_position >= text.Length) throw Error("缺少字段值");
            if (text[_position] is '{' or '"')
            {
                var open = text[_position++]; var end = open == '{' ? '}' : '"'; var depth = 0; var terminated = false;
                while (_position < text.Length)
                {
                    var c = text[_position++];
                    if (c == '\\' && _position < text.Length) { value.Append(c).Append(text[_position++]); continue; }
                    if (c == end && depth == 0) { terminated = true; break; }
                    if (c == '{') depth++; if (c == '}') depth--;
                    value.Append(c);
                }
                if (!terminated) throw Error("字段值未闭合");
            }
            else
            {
                var start = _position;
                while (_position < text.Length && !char.IsWhiteSpace(text[_position]) && text[_position] != ',' && text[_position] != close && text[_position] != '#') _position++;
                if (start == _position) throw Error("缺少字段值");
                var token = text[start.._position]; value.Append(_strings.GetValueOrDefault(token) ?? token);
            }
            Skip(); if (_position >= text.Length || text[_position] != '#') break;
            _position++;
        } while (true);
        return value.ToString();
    }
    private static IEnumerable<string> SplitAuthors(string text)
    {
        text = Regex.Replace(text, @"\s+", " ");
        var depth = 0; var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{') depth++; if (text[i] == '}') depth--;
            if (depth == 0 && i + 5 <= text.Length && text.AsSpan(i, 5).Equals(" and ", StringComparison.OrdinalIgnoreCase))
            { yield return text[start..i].Trim(); start = i + 5; i += 4; }
        }
        if (!string.IsNullOrWhiteSpace(text[start..])) yield return text[start..].Trim();
    }
    private static string Clean(string text)
    {
        text = Regex.Replace(text, @"\\(['""`^~=.uvHck])\s*\{?([A-Za-z])\}?", m =>
        {
            var accent = m.Groups[1].Value switch { "'" => "\u0301", "\"" => "\u0308", "`" => "\u0300", "^" => "\u0302", "~" => "\u0303", "=" => "\u0304", "." => "\u0307", "u" => "\u0306", "v" => "\u030c", "H" => "\u030b", "c" => "\u0327", _ => "\u0328" };
            return (m.Groups[2].Value + accent).Normalize();
        });
        text = Regex.Replace(text, @"\\(?:textit|textbf|emph|textrm|url)\s*", "");
        text = Regex.Replace(text, @"\\([&%_$#{}])", "$1");
        return text.Replace("{", "").Replace("}", "").Replace("~", " ").Trim();
    }
}

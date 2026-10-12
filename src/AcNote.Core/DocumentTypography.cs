namespace AcNote.Core;

/// <summary>Document overrides; null properties continue to follow the selected style.</summary>
public sealed class DocumentTypography
{
    public TextLayout Citation { get; set; } = new();
    public TextLayout Bibliography { get; set; } = new();

    public void Validate()
    {
        if (Citation is null || Bibliography is null)
            throw new InvalidOperationException("文档的字体段落设置不完整。");
        Citation.Validate();
        Bibliography.Validate();
    }

    public static TextLayout Resolve(TextLayout style, TextLayout overrides)
    {
        var result = new TextLayout();
        result.Merge(style);
        result.Merge(overrides);
        result.Validate();
        return result;
    }
}

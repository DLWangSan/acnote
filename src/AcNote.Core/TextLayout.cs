namespace AcNote.Core;

public sealed class TextLayout
{
    public string? FontFamily { get; set; }
    public string? EastAsiaFontFamily { get; set; }
    public double? FontSize { get; set; }
    public bool? Superscript { get; set; }
    public double? LineSpacing { get; set; }
    public double? LineSpacingPoints { get; set; }
    public double? SpaceBefore { get; set; }
    public double? SpaceAfter { get; set; }
    public double? HangingIndent { get; set; }
    public string? Alignment { get; set; }

    public void Merge(TextLayout other)
    {
        FontFamily = other.FontFamily ?? FontFamily;
        EastAsiaFontFamily = other.EastAsiaFontFamily ?? EastAsiaFontFamily;
        FontSize = other.FontSize ?? FontSize;
        Superscript = other.Superscript ?? Superscript;
        if (other.LineSpacing is not null) { LineSpacing = other.LineSpacing; LineSpacingPoints = null; }
        if (other.LineSpacingPoints is not null) { LineSpacingPoints = other.LineSpacingPoints; LineSpacing = null; }
        SpaceBefore = other.SpaceBefore ?? SpaceBefore;
        SpaceAfter = other.SpaceAfter ?? SpaceAfter;
        HangingIndent = other.HangingIndent ?? HangingIndent;
        Alignment = other.Alignment ?? Alignment;
    }

    public void Validate()
    {
        if (FontSize is double size && (!double.IsFinite(size) || size < 5 || size > 96))
            throw new InvalidOperationException("字号必须为 5–96 磅。");
        if (LineSpacing is double line && (!double.IsFinite(line) || line < 0.5 || line > 5))
            throw new InvalidOperationException("行距倍数必须为 0.5–5。");
        if (LineSpacingPoints is double points && (!double.IsFinite(points) || points < 5 || points > 144))
            throw new InvalidOperationException("固定行距必须为 5–144 磅。");
        foreach (var value in new[] { SpaceBefore, SpaceAfter, HangingIndent })
            if (value is double number && (!double.IsFinite(number) || number < 0 || number > 144))
                throw new InvalidOperationException("段落间距和悬挂缩进必须为 0–144 磅。");
        if (Alignment is not null && Alignment is not ("left" or "justify" or "center" or "right"))
            throw new InvalidOperationException("不支持的段落对齐方式。");
    }
}

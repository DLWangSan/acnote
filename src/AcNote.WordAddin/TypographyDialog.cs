using AcNote.Core;
using System.Drawing;
using System.Windows.Forms;

namespace AcNote.WordAddin;

internal sealed class TypographyDialog : Form
{
    private readonly LayoutChoices _citation;
    private readonly LayoutChoices _bibliography;
    public DocumentTypography Value => new() { Citation = _citation.Value, Bibliography = _bibliography.Value };

    public TypographyDialog(DocumentTypography current)
    {
        Text = "AcNote · 当前文档字体与段落";
        Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 510);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.Controls.Add(new Label { Text = "仅应用于当前文档，保存文档后保留。\n“跟随版式”会采用功能区所选版式的设置。", AutoSize = true }, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        _citation = new LayoutChoices(current.Citation, false);
        _bibliography = new LayoutChoices(current.Bibliography, true);
        var inline = new TabPage("文中引文"); inline.Controls.Add(_citation);
        var bibliography = new TabPage("文末参考文献"); bibliography.Controls.Add(_bibliography);
        tabs.TabPages.Add(inline); tabs.TabPages.Add(bibliography);
        root.Controls.Add(tabs, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
        var reset = new Button { Text = "全部跟随版式", AutoSize = true };
        reset.Click += (_, _) => { _citation.Reset(); _bibliography.Reset(); };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok); buttons.Controls.Add(reset);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        AcceptButton = ok; CancelButton = cancel;
    }

    public DialogResult ShowForWord(object application)
    {
        dynamic word = application;
        return ShowDialog(new WindowOwner(new IntPtr((int)word.ActiveWindow.Hwnd)));
    }

    private sealed record WindowOwner(IntPtr Handle) : IWin32Window;

    private sealed class LayoutChoices : TableLayoutPanel
    {
        private sealed record Choice(string Label, object? Value)
        { public override string ToString() => Label; }
        private readonly ComboBox _font, _eastAsian, _size, _superscript;
        private readonly ComboBox? _line, _before, _after, _indent, _alignment;

        public LayoutChoices(TextLayout initial, bool paragraph)
        {
            Dock = DockStyle.Fill; Padding = new Padding(14); ColumnCount = 2; AutoScroll = true;
            ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var fonts = FontFamily.Families.Select(f => f.Name).Distinct().OrderBy(n => n).Select(n => new Choice(n, n)).ToArray();
            _eastAsian = Add("中文字体", fonts, initial.EastAsiaFontFamily);
            _font = Add("西文字体", fonts, initial.FontFamily);
            _size = Add("字号", new double[] { 5, 6.5, 7.5, 8, 9, 10, 10.5, 11, 12, 14, 15, 16, 18, 20, 22, 24, 26, 28, 36, 42, 48, 54, 72, 96 }
                .Select(n => new Choice(n switch { 9 => "小五 · 9 磅", 10.5 => "五号 · 10.5 磅", 12 => "小四 · 12 磅", 14 => "四号 · 14 磅", 16 => "三号 · 16 磅", _ => $"{n} 磅" }, n)), initial.FontSize);
            _superscript = Add("文字位置", [new("正常", false), new("上标", true)], initial.Superscript);
            if (!paragraph) return;
            _line = Add("行距", new double[] { 1, 1.15, 1.25, 1.5, 2, 2.5, 3 }.Select(n => new Choice($"{n} 倍", n)), initial.LineSpacing);
            var spacing = new double[] { 0, 3, 6, 9, 12, 18, 24, 36, 48 }.Select(n => new Choice($"{n} 磅", n)).ToArray();
            _before = Add("段前间距", spacing, initial.SpaceBefore);
            _after = Add("段后间距", spacing, initial.SpaceAfter);
            _indent = Add("悬挂缩进", spacing, initial.HangingIndent);
            _alignment = Add("对齐", [new("左对齐", "left"), new("两端对齐", "justify"), new("居中", "center"), new("右对齐", "right")], initial.Alignment);
        }

        private ComboBox Add(string label, IEnumerable<Choice> options, object? selected)
        {
            var row = RowCount++;
            RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, IntegralHeight = false, DropDownHeight = 260 };
            combo.Items.Add(new Choice("跟随版式", null));
            combo.Items.AddRange(options.Cast<object>().ToArray());
            combo.SelectedIndex = 0;
            if (selected is not null)
            {
                var index = combo.Items.Cast<Choice>().ToList().FindIndex(c => Equals(c.Value, selected));
                if (index < 0) index = combo.Items.Add(new Choice(selected.ToString()!, selected));
                combo.SelectedIndex = index;
            }
            Controls.Add(combo, 1, row);
            return combo;
        }

        private static object? Selected(ComboBox? combo) => (combo?.SelectedItem as Choice)?.Value;
        public TextLayout Value => new()
        {
            FontFamily = (string?)Selected(_font), EastAsiaFontFamily = (string?)Selected(_eastAsian),
            FontSize = (double?)Selected(_size), Superscript = (bool?)Selected(_superscript),
            LineSpacing = (double?)Selected(_line), SpaceBefore = (double?)Selected(_before),
            SpaceAfter = (double?)Selected(_after), HangingIndent = (double?)Selected(_indent),
            Alignment = (string?)Selected(_alignment)
        };
        public void Reset() { foreach (var combo in Controls.OfType<ComboBox>()) combo.SelectedIndex = 0; }
    }
}

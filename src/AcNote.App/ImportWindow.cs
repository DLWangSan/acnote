using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AcNote.App;

internal sealed class ImportWindow : Window
{
    public string[] Files { get; private set; } = [];

    public ImportWindow()
    {
        Title = "导入文献"; Width = 540; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "文献来自哪里？", FontSize = 19, Margin = new Thickness(0, 0, 0, 16) });
        var source = new ComboBox { ItemsSource = new[] { "自动识别", "EndNote", "Zotero", "BibTeX / BibLaTeX", "RIS（其他文献管理工具）", "CSL JSON" }, SelectedIndex = 0 };
        panel.Children.Add(source);
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 16) };
        void UpdateHint() => hint.Text = source.SelectedIndex switch
        {
            1 => "选择 .enl、.enlx 或 EndNote XML。.enl 旁请保留同名 .Data 文件夹。",
            2 => "选择 Zotero 数据目录中的 zotero.sqlite，或导出的 CSL JSON、BibTeX、RIS 文件。导入文献条目；暂不迁移附件、批注和分类目录。",
            _ => "支持多选文件，也可以直接把文件拖到主窗口。有文献库时加入当前库；没有文献库时自动建立临时库，并保留到下次打开。",
        };
        source.SelectionChanged += (_, _) => UpdateHint(); UpdateHint(); panel.Children.Add(hint);
        var choose = new Button { Content = "选择文件并导入", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 8, 16, 8), IsDefault = true };
        choose.Click += (_, _) =>
        {
            var filter = source.SelectedIndex switch
            {
                1 => "EndNote|*.enl;*.enlx;*.xml",
                2 => "Zotero 文献库或导出文件|*.sqlite;*.json;*.bib;*.bibtex;*.ris",
                3 => "BibTeX / BibLaTeX|*.bib;*.bibtex",
                4 => "RIS|*.ris",
                5 => "CSL JSON|*.json",
                _ => "文献库与文献文件|*.enl;*.enlx;*.xml;*.sqlite;*.bib;*.bibtex;*.ris;*.json",
            };
            var picker = new OpenFileDialog { Title = "选择文献文件", Filter = filter, Multiselect = true };
            if (picker.ShowDialog(this) != true) return;
            Files = picker.FileNames; DialogResult = true;
        };
        panel.Children.Add(choose); Content = panel;
    }
}

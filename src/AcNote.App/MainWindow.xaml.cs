using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using AcNote.Core;
using AcNote.Import.EndNote;
using AcNote.Word;
using Microsoft.Win32;

namespace AcNote.App;

public partial class MainWindow : Window
{
    private readonly Session _session;
    private readonly ObservableCollection<ReferenceRow> _rows = [];
    private readonly ObservableCollection<string> _missing = [];
    private bool _loading;
    private string? _shownId;

    public MainWindow(Session session)
    {
        _session = session;
        InitializeComponent();
        ReferenceList.ItemsSource = _rows;
        MissingList.ItemsSource = _missing;
        TypeCombo.ItemsSource = ReferenceTypes.All.Select(item => new TypeChoice(item.Id, item.Label)).ToList();
        FillStyleCombo();
        Reload(_session.Store?.List().FirstOrDefault()?.Id);
    }

    public void SetWordStatus(string text) => WordStatusText.Text = text;

    public void SaveShown() => Save(_shownId);

    private void FillStyleCombo()
    {
        _loading = true;
        StyleCombo.ItemsSource = _session.Catalog.All;
        StyleCombo.SelectedItem = _session.Catalog.All.FirstOrDefault(style => style.Id == _session.StyleId)
            ?? _session.Catalog.All.FirstOrDefault();
        _loading = false;
    }

    private void Reload(string? selectId)
    {
        Save(_shownId);
        UpdateLibraryTitle();
        _loading = true;
        _rows.Clear();
        if (_session.Store is not LibraryStore store)
        {
            SummaryText.Text = "尚未打开文献库";
            _loading = false;
            ClearForm();
            return;
        }

        var filter = FilterBox.Text.Trim();
        var style = _session.CurrentStyle;
        var missingCount = 0;
        foreach (var item in store.List())
        {
            var missing = MissingFieldChecker.Check(item, style);
            if (missing.Count > 0)
                missingCount++;
            if (!Matches(item, filter))
                continue;
            _rows.Add(new ReferenceRow
            {
                Id = item.Id,
                Title = string.IsNullOrWhiteSpace(item.Title) ? "未命名" : item.Title,
                Meta = $"{item.Year}  {ReferenceTypes.LabelOf(item.Type)}".Trim(),
                MissingText = missing.Count == 0 ? "" : "缺" + string.Join("、", missing),
                HasMissing = missing.Count > 0,
            });
        }

        var total = store.Count();
        SummaryText.Text = $"文献 {total} 条，其中 {missingCount} 条在当前版式下有缺项";
        ReferenceList.SelectedItem = _rows.FirstOrDefault(row => row.Id == selectId) ?? _rows.FirstOrDefault();
        _loading = false;
        if (ReferenceList.SelectedItem is ReferenceRow selected)
            Show(selected.Id);
        else
            ClearForm();
    }

    private void Show(string id)
    {
        var item = _session.Store?.Find(id);
        if (item is null)
        {
            ClearForm();
            return;
        }

        _loading = true;
        _shownId = item.Id;
        SelectType(item.Type);
        SelectLanguage(item.Language);
        TitleBox.Text = item.Title;
        AuthorsBox.Text = AuthorFormatter.ToEditorText(item.Authors);
        ContainerBox.Text = item.ContainerTitle;
        YearBox.Text = item.Year;
        VolumeBox.Text = item.Volume;
        IssueBox.Text = item.Issue;
        PageBox.Text = item.Page;
        PlaceBox.Text = item.PublisherPlace;
        PublisherBox.Text = item.Publisher;
        DoiBox.Text = item.Doi;
        UrlBox.Text = item.Url;
        UpdateLabels();
        _loading = false;
        UpdatePreview();
    }

    private void ClearForm()
    {
        _loading = true;
        _shownId = null;
        TitleBox.Clear();
        AuthorsBox.Clear();
        ContainerBox.Clear();
        YearBox.Clear();
        VolumeBox.Clear();
        IssueBox.Clear();
        PageBox.Clear();
        PlaceBox.Clear();
        PublisherBox.Clear();
        DoiBox.Clear();
        UrlBox.Clear();
        PreviewBox.Clear();
        _missing.Clear();
        CompleteText.Visibility = Visibility.Collapsed;
        _loading = false;
    }

    private void Save(string? id)
    {
        if (_loading || string.IsNullOrEmpty(id) || _session.Store is not LibraryStore store)
            return;

        var existing = store.Find(id);
        var item = ReadForm();
        item.Id = id;
        if (existing is not null)
        {
            item.SourceKey = existing.SourceKey;
            item.Extra = existing.Extra;
        }
        store.Save(item);
    }

    private ReferenceItem ReadForm() => new()
        {
            Type = CurrentType(),
            Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            Title = TitleBox.Text.Trim(),
            Authors = AuthorFormatter.ParseList(AuthorsBox.Text),
            ContainerTitle = ContainerBox.Text.Trim(),
            Year = YearBox.Text.Trim(),
            Volume = VolumeBox.Text.Trim(),
            Issue = IssueBox.Text.Trim(),
            Page = PageBox.Text.Trim(),
            PublisherPlace = PlaceBox.Text.Trim(),
            Publisher = PublisherBox.Text.Trim(),
            Doi = DoiBox.Text.Trim(),
            Url = UrlBox.Text.Trim(),
        };

    private void UpdatePreview()
    {
        if (_loading)
            return;

        var item = ReadForm();
        var formatted = BibliographyFormatter.Format(item, _session.CurrentStyle);
        PreviewBox.Text = formatted.Text;
        _missing.Clear();
        foreach (var missing in formatted.Missing)
            _missing.Add("缺" + missing);
        CompleteText.Visibility = formatted.Missing.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        var type = CurrentType();
        ContainerLabel.Text = type switch
        {
            "book" or "chapter" => "书名",
            "paper-conference" => "会议名称",
            "thesis" => "来源（可不填）",
            "dataset" => "来源（可不填）",
            _ => "刊名",
        };
        PlaceLabel.Text = type == "thesis" ? "保存地" : "出版地";
        PublisherLabel.Text = type == "thesis" ? "保存单位" : "出版社";
    }

    private string CurrentType() => (TypeCombo.SelectedItem as TypeChoice)?.Id ?? "article-journal";

    private void SelectType(string type)
    {
        TypeCombo.SelectedItem = TypeCombo.Items.OfType<TypeChoice>().FirstOrDefault(item => item.Id == type)
            ?? TypeCombo.Items.OfType<TypeChoice>().FirstOrDefault();
    }

    private void SelectLanguage(string language)
    {
        var normalized = LanguageDetector.Normalize(language) ?? "";
        foreach (var item in LanguageCombo.Items)
        {
            if (item is ComboBoxItem combo && (combo.Tag as string ?? "") == normalized)
            {
                LanguageCombo.SelectedItem = item;
                return;
            }
        }
        LanguageCombo.SelectedIndex = 0;
    }

    private static bool Matches(ReferenceItem item, string filter)
    {
        if (filter.Length == 0)
            return true;
        var authors = AuthorFormatter.ToEditorText(item.Authors);
        return item.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
            || authors.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
            || item.ContainerTitle.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    private void ReferenceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        var next = (ReferenceList.SelectedItem as ReferenceRow)?.Id;
        if (next == _shownId)
            return;
        var previous = _shownId;
        Save(previous);
        if (next is null)
            ClearForm();
        else
            Show(next);
        RefreshRow(previous);
    }

    private void RefreshRow(string? id)
    {
        if (id is null)
            return;
        var item = _session.Store?.Find(id);
        var row = _rows.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null || row is null)
            return;
        var index = _rows.IndexOf(row);
        var missing = MissingFieldChecker.Check(item, _session.CurrentStyle);
        _loading = true;
        _rows[index] = new ReferenceRow
        {
            Id = item.Id,
            Title = string.IsNullOrWhiteSpace(item.Title) ? "未命名" : item.Title,
            Meta = $"{item.Year}  {ReferenceTypes.LabelOf(item.Type)}".Trim(),
            MissingText = missing.Count == 0 ? "" : "缺" + string.Join("、", missing),
            HasMissing = missing.Count > 0,
        };
        _loading = false;
    }

    private void Field_Changed(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void Choice_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _loading)
            return;
        Reload(_shownId);
    }

    private void StyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || StyleCombo.SelectedItem is not ResolvedStyle style)
            return;
        _session.StyleId = style.Id;
        Reload(_shownId);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveShown();
        Reload(_shownId);
    }

    private void NewLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "新建文献库",
            Filter = "AcNote 文献库 (*.acnote)|*.acnote",
            DefaultExt = ".acnote",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            SaveShown();
            var path = dialog.FileName;
            DeleteLibraryFiles(path);
            var store = new LibraryStore(path);
            if (MessageBox.Show("放入几条经典文献作为示例？", "AcNote", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                SampleLibrary.Seed(store);
            _session.UseLibrary(store, path);
            Reload(store.List().FirstOrDefault()?.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
        }
    }

    private void OpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开文献库",
            Filter = "AcNote 文献库 (*.acnote)|*.acnote",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            SaveShown();
            var store = new LibraryStore(dialog.FileName);
            _session.UseLibrary(store, dialog.FileName);
            Reload(store.List().FirstOrDefault()?.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
        }
    }

    private void EditStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = _session.CurrentStyle;
        var json = style.FilePath is not null && File.Exists(style.FilePath)
            ? File.ReadAllText(style.FilePath)
            : "";
        var editor = new StyleEditorWindow(json) { Owner = this };
        if (editor.ShowDialog() != true)
            return;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(editor.JsonText);
            var id = document.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("版式 id 不能包含文件名里不允许的字符。", "AcNote");
                return;
            }

            Directory.CreateDirectory(AppPaths.Styles);
            File.WriteAllText(Path.Combine(AppPaths.Styles, id + ".style"), editor.JsonText);
            _session.ReloadStyles();
            _session.StyleId = id;
            FillStyleCombo();
            Reload(_shownId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
        }
    }

    private void UpdateLibraryTitle()
    {
        if (string.IsNullOrWhiteSpace(_session.LibraryPath))
        {
            Title = "AcNote";
            LibraryName.Text = "尚未打开文献库";
            return;
        }

        Title = "AcNote - " + Path.GetFileName(_session.LibraryPath);
        LibraryName.Text = _session.LibraryPath;
    }

    private static void DeleteLibraryFiles(string path)
    {
        foreach (var candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
        }
    }

    private bool EnsureLibrary()
    {
        if (_session.Store is not null)
            return true;
        MessageBox.Show("请先新建或打开一个文献库。", "AcNote");
        return false;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Store is not LibraryStore store)
        {
            MessageBox.Show("请先新建或打开一个文献库。", "AcNote");
            return;
        }
        SaveShown();
        var item = new ReferenceItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "article-journal",
            Title = "未命名",
        };
        store.Save(item);
        Reload(item.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Store is not LibraryStore store || _shownId is null)
        {
            if (_session.Store is null)
                MessageBox.Show("请先新建或打开一个文献库。", "AcNote");
            return;
        }
        if (MessageBox.Show("删除这条文献？", "AcNote", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        var id = _shownId;
        _shownId = null;
        store.Delete(id);
        Reload(null);
    }

    private void ImportEndNote_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入 EndNote 文献库",
            Filter = "EndNote (*.enl;*.enlx;*.xml)|*.enl;*.enlx;*.xml",
        };
        if (dialog.ShowDialog(this) != true)
            return;
        if (_session.Store is not LibraryStore store)
        {
            MessageBox.Show("请先新建或打开一个文献库。", "AcNote");
            return;
        }

        try
        {
            SaveShown();
            var imported = EndNoteImporter.Import(dialog.FileName);
            var added = 0;
            var updated = 0;
            foreach (var item in imported.Items)
            {
                var existing = string.IsNullOrWhiteSpace(item.SourceKey) ? null : store.FindBySource(item.SourceKey);
                if (existing is null)
                {
                    item.Id = Guid.NewGuid().ToString("N");
                    added++;
                }
                else
                {
                    item.Id = existing.Id;
                    item.Extra = existing.Extra;
                    updated++;
                }
                store.Save(item);
            }

            Reload(_shownId);
            var skipped = imported.Warnings.Count == 0 ? "" : $"\n跳过 {imported.Warnings.Count} 条。";
            MessageBox.Show($"导入 {added} 条，更新 {updated} 条。{skipped}", "AcNote");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
        }
    }

    private void ImportStyle_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入版式",
            Filter = "版式文件 (*.style)|*.style",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            Directory.CreateDirectory(AppPaths.Styles);
            var destination = Path.Combine(AppPaths.Styles, Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, destination, overwrite: true);
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(destination));
            var id = json.RootElement.TryGetProperty("id", out var property) ? property.GetString() : null;
            _session.ReloadStyles();
            if (!string.IsNullOrWhiteSpace(id))
                _session.StyleId = id;
            FillStyleCombo();
            Reload(_shownId);
            MessageBox.Show($"已安装版式：{_session.CurrentStyle.DisplayName}", "AcNote");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
        }
    }

    private void InsertWord_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLibrary())
            return;
        SaveShown();
        if (_shownId is null || _session.Store is null)
        {
            MessageBox.Show("请先选择一条文献。", "AcNote");
            return;
        }

        var word = RunningWord.TryGet();
        if (word is null)
        {
            MessageBox.Show("请先打开 Word，并把光标放在要插入的位置。", "AcNote");
            return;
        }

        try
        {
            dynamic application = word;
            object document = application.ActiveDocument;
            object selection = application.Selection;
            var styleId = CitationDocument.GetStyleId(document);
            var style = _session.Catalog.Get(string.IsNullOrWhiteSpace(styleId) ? _session.StyleId : styleId);
            var form = (InsertFormCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "parenthetical";
            CitationDocument.InsertAndRefresh(document, selection, [_shownId], form, style, _session.Store.Find);
        }
        catch (Exception ex)
        {
            MessageBox.Show("没能写入 Word。" + ex.Message, "AcNote");
        }
    }
}

public sealed record TypeChoice(string Id, string Label);

public sealed class ReferenceRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Meta { get; init; }
    public required string MissingText { get; init; }
    public required bool HasMissing { get; init; }
}

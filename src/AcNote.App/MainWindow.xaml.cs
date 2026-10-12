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
    private string _filterMode = "all";
    private readonly HashSet<string> _recentIds = [];
    public List<string> SelectedIds => ReferenceList.Items.OfType<ReferenceRow>().Where(row => ReferenceList.SelectedItems.Contains(row)).Select(row => row.Id).ToList();
    public string SelectedForm => (InsertFormCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "parenthetical";


    public MainWindow(Session session)
    {
        _session = session;
        InitializeComponent();
        ReferenceList.ItemsSource = _rows;
        MissingList.ItemsSource = _missing;
        TypeCombo.ItemsSource = ReferenceTypes.All.Select(item => new TypeChoice(item.Id, item.Label)).ToList();
        FillStyleCombo();
        Reload(_session.Store?.List().FirstOrDefault()?.Id);
        Closing += (_, _) => SaveShown();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.D2: InsertWord_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case System.Windows.Input.Key.S: Save_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case System.Windows.Input.Key.N: New_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            }
        }
        base.OnPreviewKeyDown(e);
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
        var selectedIds = SelectedIds.ToHashSet();
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
            if (_filterMode == "missing" && missing.Count == 0) continue;
            if (_filterMode == "recent" && !_recentIds.Contains(item.Id)) continue;
            if (!Matches(item, filter))
                continue;
            _rows.Add(new ReferenceRow
            {
                Id = item.Id,
                Title = string.IsNullOrWhiteSpace(item.Title) ? "未命名" : item.Title,
                Authors = AuthorFormatter.FormatList(item, style.EtAl),
                Year = item.Year,
                Type = ReferenceTypes.LabelOf(item.Type),
                Meta = $"{item.Year}  {ReferenceTypes.LabelOf(item.Type)}".Trim(),
                MissingText = missing.Count == 0 ? "" : "缺" + string.Join("、", missing),
                HasMissing = missing.Count > 0,
            });
        }

        var total = store.Count();
        SummaryText.Text = $"文献 {total} 条，其中 {missingCount} 条在当前版式下有缺项";
        ReferenceList.SelectedItem = _rows.FirstOrDefault(row => row.Id == selectId) ?? _rows.FirstOrDefault();
        foreach (var row in _rows.Where(row => selectedIds.Contains(row.Id)))
            if (!ReferenceList.SelectedItems.Contains(row)) ReferenceList.SelectedItems.Add(row);
        _loading = false;
        UpdateSelectionPreview();
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
        EditorScroll.ScrollToTop();
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
        UpdateSelectionPreview();
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
        UpdateSelectionPreview();
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
        var missing = MissingFieldChecker.Check(item, _session.CurrentStyle);
        row.Title = string.IsNullOrWhiteSpace(item.Title) ? "未命名" : item.Title;
        row.Authors = AuthorFormatter.FormatList(item, _session.CurrentStyle.EtAl);
        row.Year = item.Year;
        row.Type = ReferenceTypes.LabelOf(item.Type);
        row.Meta = $"{item.Year}  {row.Type}";
        row.MissingText = missing.Count == 0 ? "" : "缺" + string.Join("、", missing);
        row.HasMissing = missing.Count > 0;
        row.NotifyChanged();
    }

    private void ReferenceList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        var onCheckbox = false;
        DataGridRow? row = null;
        while (source is not null)
        {
            if (source is CheckBox) onCheckbox = true;
            if (source is DataGridRow found) { row = found; break; }
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        if (!onCheckbox || row is null) return;
        e.Handled = true;
        row.IsSelected = !row.IsSelected;
        UpdateSelectionPreview();
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
            _shownId = null;
            _recentIds.Clear();
            ReferenceList.SelectedItems.Clear();
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
            _shownId = null;
            _recentIds.Clear();
            ReferenceList.SelectedItems.Clear();
            _session.UseLibrary(store, dialog.FileName);
            Reload(store.List().FirstOrDefault()?.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "AcNote");
        }
    }

    public void EditCurrentStyle() => EditStyle_Click(this, new RoutedEventArgs());

    private void EditStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = _session.CurrentStyle;
        var json = style.FilePath is not null && File.Exists(style.FilePath)
            ? File.ReadAllText(style.FilePath)
            : "";
        var editor = new StyleEditorWindow(json, style) { Owner = this };
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
            StyleCatalog.Install(editor.JsonText, AppPaths.Styles, Path.Combine(AppContext.BaseDirectory, "styles"));
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
        if (MessageBox.Show($"删除选中的 {SelectedIds.Count} 条文献？", "AcNote", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        var ids = SelectedIds;
        _shownId = null;
        foreach (var id in ids) store.Delete(id);
        Reload(null);
    }

    private void ImportEndNote_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ImportWindow { Owner = this };
        if (dialog.ShowDialog() == true) OpenFiles(dialog.Files);
    }

    private void FileDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(p => ReferenceImporter.Supports(p) || Path.GetExtension(p).Equals(".acnote", StringComparison.OrdinalIgnoreCase))
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FileDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) OpenFiles(paths);
        e.Handled = true;
    }

    public void OpenFiles(IEnumerable<string> paths)
    {
        SaveShown();
        var added = 0; var updated = 0; var warnings = new List<string>();
        _recentIds.Clear();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("找不到文件。", path);
                if (Path.GetExtension(path).Equals(".acnote", StringComparison.OrdinalIgnoreCase))
                {
                    var opened = new LibraryStore(path);
                    _shownId = null; ReferenceList.SelectedItems.Clear();
                    _session.UseLibrary(opened, path);
                    continue;
                }
                var imported = ReferenceImporter.Import(path);
                warnings.AddRange(imported.Warnings.Select(w => Path.GetFileName(path) + "：" + w));
                if (imported.Items.Count == 0) continue;
                if (_session.Store is null)
                {
                    var temporary = Path.Combine(AppPaths.Root, "Libraries", $"临时文献库-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.acnote");
                    _session.UseLibrary(new LibraryStore(temporary), temporary);
                }
                var store = _session.Store!;
                foreach (var item in imported.Items)
                {
                    var existing = store.FindBySource(item.SourceKey);
                    if (existing is null) { item.Id = Guid.NewGuid().ToString("N"); added++; }
                    else
                    {
                        item.Id = existing.Id;
                        foreach (var pair in existing.Extra) item.Extra.TryAdd(pair.Key, pair.Value);
                        updated++;
                    }
                    store.Save(item); _recentIds.Add(item.Id);
                }
            }
            catch (Exception ex) { warnings.Add(Path.GetFileName(path) + "：" + ex.Message); }
        }
        // Discard the old editor snapshot before reloading an updated selected record.
        _shownId = null;
        _filterMode = "all"; AllReferences.IsChecked = true; FilterBox.Clear();
        Reload(_recentIds.FirstOrDefault());
        SummaryText.Text = $"导入 {added} 条，更新 {updated} 条；当前库 {_session.Store?.Count() ?? 0} 条";
        if (warnings.Count > 0)
        {
            SummaryText.Text += $"；{warnings.Count} 项提示";
            MessageBox.Show(this, string.Join("\n", warnings.Take(12)) + (warnings.Count > 12 ? "\n其余提示已省略。" : ""), "导入提示");
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
            var id = StyleCatalog.Install(File.ReadAllText(dialog.FileName), AppPaths.Styles, Path.Combine(AppContext.BaseDirectory, "styles"));
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

    private void FilterMode_Click(object sender, RoutedEventArgs e)
    {
        _filterMode = (sender as RadioButton)?.Tag as string ?? "all";
        if (IsLoaded) Reload(_shownId);
    }

    private void UpdateSelectionPreview()
    {
        if (SelectionText is null) return;
        SelectionText.Text = $"已选择 {SelectedIds.Count} 条 · Ctrl / Shift 多选";
        if (_session.Store is null) return;
        var selected = SelectedIds.Select(id =>
        {
            if (id != _shownId) return _session.Store.Find(id);
            var current = ReadForm(); current.Id = id; return current;
        }).OfType<ReferenceItem>().ToList();
        var years = YearDisambiguator.Resolve(selected, _session.CurrentStyle.CitationEtAl);
        var authorYear = _session.CurrentStyle.Layout == "author-year";
        var listOptions = BibliographyOptions.Resolve(_session.CurrentStyle.BibliographyOptions, new());
        if (authorYear) selected = listOptions.Order(selected).ToList();
        SelectionPreview.Text = string.Join(Environment.NewLine + Environment.NewLine, selected.Select((item, index) =>
            (authorYear && listOptions.NumberAuthorYear != true ? "" : $"[{index + 1}] ") +
            BibliographyFormatter.Format(item, _session.CurrentStyle, _session.CurrentStyle.Layout == "author-year" ? years.GetValueOrDefault(item.Id) : null).Text));
    }

    private void UpdateWord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveShown();
            if (_session.Store is null) return;
            dynamic app = RunningWord.TryGet() ?? throw new InvalidOperationException("请先打开 Word。");
            object document = app.ActiveDocument;
            CitationDocument.BindLibrary(document, _session.LibraryPath!);
            CitationDocument.Refresh(document, _session.Catalog.Get(CitationDocument.GetStyleId(document)), _session.Store.Find);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "AcNote"); }
    }

    private void InsertWord_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureLibrary())
            return;
        SaveShown();
        if (SelectedIds.Count == 0 || _session.Store is null)
        {
            MessageBox.Show("请先选择一条或多条文献。", "AcNote");
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
            CitationDocument.BindLibrary(document, _session.LibraryPath!);
            CitationDocument.InsertAndRefresh(document, selection, SelectedIds, form, style, _session.Store.Find);
        }
        catch (Exception ex)
        {
            MessageBox.Show("没能写入 Word。" + ex.Message, "AcNote");
        }
    }
}

public sealed record TypeChoice(string Id, string Label);

public sealed class ReferenceRow : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void NotifyChanged() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(null));
    public string Authors { get; set; } = "";
    public string Year { get; set; } = "";
    public string Type { get; set; } = "";
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Meta { get; set; }
    public required string MissingText { get; set; }
    public required bool HasMissing { get; set; }
}

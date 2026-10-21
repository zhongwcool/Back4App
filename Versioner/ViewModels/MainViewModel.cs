using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Versioner.Helpers;
using Versioner.Models;
using Versioner.Services.Parse;

namespace Versioner.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IAppReleaseClient _client;
    private readonly ILogger<MainViewModel> _logger;
    private readonly EditorState _empty = new("", "", "", false, true);
    private EditorState _snapshot;
    private EditorMode _mode = EditorMode.Empty;
    private AppReleaseListItem? _currentSelection;
    private bool _reverting;
    private bool _confirming;
    private bool _dialogOpen;

    public MainViewModel(IAppReleaseClient client, ILogger<MainViewModel> logger)
    {
        _client = client;
        _logger = logger;
        IsConfigured = client.IsConfigured;
        _snapshot = _empty;
        StatusText = IsConfigured
            ? "就绪"
            : "未配置 Back4App 密钥。请在 appsettings.local.json 中填写 ApplicationId 和 MasterKey。";
    }

    public bool IsConfigured { get; }

    public ObservableCollection<AppReleaseListItem> Releases { get; } = [];

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _loadingTitle = "";

    [ObservableProperty]
    private string _loadingDetail = "";

    public ObservableCollection<string> LoadingSteps { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isDrawerOpen;

    [ObservableProperty]
    private AppReleaseListItem? _selectedRelease;

    [ObservableProperty]
    private string _editorTitle = "编辑版本";

    [ObservableProperty]
    private string _editorMeta = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _editVersion = "";

    [ObservableProperty]
    private string _editLines = "";

    [ObservableProperty]
    private string _editDownloadUrl = "";

    [ObservableProperty]
    private bool _editMandatory;

    [ObservableProperty]
    private bool _editEnabled = true;

    public bool HasUnsavedChanges => IsDirty();

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (!await ConfirmDiscardAsync())
            return;

        var keepId = _mode == EditorMode.Edit ? _currentSelection?.Record.ObjectId : null;
        await RunAsync(() => ReloadCoreAsync(keepId), "正在加载版本记录");
    }

    public async Task BeginFromVersionFile(string path)
    {
        if (!IsConfigured)
        {
            StatusText = "未配置 Back4App 密钥。";
            return;
        }

        if (!VersionFile.TryRead(path, out var version, out var error))
        {
            StatusText = error;
            return;
        }

        if (!await ConfirmDiscardAsync())
            return;

        _reverting = true;
        SelectedRelease = null;
        _currentSelection = null;
        _reverting = false;
        BeginNew();
        EditVersion = version;
        StatusText = $"已从 {Path.GetFileName(path)} 读取文件版本 {version}";
    }

    public event Action<bool, bool>? RequiredFieldsMissing;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var versionMissing = string.IsNullOrWhiteSpace(EditVersion);
        var notesMissing = SplitLines(EditLines).Count == 0;
        if (versionMissing || notesMissing)
        {
            RequiredFieldsMissing?.Invoke(versionMissing, notesMissing);
            return;
        }

        if (Validate(out var draft) is { } error)
        {
            StatusText = error;
            return;
        }

        var keepNew = _mode == EditorMode.New;
        var existingId = _currentSelection?.Record.ObjectId;
        await RunAsync(async () =>
        {
            ReportStep(keepNew ? "正在创建记录" : "正在更新记录");
            var id = keepNew
                ? await _client.CreateAsync(draft)
                : existingId ?? throw new AppReleaseException("没有可保存的记录。");
            if (!keepNew)
                await _client.UpdateAsync(id, draft);

            ReportStep(keepNew ? "记录已创建，正在刷新列表" : "修改已写入，正在刷新列表");
            await ReloadCoreAsync(id);
            StatusText = "已提交";
        }, keepNew ? "正在创建版本" : "正在更新版本");
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (_currentSelection is null)
            return;

        if (!await ConfirmAsync("删除版本", $"确定删除版本 {_currentSelection.VersionText}？"))
            return;

        var id = _currentSelection.Record.ObjectId;
        var versionText = _currentSelection.VersionText;
        await RunAsync(async () =>
        {
            ReportStep($"正在删除 {versionText}");
            await _client.DeleteAsync(id);
            ReportStep("记录已删除，正在刷新列表");
            await ReloadCoreAsync(null);
            IsDrawerOpen = false;
            StatusText = "已删除";
        }, "正在删除版本");
    }

    private bool CanRefresh() => IsConfigured && !IsBusy;

    private bool CanSave() => IsConfigured && !IsBusy && IsEditing;

    private bool CanDelete() => IsConfigured && !IsBusy && _mode == EditorMode.Edit && _currentSelection is not null;

    partial void OnSelectedReleaseChanged(AppReleaseListItem? value)
    {
        if (_reverting || _confirming)
            return;

        if (!IsDirty())
        {
            CommitSelection(value);
            return;
        }

        ConfirmSelectionAsync(_currentSelection, value);
    }

    private async void ConfirmSelectionAsync(AppReleaseListItem? previous, AppReleaseListItem? value)
    {
        _confirming = true;
        try
        {
            if (!await ConfirmDiscardAsync())
            {
                _reverting = true;
                SelectedRelease = previous;
                _reverting = false;
                return;
            }

            CommitSelection(value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "确认放弃修改失败");
            _reverting = true;
            SelectedRelease = previous;
            _reverting = false;
        }
        finally
        {
            _confirming = false;
        }
    }

    private void CommitSelection(AppReleaseListItem? value)
    {
        _currentSelection = value;
        if (value is null)
            ShowEmpty();
        else
            Load(value);
    }

    private async Task RunAsync(Func<Task> action, string loadingTitle)
    {
        try
        {
            LoadingTitle = loadingTitle;
            LoadingDetail = "";
            LoadingSteps.Clear();
            IsBusy = true;
            await action();
        }
        catch (AppReleaseException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = "操作失败";
            _logger.LogWarning(ex, "版本记录操作失败");
        }
        finally
        {
            IsBusy = false;
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    private void ReportStep(string step)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ReportStep(step));
            return;
        }

        LoadingDetail = step;
        LoadingSteps.Add(step);
        StatusText = step;
    }

    private async Task ReloadCoreAsync(string? selectObjectId)
    {
        var records = await _client.ListAsync(ReportStep);
        var items = BuildItems(records);
        var current = items.FirstOrDefault(item => item.PickText.Length > 0);
        ReportStep(current is null
            ? $"已排序 {items.Count} 条，没有可发布的版本"
            : $"已排序 {items.Count} 条，当前发布 {current.VersionText}");

        _reverting = true;
        Releases.Clear();
        foreach (var item in items)
            Releases.Add(item);

        var match = selectObjectId is null
            ? null
            : Releases.FirstOrDefault(item => item.Record.ObjectId == selectObjectId);
        match ??= Releases.FirstOrDefault(item => item.PickText.Length > 0) ?? Releases.FirstOrDefault();
        SelectedRelease = match;
        _currentSelection = match;
        _reverting = false;

        if (match is null)
            ShowEmpty();
        else
            Load(match);

        StatusText = current is null
            ? $"已加载 {items.Count} 条"
            : $"已加载 {items.Count} 条，当前发布 {current.VersionText}";
    }

    private static List<AppReleaseListItem> BuildItems(IReadOnlyList<AppReleaseRecord> records)
    {
        var items = new List<AppReleaseListItem>(records.Count);
        foreach (var record in records)
        {
            Version? parsed = VersionNumber.TryNormalize(record.Version, out var version, out _)
                ? version
                : null;

            items.Add(CreateItem(record, parsed, ""));
        }

        items.Sort(static (a, b) =>
        {
            if (a.Version is null && b.Version is null)
                return string.Compare(b.VersionText, a.VersionText, StringComparison.Ordinal);
            if (a.Version is null)
                return 1;
            if (b.Version is null)
                return -1;
            return b.Version.CompareTo(a.Version);
        });

        var pick = items
            .Where(item => item.Record.Enabled && item.Version is not null)
            .OrderByDescending(item => item.Version)
            .ThenByDescending(item => item.Record.Mandatory)
            .FirstOrDefault();
        if (pick is not null)
        {
            var index = items.IndexOf(pick);
            items[index] = CreateItem(pick.Record, pick.Version, "当前发布");
        }

        return items;
    }

    private static AppReleaseListItem CreateItem(AppReleaseRecord record, Version? version, string pickText)
    {
        var notes = ReleaseNotes(record);
        return new AppReleaseListItem
        {
            Record = record,
            Version = version,
            VersionText = string.IsNullOrWhiteSpace(record.Version) ? "—" : record.Version,
            EnabledText = record.Enabled ? "是" : "否",
            MandatoryText = record.Mandatory ? "是" : "否",
            DownloadUrl = record.DownloadUrl,
            NotesText = notes,
            NotesPreview = notes.Replace('\r', ' ').Replace('\n', ' '),
            UpdatedAtText = record.UpdatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
            PickText = pickText
        };
    }

    /// <summary>lines 有内容时用 lines；否则用 notes，并展开字面量 \n。</summary>
    private static string ReleaseNotes(AppReleaseRecord record)
    {
        if (record.Lines.Any(line => line.Length > 0))
            return string.Join('\n', record.Lines);

        return (record.Notes ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\\n", "\n");
    }

    private void Load(AppReleaseListItem item)
    {
        EditorTitle = "编辑版本";
        EditorMeta = $"记录 {item.Record.ObjectId} · 更新于 {item.UpdatedAtText}";
        EditVersion = item.Record.Version;
        EditLines = item.NotesText;
        EditDownloadUrl = item.Record.DownloadUrl;
        EditMandatory = item.Record.Mandatory;
        EditEnabled = item.Record.Enabled;
        IsEditing = true;
        _snapshot = Capture();
        SetMode(EditorMode.Edit);
        StatusText = $"正在编辑 {item.VersionText}";
    }

    private void BeginNew()
    {
        EditorTitle = "新建版本";
        EditorMeta = "提交后生成记录编号";
        EditVersion = "";
        EditLines = "";
        EditDownloadUrl = "";
        EditMandatory = false;
        EditEnabled = true;
        IsEditing = true;
        _snapshot = Capture();
        SetMode(EditorMode.New);
        StatusText = "正在新建";
        IsDrawerOpen = true;
    }

    private void ShowEmpty()
    {
        EditorTitle = "编辑版本";
        EditorMeta = "";
        EditVersion = "";
        EditLines = "";
        EditDownloadUrl = "";
        EditMandatory = false;
        EditEnabled = true;
        IsEditing = false;
        _snapshot = Capture();
        SetMode(EditorMode.Empty);
    }

    private string? Validate(out AppReleaseDraft draft)
    {
        draft = null!;
        if (!VersionNumber.TryNormalize(EditVersion, out var version, out var normalized))
            return "版本号无效，请使用如 2026.1.8.0";

        var selfId = _mode == EditorMode.Edit ? _currentSelection?.Record.ObjectId : null;
        if (Releases.Any(item => item.Version is not null && item.Version == version && item.Record.ObjectId != selfId))
            return "版本号已存在";

        var url = EditDownloadUrl.Trim();
        if (url.Length > 0 && !IsHttpUrl(url))
            return "下载地址必须是 http 或 https";

        var lines = SplitLines(EditLines);
        draft = new AppReleaseDraft
        {
            Version = normalized,
            Lines = lines,
            Notes = string.Join('\n', lines),
            DownloadUrl = url,
            Mandatory = EditMandatory,
            Enabled = EditEnabled
        };
        return null;
    }

    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!IsDirty())
            return true;

        return await ConfirmAsync("Versioner", "有未保存的修改，确定放弃吗？");
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        if (_dialogOpen)
            return false;

        _dialogOpen = true;
        try
        {
            var result = await DialogHost.Show(new ConfirmPrompt { Title = title, Message = message }, "RootDialog");
            return result is true || (result is string text && bool.TryParse(text, out var accepted) && accepted);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "显示确认对话框失败");
            return false;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private bool IsDirty()
    {
        var current = Capture();
        return Norm(current.Version) != Norm(_snapshot.Version)
               || Norm(current.Lines) != Norm(_snapshot.Lines)
               || Norm(current.DownloadUrl) != Norm(_snapshot.DownloadUrl)
               || current.Mandatory != _snapshot.Mandatory
               || current.Enabled != _snapshot.Enabled;
    }

    private EditorState Capture() => new(EditVersion, EditLines, EditDownloadUrl, EditMandatory, EditEnabled);

    private void SetMode(EditorMode mode)
    {
        _mode = mode;
        DeleteCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static List<string> SplitLines(string text)
    {
        var parts = Norm(text).Split('\n').Select(line => line.Trim()).ToList();
        while (parts.Count > 0 && parts[0].Length == 0)
            parts.RemoveAt(0);
        while (parts.Count > 0 && parts[^1].Length == 0)
            parts.RemoveAt(parts.Count - 1);
        return parts;
    }

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string Norm(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private readonly record struct EditorState(
        string Version,
        string Lines,
        string DownloadUrl,
        bool Mandatory,
        bool Enabled);

    private enum EditorMode
    {
        Empty,
        New,
        Edit
    }
}

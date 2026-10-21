using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Versioner.Helpers;
using Versioner.ViewModels;

namespace Versioner.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DpiTextRenderingHelper.Attach(this);
        DataContext = viewModel;
        viewModel.RequiredFieldsMissing += OnRequiredFieldsMissing;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;

        UpdateTheme();
        UpdateIcon();
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category != UserPreferenceCategory.General) return;
            Dispatcher.BeginInvoke(() =>
            {
                UpdateTheme();
                UpdateIcon();
            });
        };

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var name = GetType().Assembly.GetName().Name;
        if (version is null) return;
        var versionText = version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : version.Build > 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
        Title = $"{name} v{versionText}";
    }

    private void OnRequiredFieldsMissing(bool versionMissing, bool notesMissing)
    {
        if (versionMissing)
            Flash(VersionBox);
        if (notesMissing)
            Flash(NotesBox);

        var first = versionMissing ? VersionBox : NotesBox;
        first.BringIntoView();
        first.Focus();
    }

    private static void Flash(TextBox box)
    {
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0.25,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2),
            FillBehavior = FillBehavior.Stop
        };
        box.BeginAnimation(OpacityProperty, animation);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        EnsureLoadingLottie();
        if (_viewModel.RefreshCommand.CanExecute(null))
            await _viewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsBusy))
            SyncLoadingAnimation();
    }

    private void StartLoadingAnimation()
    {
        if (!_viewModel.IsBusy)
            return;

        EnsureLoadingLottie();
        if (!string.IsNullOrEmpty(LoadingLottie.FileName))
            LoadingLottie.PlayAnimation();
    }

    private void EnsureLoadingLottie()
    {
        if (!string.IsNullOrEmpty(LoadingLottie.FileName))
            return;

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "fish.json");
        if (File.Exists(path))
            LoadingLottie.FileName = path;
    }

    private void SyncLoadingAnimation()
    {
        if (_viewModel.IsBusy)
        {
            // 遮罩还没变成 Visible 时 PlayAnimation 会直接返回，等布局完成后再播。
            Dispatcher.BeginInvoke(StartLoadingAnimation, DispatcherPriority.Loaded);
            return;
        }

        if (!string.IsNullOrEmpty(LoadingLottie.FileName))
            LoadingLottie.StopAnimation();
    }

    private bool _allowClose;

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_allowClose || !_viewModel.HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (!await _viewModel.ConfirmDiscardAsync())
            return;

        _allowClose = true;
        Close();
    }

    private TextBox? _caretPressBox;
    private Point _caretPressPoint;

    private void FieldBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox box || box.Text.Length == 0)
            return;

        if (!box.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            box.Focus();
            MoveCaretToEnd(box);
            return;
        }

        _caretPressBox = box;
        _caretPressPoint = e.GetPosition(box);
    }

    private void FieldBox_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox box || box != _caretPressBox || box.Text.Length == 0)
            return;

        _caretPressBox = null;
        if (e.ClickCount > 1)
            return;

        var pos = e.GetPosition(box);
        if (Math.Abs(pos.X - _caretPressPoint.X) > 4 || Math.Abs(pos.Y - _caretPressPoint.Y) > 4)
            return;

        MoveCaretToEnd(box);
    }

    private static void MoveCaretToEnd(TextBox box)
    {
        box.CaretIndex = box.Text.Length;
        box.ScrollToEnd();
    }

    private void ReleaseList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView list || e.OriginalSource is not DependencyObject source)
            return;
        if (ItemsControl.ContainerFromElement(list, source) is ListViewItem)
            _viewModel.IsDrawerOpen = true;
    }

    private void Window_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedFile(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void Window_OnDrop(object sender, DragEventArgs e)
    {
        var path = GetDroppedFile(e);
        if (path is null)
            return;

        e.Handled = true;
        await _viewModel.BeginFromVersionFile(path);
    }

    private static string? GetDroppedFile(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return null;

        return e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
            ? files[0]
            : null;
    }

    private static void UpdateTheme()
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        switch (Theme.GetSystemTheme())
        {
            case BaseTheme.Light:
                theme.SetBaseTheme(BaseTheme.Light);
                break;
            case BaseTheme.Dark:
                theme.SetBaseTheme(BaseTheme.Dark);
                break;
        }

        paletteHelper.SetTheme(theme);
    }

    private void UpdateIcon()
    {
        var iconUri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.RelativeOrAbsolute);
        Icon = BitmapFrame.Create(iconUri);
    }
}

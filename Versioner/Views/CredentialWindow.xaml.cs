using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Versioner.Helpers;
using Versioner.Options;

namespace Versioner.Views;

public partial class CredentialWindow : Window
{
    private static readonly MethodInfo? PasswordSelect = typeof(PasswordBox).GetMethod(
        "Select",
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(int), typeof(int)],
        null);

    private Control? _caretPressField;
    private Point _caretPressPoint;

    public Back4AppOptions Result { get; private set; }

    public CredentialWindow(Back4AppOptions current)
    {
        InitializeComponent();
        DpiTextRenderingHelper.Attach(this);
        WindowCaptionTheme.Attach(this);
        Result = new Back4AppOptions
        {
            ApplicationId = current.ApplicationId,
            MasterKey = current.MasterKey,
            ServerUrl = current.ServerUrl
        };
        ApplicationIdBox.Text = current.ApplicationId;
        MasterKeyBox.Password = current.MasterKey;
        ServerUrlBox.Text = string.IsNullOrWhiteSpace(current.ServerUrl)
            ? "https://parseapi.back4app.com"
            : current.ServerUrl;
        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(ApplicationIdBox.Text))
                ApplicationIdBox.Focus();
            else
                MasterKeyBox.Focus();
        };
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var applicationId = ApplicationIdBox.Text.Trim();
        var masterKey = MasterKeyBox.Password.Trim();
        var serverUrl = ServerUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serverUrl))
            serverUrl = "https://parseapi.back4app.com";

        if (applicationId.Length == 0 || masterKey.Length == 0)
        {
            ShowError("请填写 Application Id 和 Master Key。");
            return;
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowError("Server URL 需要是 http 或 https 地址。");
            return;
        }

        Result = new Back4AppOptions
        {
            ApplicationId = applicationId,
            MasterKey = masterKey,
            ServerUrl = serverUrl
        };
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Field_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Control field || !HasFieldText(field) || IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        if (!field.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            field.Focus();
            MoveCaretToEnd(field);
            return;
        }

        _caretPressField = field;
        _caretPressPoint = e.GetPosition(field);
    }

    private void Field_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Control field || !ReferenceEquals(field, _caretPressField) || !HasFieldText(field))
            return;

        _caretPressField = null;
        if (e.ClickCount > 1 || IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        var pos = e.GetPosition(field);
        if (Math.Abs(pos.X - _caretPressPoint.X) > 4 || Math.Abs(pos.Y - _caretPressPoint.Y) > 4)
            return;

        MoveCaretToEnd(field);
    }

    private void Field_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not Control field || !HasFieldText(field) || IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        // 控件自己的 GotKeyboardFocus 可能随后把光标放到开头或全选，等它处理完再移到末尾。
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => MoveCaretToEnd(field));
    }

    private static bool HasFieldText(Control field) => field switch
    {
        TextBox box => box.Text.Length > 0,
        PasswordBox box => box.Password.Length > 0,
        _ => false
    };

    private static void MoveCaretToEnd(Control field)
    {
        switch (field)
        {
            case TextBox box:
                box.CaretIndex = box.Text.Length;
                box.ScrollToEnd();
                break;
            case PasswordBox box:
                PasswordSelect?.Invoke(box, [box.Password.Length, 0]);
                break;
        }
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ButtonBase)
                return true;
            if (source is TextBox or PasswordBox)
                return false;

            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }
}

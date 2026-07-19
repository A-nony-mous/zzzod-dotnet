using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Pages.ApplicationSettings;

internal sealed class ZzzRedemptionCodeRowModel : INotifyPropertyChanged
{
    private string _code;
    private string _endDateText;

    public ZzzRedemptionCodeRowModel(string code, int endDate, bool isReadOnly, bool isNew = false)
    {
        _code = code;
        _endDateText = endDate.ToString(CultureInfo.InvariantCulture);
        OriginalCode = code;
        IsReadOnly = isReadOnly;
        IsNew = isNew;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code
    {
        get => _code;
        set => SetField(ref _code, value);
    }

    public string EndDateText
    {
        get => _endDateText;
        set => SetField(ref _endDateText, value);
    }

    public string OriginalCode { get; private set; }

    public bool IsReadOnly { get; }

    public bool IsNew { get; private set; }

    public bool CanDelete => !IsReadOnly;

    public string Watermark => IsNew ? "请输入兑换码" : string.Empty;

    public void MarkSaved(string code)
    {
        OriginalCode = code;
        IsNew = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Watermark)));
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed partial class ZzzRedemptionCodeAppSettingPage : UserControl, IZzzPageLifecycle
{
    private const int EmptyEndDate = 20990101;
    private readonly IZzzRedemptionCodeBackend _backend;
    private readonly ObservableCollection<ZzzRedemptionCodeRowModel> _rows = [];
    private readonly ItemsControl _codeList;
    private readonly FAInfoBar _messageBar;

    public ZzzRedemptionCodeAppSettingPage(IZzzRedemptionCodeBackend backend)
    {
        _backend = backend;
        AvaloniaXamlLoader.Load(this);
        _codeList = Required<ItemsControl>("CodeList");
        _messageBar = Required<FAInfoBar>("MessageBar");
        _codeList.ItemsSource = _rows;
        Reload();
    }

    public void OnPageShown() => Reload();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage()
    {
    }

    internal IReadOnlyList<ZzzRedemptionCodeRowModel> Rows => _rows;

    internal static int CreateDefaultEndDate(DateTime now) =>
        int.Parse(now.AddDays(30).ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    internal ZzzRedemptionCodeRowModel AddRowForTest() => AddNewRow(focus: false);

    internal void CommitRowForTest(ZzzRedemptionCodeRowModel row) => CommitRow(row);

    internal void DeleteRowForTest(ZzzRedemptionCodeRowModel row) => DeleteRow(row);

    private void Reload()
    {
        ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> result = _backend.GetRedemptionCodes();
        if (!result.Success || result.Value is null)
        {
            ShowMessage(result.Error ?? "兑换码读取失败。", FAInfoBarSeverity.Error);
            return;
        }

        ApplyRows(result.Value);
        _messageBar.IsOpen = false;
    }

    private void ApplyRows(IReadOnlyList<ZzzRedemptionCodeDto> rows)
    {
        _rows.Clear();
        foreach (ZzzRedemptionCodeDto row in rows)
        {
            _rows.Add(new ZzzRedemptionCodeRowModel(row.Code, row.EndDate, row.ReadOnly));
        }
    }

    private void OnAddClicked(object? sender, RoutedEventArgs args) => AddNewRow(focus: true);

    private ZzzRedemptionCodeRowModel AddNewRow(bool focus)
    {
        var row = new ZzzRedemptionCodeRowModel(
            string.Empty,
            CreateDefaultEndDate(DateTime.Now),
            isReadOnly: false,
            isNew: true);
        _rows.Add(row);
        if (focus)
        {
            Dispatcher.UIThread.Post(() => FocusCodeInput(row));
        }

        return row;
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzRedemptionCodeRowModel row })
        {
            CommitRow(row);
        }
    }

    private void OnEndDateTextInput(object? sender, TextInputEventArgs args)
    {
        if (!string.IsNullOrEmpty(args.Text) && args.Text.Any(character => !char.IsDigit(character)))
        {
            args.Handled = true;
        }
    }

    private void CommitRow(ZzzRedemptionCodeRowModel row)
    {
        if (row.IsReadOnly)
        {
            return;
        }

        string code = row.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        int endDate = int.TryParse(row.EndDateText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : EmptyEndDate;
        ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> result = row.IsNew
            ? _backend.AddRedemptionCode(code, endDate)
            : _backend.UpdateRedemptionCode(row.OriginalCode, code, endDate);
        if (!result.Success || result.Value is null)
        {
            if (string.Equals(result.Error, "兑换码已存在", StringComparison.Ordinal))
            {
                if (row.IsNew)
                {
                    row.Code = string.Empty;
                    FocusCodeInput(row);
                }
                else
                {
                    row.Code = row.OriginalCode;
                }

                ShowMessage("兑换码已存在", FAInfoBarSeverity.Warning);
                return;
            }

            ShowMessage(result.Error ?? "兑换码保存失败。", FAInfoBarSeverity.Error);
            return;
        }

        row.MarkSaved(code);
        ApplyRows(result.Value);
        _messageBar.IsOpen = false;
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ZzzRedemptionCodeRowModel row })
        {
            DeleteRow(row);
        }
    }

    private void DeleteRow(ZzzRedemptionCodeRowModel row)
    {
        if (row.IsReadOnly)
        {
            return;
        }

        if (row.IsNew)
        {
            _rows.Remove(row);
            return;
        }

        ZzzBackendResult<IReadOnlyList<ZzzRedemptionCodeDto>> result = _backend.DeleteRedemptionCode(row.OriginalCode);
        if (!result.Success || result.Value is null)
        {
            ShowMessage(result.Error ?? "兑换码删除失败。", FAInfoBarSeverity.Error);
            return;
        }

        ApplyRows(result.Value);
        _messageBar.IsOpen = false;
    }

    private void FocusCodeInput(ZzzRedemptionCodeRowModel row)
    {
        TextBox? textBox = _codeList.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(input => ReferenceEquals(input.DataContext, row));
        textBox?.Focus(NavigationMethod.Unspecified);
    }

    private void ShowMessage(string message, FAInfoBarSeverity severity)
    {
        _messageBar.Title = string.Empty;
        _messageBar.Message = message;
        _messageBar.Severity = severity;
        _messageBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"兑换码设置缺少 {name}。");
}

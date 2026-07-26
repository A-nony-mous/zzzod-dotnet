using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

/// <summary>
/// 每日签到可选签到商店。
/// </summary>
internal sealed record ZzzDailySignInShopOption(string Label, string Value)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// 每日签到设置弹出框：选择代理签到的具体商店。
/// </summary>
internal sealed partial class FrontierDailySignInSettingsFlyoutContent : UserControl, IZzzPageLifecycle
{
    private const string ScopeName = "daily-signin";

    private static readonly ZzzDailySignInShopOption[] ShopOptions =
    {
        new("吼吼饼铺", "hou_hou_bakery"),
        new("卦象集录", "trigrams_collection"),
        new("刮刮卡", "scratch_card"),
    };

    private readonly IZzzAppBackend _backend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private readonly FAInfoBar _errorBar;
    private readonly FAComboBox _shopCombo;
    private bool _loading;

    public FrontierDailySignInSettingsFlyoutContent(
        IZzzAppBackend backend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _shopCombo = Required<FAComboBox>("ShopCombo");
        _shopCombo.ItemsSource = ShopOptions;
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

    internal bool SaveForTest(string key, string value) => Save(key, value);

    private void Reload()
    {
        _loading = true;
        _errorBar.IsOpen = false;
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.GetConfigScope(ScopeName, _instanceIndex, _groupId);
        if (!result.Success || result.Value is null)
        {
            ShowError(result.Error ?? "每日签到配置读取失败。");
            _loading = false;
            return;
        }

        string selected = RequiredString(result.Value.Values, "selected_sign");
        _shopCombo.SelectedItem = ShopOptions.FirstOrDefault(option =>
            string.Equals(option.Value, selected, StringComparison.Ordinal));
        _loading = false;
    }

    private void OnShopChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (!_loading && _shopCombo.SelectedItem is ZzzDailySignInShopOption option)
        {
            Save("selected_sign", option.Value);
        }
    }

    private bool Save(string key, string value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new ZzzSaveConfigScopeRequest(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        if (result.Success)
        {
            _errorBar.IsOpen = false;
            return true;
        }

        ShowError(result.Error ?? $"{key} 保存失败。");
        return false;
    }

    private void ShowError(string message)
    {
        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.IsOpen = true;
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"每日签到配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"每日签到设置缺少 {name}。");
}

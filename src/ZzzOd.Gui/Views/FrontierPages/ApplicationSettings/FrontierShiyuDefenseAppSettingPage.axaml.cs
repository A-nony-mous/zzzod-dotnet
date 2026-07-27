using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.GameData;
using ZzzOd.Gui.Shell;

namespace ZzzOd.Gui.Views.FrontierPages.ApplicationSettings;

internal sealed partial class ZzzShiyuDefenseTeamRowModel : ObservableObject
{
    private readonly Action<int, DmgTypeEnum?, bool> _onChanged;
    private bool _forCritical;
    private bool _electric;
    private bool _ether;
    private bool _physical;
    private bool _fire;
    private bool _ice;
    private bool _wind;

    public ZzzShiyuDefenseTeamRowModel(
        int teamIndex,
        string teamName,
        string autoBattleConfig,
        bool forCritical,
        bool electric,
        bool ether,
        bool physical,
        bool fire,
        bool ice,
        bool wind,
        Action<int, DmgTypeEnum?, bool> onChanged)
    {
        TeamIndex = teamIndex;
        TeamName = teamName;
        AutoBattleConfig = autoBattleConfig;
        _forCritical = forCritical;
        _electric = electric;
        _ether = ether;
        _physical = physical;
        _fire = fire;
        _ice = ice;
        _wind = wind;
        _onChanged = onChanged;
    }

    public int TeamIndex { get; }

    public string TeamName { get; }

    public string AutoBattleConfig { get; }

    public bool ForCritical
    {
        get => _forCritical;
        set
        {
            if (SetProperty(ref _forCritical, value))
            {
                _onChanged(TeamIndex, null, value);
            }
        }
    }

    public bool Electric
    {
        get => _electric;
        set => SetWeakness(ref _electric, DmgTypeEnum.ELECTRIC, value);
    }

    public bool Ether
    {
        get => _ether;
        set => SetWeakness(ref _ether, DmgTypeEnum.ETHER, value);
    }

    public bool Physical
    {
        get => _physical;
        set => SetWeakness(ref _physical, DmgTypeEnum.PHYSICAL, value);
    }

    public bool Fire
    {
        get => _fire;
        set => SetWeakness(ref _fire, DmgTypeEnum.FIRE, value);
    }

    public bool Ice
    {
        get => _ice;
        set => SetWeakness(ref _ice, DmgTypeEnum.ICE, value);
    }

    public bool Wind
    {
        get => _wind;
        set => SetWeakness(ref _wind, DmgTypeEnum.WIND, value);
    }

    public bool IsWeakness(DmgTypeEnum type) => type switch
    {
        DmgTypeEnum.ELECTRIC => Electric,
        DmgTypeEnum.ETHER => Ether,
        DmgTypeEnum.PHYSICAL => Physical,
        DmgTypeEnum.FIRE => Fire,
        DmgTypeEnum.ICE => Ice,
        DmgTypeEnum.WIND => Wind,
        _ => false,
    };

    private void SetWeakness(ref bool field, DmgTypeEnum type, bool value)
    {
        if (SetProperty(ref field, value))
        {
            _onChanged(TeamIndex, type, value);
        }
    }
}

internal sealed partial class FrontierShiyuDefenseAppSettingPage : UserControl, IZzzPageLifecycle
{
    private readonly ZzzShiyuDefenseAppSettingViewModel _viewModel;
    private readonly FAInfoBar _errorBar;

    public FrontierShiyuDefenseAppSettingPage(IZzzAppBackend backend, int instanceIndex, string groupId)
    {
        AvaloniaXamlLoader.Load(this);
        _errorBar = Required<FAInfoBar>("ErrorBar");
        _viewModel = new ZzzShiyuDefenseAppSettingViewModel(
            backend,
            instanceIndex,
            groupId,
            ShowError);
        DataContext = _viewModel;
        _viewModel.OnPageShown();
    }

    internal ZzzShiyuDefenseAppSettingViewModel State => _viewModel;

    public void OnPageShown() => _viewModel.OnPageShown();

    public void OnPageHidden()
    {
    }

    public void OnPageLeave()
    {
    }

    public void DisposePage() => _viewModel.DisposePage();

    public void ResetRunRecord() => _viewModel.ResetRunRecordForTest();

    private void ShowError(string? message)
    {
        if (message is null)
        {
            _errorBar.IsOpen = false;
            return;
        }

        _errorBar.Title = "错误";
        _errorBar.Message = message;
        _errorBar.Severity = FAInfoBarSeverity.Error;
        _errorBar.IsOpen = true;
    }

    private T Required<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"式舆防卫战设置缺少 {name}。");
}

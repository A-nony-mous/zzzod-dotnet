using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.Gui.Services.Config;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzLostVoidUiOption(string Label, object Value, string Description = "")
{
    public override string ToString() => Label;
}

internal sealed partial class ZzzLostVoidAppSettingViewModel : ZzzConfigSectionViewModel
{
    internal const string ScopeNameValue = "lost-void";

    private static readonly ZzzConfigField DailyPlanTimesField =
        new("daily_plan_times", typeof(int), 5);
    private static readonly ZzzConfigField WeeklyPlanTimesField =
        new("weekly_plan_times", typeof(int), 2);
    private static readonly ZzzConfigField ExtraTaskField =
        new("extra_task", typeof(string), LostVoidTask.BountyCommission);
    private static readonly ZzzConfigField MissionNameField =
        new("mission_name", typeof(string), "战线肃清");
    private static readonly ZzzConfigField ChallengeConfigNameField =
        new("challenge_config", typeof(string), "默认-成就模式");
    private static readonly IReadOnlyList<ZzzConfigField> FieldList =
    [
        DailyPlanTimesField,
        WeeklyPlanTimesField,
        ExtraTaskField,
        MissionNameField,
        ChallengeConfigNameField,
    ];

    private readonly IZzzLostVoidSettingsBackend _lostVoidBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private string? _persistedModuleName;

    public ZzzLostVoidAppSettingViewModel(
        IZzzAppBackend backend,
        IZzzLostVoidSettingsBackend lostVoidBackend,
        int instanceIndex,
        string groupId,
        Action<string?>? errorReporter = null)
        : base(backend, errorReporter)
    {
        _lostVoidBackend = lostVoidBackend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    protected override string ScopeName => ScopeNameValue;

    protected override IReadOnlyList<ZzzConfigField> Fields => FieldList;

    protected override int? InstanceIndex => _instanceIndex;

    protected override string? GroupId => _groupId;

    public double DailyPlanTimes
    {
        get => GetValue<int>(DailyPlanTimesField);
        set => SetValue(DailyPlanTimesField, Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    public double WeeklyPlanTimes
    {
        get => GetValue<int>(WeeklyPlanTimesField);
        set => SetValue(WeeklyPlanTimesField, Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    public string ExtraTask
    {
        get => GetValue<string>(ExtraTaskField);
        set
        {
            if (SetValue(ExtraTaskField, value))
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnPropertyChanged(nameof(WeeklyPlanTimesVisible));
            }
        }
    }

    public string MissionName
    {
        get => GetValue<string>(MissionNameField);
        set => SetValue(MissionNameField, value);
    }

    public string ChallengeConfigName
    {
        get => GetValue<string>(ChallengeConfigNameField);
        set => SetValue(ChallengeConfigNameField, value);
    }

    public ZzzLostVoidRunRecordDto? RunRecord { get; private set; }

    public IReadOnlyList<string> Missions { get; private set; } = [];

    public IReadOnlyList<string> ChallengeConfigNames { get; private set; } = [];

    public ZzzLostVoidChallengeCatalogDto? ChallengeCatalog { get; private set; }

    public ZzzLostVoidChallengeConfigDto? ChosenConfig { get; private set; }

    public string? Error => LastError;

    public IReadOnlyList<ZzzLostVoidUiOption> TaskOptions { get; } = LostVoidTask.Options
        .Select(option => new ZzzLostVoidUiOption(option.Label, option.Value ?? option.Label, option.Description))
        .ToArray();

    public IReadOnlyList<ZzzLostVoidUiOption> PeriodBuffOptions { get; } =
    [
        new(LostVoidPeriodBuffNo.No1, LostVoidPeriodBuffNo.No1),
        new(LostVoidPeriodBuffNo.No2, LostVoidPeriodBuffNo.No2),
        new(LostVoidPeriodBuffNo.No3, LostVoidPeriodBuffNo.No3),
    ];

    public IReadOnlyList<ZzzLostVoidUiOption> BuyPriorityOptions { get; } =
    [
        new("刷新0次", LostVoidBuyOnlyPriority.None),
        new("刷新1次(50硬币)", LostVoidBuyOnlyPriority.No1),
        new("刷新2次(100硬币)", LostVoidBuyOnlyPriority.No2),
        new("刷新3次(200硬币)", LostVoidBuyOnlyPriority.No3),
        new("刷新4次(300硬币)", LostVoidBuyOnlyPriority.No4),
        new("一直刷新", LostVoidBuyOnlyPriority.Always),
    ];

    public string RunRecordText => RunRecord switch
    {
        { BountyCommissionComplete: true } => "已完成悬赏委托 如错误可重置",
        { PeriodRewardComplete: true } => "已完成刷取周期奖励 如错误可重置",
        { EvalPointComplete: true } => "已完成刷取业绩 如错误可重置",
        { } record => $"通关次数 本日: {record.DailyRunTimes}, 本周: {record.WeeklyRunTimes}",
        _ => string.Empty,
    };

    public bool WeeklyPlanTimesVisible => string.Equals(
        ExtraTask,
        LostVoidTask.WeeklyPlanTimes,
        StringComparison.Ordinal);

    public ZzzLostVoidUiOption? SelectedTask
    {
        get => TaskOptions.FirstOrDefault(option => Equals(option.Value, ExtraTask));
        set
        {
            if (value is not null)
            {
                ExtraTask = Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    public override void OnPageShown()
    {
        base.OnPageShown();
        if (LastError is not null)
        {
            return;
        }

        ZzzBackendResult<ZzzLostVoidSettingsCatalogDto> catalog =
            _lostVoidBackend.GetLostVoidSettingsCatalog(_instanceIndex);
        if (!catalog.Success || catalog.Value is null)
        {
            ReportError(catalog.Error ?? "迷失之地目录读取失败。");
            return;
        }

        Missions = catalog.Value.Missions;
        ChallengeConfigNames = catalog.Value.ChallengeConfigs;
        RunRecord = catalog.Value.RunRecord;
        OnPropertyChanged(nameof(Missions));
        OnPropertyChanged(nameof(ChallengeConfigNames));
        OnPropertyChanged(nameof(RunRecord));
        OnPropertyChanged(nameof(RunRecordText));
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(WeeklyPlanTimesVisible));
        ReportError(null);
    }

    public bool ReloadBase()
    {
        OnPageShown();
        return LastError is null;
    }

    public bool ReloadChallengeCatalog()
    {
        ZzzBackendResult<ZzzLostVoidChallengeCatalogDto> result =
            _lostVoidBackend.GetLostVoidChallengeCatalog(_instanceIndex);
        if (!result.Success || result.Value is null)
        {
            return Fail(result.Error ?? "挑战配置目录读取失败。");
        }

        ChallengeCatalog = result.Value;
        ReportError(null);
        return true;
    }

    public bool SaveBase(string key, object value)
    {
        switch (key)
        {
            case "daily_plan_times": DailyPlanTimes = Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            case "weekly_plan_times": WeeklyPlanTimes = Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            case "extra_task": ExtraTask = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "mission_name": MissionName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            case "challenge_config": ChallengeConfigName = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; break;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, "未知的迷失之地配置字段。");
        }

        return LastError is null;
    }

    public bool ResetRunRecord()
    {
        ZzzBackendResult<ZzzLostVoidRunRecordDto> result =
            _lostVoidBackend.ResetLostVoidRunRecord(_instanceIndex);
        if (!result.Success || result.Value is null)
        {
            return Fail(result.Error ?? "运行记录重置失败。");
        }

        RunRecord = result.Value;
        ReportError(null);
        OnPropertyChanged(nameof(RunRecordText));
        return true;
    }

    public bool ChooseConfig(string moduleName)
    {
        ZzzBackendResult<ZzzLostVoidChallengeConfigDto> result =
            _lostVoidBackend.GetLostVoidChallengeConfig(moduleName);
        if (!result.Success || result.Value is null)
        {
            return Fail(result.Error ?? "挑战配置读取失败。");
        }

        ChosenConfig = result.Value;
        _persistedModuleName = result.Value.ModuleName;
        ReportError(null);
        return true;
    }

    public bool CreateConfig() => ApplyDraft(_lostVoidBackend.CreateLostVoidChallengeConfigDraft());

    public bool CopyConfig()
    {
        if (ChosenConfig is null)
        {
            return false;
        }

        if (!ChosenConfig.Exists)
        {
            ChosenConfig = ChosenConfig with { ModuleName = $"{ChosenConfig.ModuleName}_copy" };
            _persistedModuleName = null;
            ReportError(null);
            return true;
        }

        return ApplyDraft(_lostVoidBackend.CopyLostVoidChallengeConfigDraft(ChosenConfig.ModuleName));
    }

    public void CloseConfig()
    {
        ChosenConfig = null;
        _persistedModuleName = null;
        ReportError(null);
    }

    public bool DeleteConfig()
    {
        if (ChosenConfig is null || ChosenConfig.IsSample)
        {
            return false;
        }

        ZzzBackendResult<bool> result = _lostVoidBackend.DeleteLostVoidChallengeConfig(ChosenConfig.ModuleName);
        if (!result.Success)
        {
            return Fail(result.Error ?? "挑战配置删除失败。");
        }

        CloseConfig();
        return ReloadChallengeCatalog();
    }

    public bool UpdateConfig(Func<ZzzLostVoidChallengeConfigDto, ZzzLostVoidChallengeConfigDto> update)
    {
        if (ChosenConfig is null || ChosenConfig.IsSample)
        {
            return false;
        }

        ZzzLostVoidChallengeConfigDto changed = update(ChosenConfig);
        ZzzBackendResult<ZzzLostVoidChallengeConfigDto> result =
            _lostVoidBackend.SaveLostVoidChallengeConfig(new(_persistedModuleName, changed));
        if (!result.Success || result.Value is null)
        {
            return Fail(result.Error ?? "挑战配置保存失败。");
        }

        ChosenConfig = result.Value;
        _persistedModuleName = result.Value.ModuleName;
        ReportError(null);
        ReloadChallengeCatalog();
        return true;
    }

    public bool UpdatePriority(ZzzLostVoidPriorityKind kind, string text)
    {
        ZzzBackendResult<ZzzLostVoidPriorityParseDto> parsed =
            _lostVoidBackend.ParseLostVoidPriority(kind, text);
        if (!parsed.Success || parsed.Value is null)
        {
            return Fail(parsed.Error ?? "优先级输入校验失败。");
        }

        string validationError = parsed.Value.ErrorMessage;
        bool saved = UpdateConfig(config => kind switch
        {
            ZzzLostVoidPriorityKind.ArtifactPriority => config with { ArtifactPriority = parsed.Value.Items },
            ZzzLostVoidPriorityKind.ArtifactPriority2 => config with { ArtifactPriority2 = parsed.Value.Items },
            _ => config with { RegionTypePriority = parsed.Value.Items },
        });
        if (saved && !string.IsNullOrWhiteSpace(validationError))
        {
            ReportError(validationError);
        }

        return saved;
    }

    private bool ApplyDraft(ZzzBackendResult<ZzzLostVoidChallengeConfigDto> result)
    {
        if (!result.Success || result.Value is null)
        {
            return Fail(result.Error ?? "挑战配置草稿创建失败。");
        }

        ChosenConfig = result.Value;
        _persistedModuleName = null;
        ReportError(null);
        return true;
    }

    private bool Fail(string message)
    {
        ReportError(message);
        return false;
    }

    [RelayCommand]
    private void ResetRunRecordAction() => ResetRunRecord();
}

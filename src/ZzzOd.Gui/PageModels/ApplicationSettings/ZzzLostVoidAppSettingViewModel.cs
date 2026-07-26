using System.Globalization;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;

namespace ZzzOd.Gui.PageModels.ApplicationSettings;

internal sealed record ZzzLostVoidUiOption(string Label, object Value, string Description = "")
{
    public override string ToString() => Label;
}

internal sealed class ZzzLostVoidAppSettingViewModel
{
    internal const string ScopeName = "lost-void";

    private readonly IZzzAppBackend _backend;
    private readonly IZzzLostVoidSettingsBackend _lostVoidBackend;
    private readonly int _instanceIndex;
    private readonly string _groupId;
    private string? _persistedModuleName;

    public ZzzLostVoidAppSettingViewModel(
        IZzzAppBackend backend,
        IZzzLostVoidSettingsBackend lostVoidBackend,
        int instanceIndex,
        string groupId)
    {
        _backend = backend;
        _lostVoidBackend = lostVoidBackend;
        _instanceIndex = instanceIndex;
        _groupId = groupId;
    }

    public int DailyPlanTimes { get; private set; }

    public int WeeklyPlanTimes { get; private set; }

    public string ExtraTask { get; private set; } = string.Empty;

    public string MissionName { get; private set; } = string.Empty;

    public string ChallengeConfigName { get; private set; } = string.Empty;

    public ZzzLostVoidRunRecordDto? RunRecord { get; private set; }

    public IReadOnlyList<string> Missions { get; private set; } = [];

    public IReadOnlyList<string> ChallengeConfigNames { get; private set; } = [];

    public ZzzLostVoidChallengeCatalogDto? ChallengeCatalog { get; private set; }

    public ZzzLostVoidChallengeConfigDto? ChosenConfig { get; private set; }

    public string? Error { get; private set; }

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

    public bool ReloadBase()
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> config = _backend.GetConfigScope(
            ScopeName,
            _instanceIndex,
            _groupId);
        if (!config.Success || config.Value is null)
        {
            return Fail(config.Error ?? "迷失之地配置读取失败。");
        }

        ZzzBackendResult<ZzzLostVoidSettingsCatalogDto> catalog =
            _lostVoidBackend.GetLostVoidSettingsCatalog(_instanceIndex);
        if (!catalog.Success || catalog.Value is null)
        {
            return Fail(catalog.Error ?? "迷失之地目录读取失败。");
        }

        try
        {
            IReadOnlyDictionary<string, object?> values = config.Value.Values;
            DailyPlanTimes = RequiredInt(values, "daily_plan_times");
            WeeklyPlanTimes = RequiredInt(values, "weekly_plan_times");
            ExtraTask = RequiredString(values, "extra_task");
            MissionName = RequiredString(values, "mission_name");
            ChallengeConfigName = RequiredString(values, "challenge_config");
            Missions = catalog.Value.Missions;
            ChallengeConfigNames = catalog.Value.ChallengeConfigs;
            RunRecord = catalog.Value.RunRecord;
            Error = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            return Fail(exception.Message);
        }
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
        Error = null;
        return true;
    }

    public bool SaveBase(string key, object value)
    {
        ZzzBackendResult<ZzzConfigScopeValuesDto> result = _backend.SaveConfigScope(new(
            ScopeName,
            new Dictionary<string, object?> { [key] = value },
            _instanceIndex,
            _groupId));
        if (!result.Success)
        {
            return Fail(result.Error ?? $"{key} 保存失败。");
        }

        if (key == "extra_task")
        {
            ExtraTask = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        Error = null;
        return true;
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
        Error = null;
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
        Error = null;
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
            Error = null;
            return true;
        }

        return ApplyDraft(_lostVoidBackend.CopyLostVoidChallengeConfigDraft(ChosenConfig.ModuleName));
    }

    public void CloseConfig()
    {
        ChosenConfig = null;
        _persistedModuleName = null;
        Error = null;
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
        Error = null;
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
            Error = validationError;
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
        Error = null;
        return true;
    }

    private bool Fail(string message)
    {
        Error = message;
        return false;
    }

    private static int RequiredInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"迷失之地配置缺少 {key}。");
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string RequiredString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? value))
        {
            throw new InvalidOperationException($"迷失之地配置缺少 {key}。");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}

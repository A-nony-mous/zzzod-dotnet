using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Services.Home;

public sealed class ZzzDashboardReadinessService
{
    private readonly IZzzAppBackend _backend;
    private readonly string _runRoot;

    public ZzzDashboardReadinessService(IZzzAppBackend backend, ZzzRunRoot runRoot)
    {
        _backend = backend;
        _runRoot = runRoot.Path;
    }

    public ZzzDashboardReadinessResult Check()
    {
        List<ZzzDashboardReadinessIssue> issues = [];
        ZzzBackendResult<ZzzConfigScopeValuesDto> account = _backend.GetConfigScope("instance");
        string gamePath = ReadString(account, "game_path");
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            issues.Add(new ZzzDashboardReadinessIssue(
                "未设置游戏路径 - 请在「账户管理 → 多账户管理 → 当前账户设置」中配置",
                "accounts"));
        }

        ZzzBackendResult<ZzzConfigScopeValuesDto> model = _backend.GetConfigScope("model");
        string modelName = ReadString(model, "flash_classifier");
        string modelDirectory = Path.Combine(_runRoot, "assets", "models", "flash_classifier", modelName);
        if (string.IsNullOrWhiteSpace(modelName)
            || !File.Exists(Path.Combine(modelDirectory, "labels.csv"))
            || !File.Exists(Path.Combine(modelDirectory, "model.onnx")))
        {
            issues.Add(new ZzzDashboardReadinessIssue(
                "闪光识别模型未下载 - 请在「设置 → 资源下载」中下载",
                "settings-resource-download"));
        }

        return new ZzzDashboardReadinessResult(issues.Count == 0, issues);
    }

    private static string ReadString(ZzzBackendResult<ZzzConfigScopeValuesDto> result, string key) =>
        result.Success && result.Value is not null && result.Value.Values.TryGetValue(key, out object? value)
            ? value?.ToString()?.Trim() ?? string.Empty
            : string.Empty;
}

public sealed record ZzzDashboardReadinessResult(bool Ready, IReadOnlyList<ZzzDashboardReadinessIssue> Issues);

public sealed record ZzzDashboardReadinessIssue(string Message, string TargetNavigationKey);

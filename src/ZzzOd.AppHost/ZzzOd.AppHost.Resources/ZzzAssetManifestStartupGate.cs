using System.Runtime.InteropServices;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 为宿主启动入口提供统一的资源清单校验门禁。
/// </summary>
public static class ZzzAssetManifestStartupGate
{
    /// <summary>
    /// 校验当前进程运行根的资源清单。
    /// </summary>
    /// <param name="runRoot">已解析的运行根目录。</param>
    /// <returns>资源清单校验结果。</returns>
    public static ZzzAssetManifestValidationResult Validate(string runRoot) =>
        new ZzzAssetManifestValidator().Validate(runRoot, RuntimeInformation.RuntimeIdentifier);

    /// <summary>
    /// 将已通过校验的结果转换为宿主可使用的运行根。
    /// </summary>
    /// <param name="validationResult">已经执行的资源清单校验结果。</param>
    /// <returns>规范化运行根及资源清单摘要。</returns>
    public static ZzzValidatedRunRoot CreateValidatedRunRoot(ZzzAssetManifestValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException("资源清单校验未通过，不能创建运行时上下文。");
        }

        return new ZzzValidatedRunRoot(
            Path.GetFullPath(validationResult.RunRoot),
            validationResult.SourceSummary);
    }

    /// <summary>
    /// 将全部校验问题写入宿主标准错误输出。
    /// </summary>
    /// <param name="issues">待输出的问题。</param>
    /// <param name="error">宿主标准错误输出。</param>
    public static void WriteIssues(IReadOnlyList<ZzzAssetManifestIssue> issues, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(error);
        foreach (ZzzAssetManifestIssue issue in issues)
        {
            error.WriteLine($"{issue.Code}: {issue.Path} {issue.Message}");
        }
    }
}

/// <summary>
/// 已通过资源清单校验的运行根。
/// </summary>
/// <param name="Path">规范化运行根目录。</param>
/// <param name="ManifestSourceSummary">资源清单来源摘要。</param>
public sealed record ZzzValidatedRunRoot(string Path, string ManifestSourceSummary);

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

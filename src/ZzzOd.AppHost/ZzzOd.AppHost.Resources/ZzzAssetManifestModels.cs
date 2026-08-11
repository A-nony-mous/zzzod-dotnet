using System.Text.Json.Serialization;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// staging 根目录中的资源清单。
/// </summary>
public sealed class ZzzAssetManifest
{
    /// <summary>
    /// 当前支持的清单版本。
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// 清单版本。
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    /// <summary>
    /// 目标运行时标识符。
    /// </summary>
    [JsonPropertyName("rid")]
    public string Rid { get; init; } = string.Empty;

    /// <summary>
    /// 生成来源标识。
    /// </summary>
    [JsonPropertyName("generatedSource")]
    public string GeneratedSource { get; init; } = string.Empty;

    /// <summary>
    /// 按资源集合生成的摘要。
    /// </summary>
    [JsonPropertyName("sourceSummary")]
    public string SourceSummary { get; init; } = string.Empty;

    /// <summary>
    /// 需要反向枚举的受管理目录。
    /// </summary>
    [JsonPropertyName("managedRoots")]
    public IReadOnlyList<string> ManagedRoots { get; init; } = [];

    /// <summary>
    /// 不参与受管理文件集合比较的可变路径模式。
    /// </summary>
    [JsonPropertyName("mutablePaths")]
    public IReadOnlyList<string> MutablePaths { get; init; } = [];

    /// <summary>
    /// 受管理资源文件。
    /// </summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<ZzzAssetManifestFile> Files { get; init; } = [];
}

/// <summary>
/// 资源清单文件项。
/// </summary>
public sealed class ZzzAssetManifestFile
{
    /// <summary>
    /// 相对 staging 根目录的 POSIX 路径。
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// 资源分类。
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 是否为启动必需资源。
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>
    /// 此文件适用的运行时标识符。
    /// </summary>
    [JsonPropertyName("rids")]
    public IReadOnlyList<string> Rids { get; init; } = [];

    /// <summary>
    /// 文件字节数。
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// 大写十六进制 SHA-256。
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>
    /// 生成来源标识。
    /// </summary>
    [JsonPropertyName("generatedSource")]
    public string GeneratedSource { get; init; } = string.Empty;
}

/// <summary>
/// 单个资源清单校验问题。
/// </summary>
/// <param name="Code">稳定问题代码。</param>
/// <param name="Path">相关相对路径。</param>
/// <param name="Message">中文问题说明。</param>
public sealed record ZzzAssetManifestIssue(ZzzAssetManifestIssueCode Code, string Path, string Message);

/// <summary>
/// 资源清单校验结果。
/// </summary>
/// <param name="RunRoot">参与校验的规范化运行根目录。</param>
/// <param name="Manifest">已解析清单，解析失败时为 null。</param>
/// <param name="Issues">全部校验问题。</param>
public sealed record ZzzAssetManifestValidationResult(
    string RunRoot,
    ZzzAssetManifest? Manifest,
    IReadOnlyList<ZzzAssetManifestIssue> Issues)
{
    /// <summary>
    /// 清单是否完整有效。
    /// </summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>
    /// 清单来源摘要。
    /// </summary>
    public string SourceSummary => Manifest?.SourceSummary ?? string.Empty;
}

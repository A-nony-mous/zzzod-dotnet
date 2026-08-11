namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 资源清单校验问题代码。
/// </summary>
public enum ZzzAssetManifestIssueCode
{
    /// <summary>
    /// 清单文件不存在。
    /// </summary>
    ManifestMissing,

    /// <summary>
    /// 清单 JSON 无法解析。
    /// </summary>
    ManifestInvalidJson,

    /// <summary>
    /// 清单版本不受支持。
    /// </summary>
    UnsupportedSchema,

    /// <summary>
    /// 清单目标 RID 不匹配。
    /// </summary>
    RidMismatch,

    /// <summary>
    /// 清单路径无效。
    /// </summary>
    InvalidPath,

    /// <summary>
    /// 清单条目重复。
    /// </summary>
    DuplicatePath,

    /// <summary>
    /// 清单存在仅大小写不同的路径。
    /// </summary>
    CaseConflict,

    /// <summary>
    /// 清单包含聚合 YAML。
    /// </summary>
    AggregatedYaml,

    /// <summary>
    /// 清单文件不存在。
    /// </summary>
    FileMissing,

    /// <summary>
    /// 清单文件大小不一致。
    /// </summary>
    SizeMismatch,

    /// <summary>
    /// 清单文件哈希不一致。
    /// </summary>
    Sha256Mismatch,

    /// <summary>
    /// 受管理目录存在未声明文件。
    /// </summary>
    ExtraManagedFile,

    /// <summary>
    /// 清单分类无效。
    /// </summary>
    UnknownCategory,
}

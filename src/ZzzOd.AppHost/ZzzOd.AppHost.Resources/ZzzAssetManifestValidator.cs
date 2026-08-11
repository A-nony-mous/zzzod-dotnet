using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using OneDragon.Core.Runtime;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 校验 staging 运行根中的资源清单和受管理文件集合。
/// </summary>
public sealed class ZzzAssetManifestValidator
{
    private const string ManifestFileName = "assets-manifest.json";
    private const int HashBufferSize = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 校验运行根中的资源清单。
    /// </summary>
    /// <param name="runRoot">运行根目录。</param>
    /// <param name="expectedRid">调用方要求的目标 RID，可为空。</param>
    /// <param name="expectedSourceSummary">调用方要求的来源摘要，可为空。</param>
    /// <returns>校验结果。</returns>
    public ZzzAssetManifestValidationResult Validate(
        string runRoot,
        string? expectedRid = null,
        string? expectedSourceSummary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRoot);
        string fullRunRoot = Path.GetFullPath(runRoot);
        List<ZzzAssetManifestIssue> issues = [];
        string manifestPath = Path.Combine(fullRunRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ManifestMissing, ManifestFileName, "未找到资源清单。"));
            return new ZzzAssetManifestValidationResult(fullRunRoot, null, issues);
        }

        ZzzAssetManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ZzzAssetManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ManifestInvalidJson, ManifestFileName, $"资源清单 JSON 无法解析: {ex.Message}"));
            return new ZzzAssetManifestValidationResult(fullRunRoot, null, issues);
        }

        if (manifest is null)
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ManifestInvalidJson, ManifestFileName, "资源清单内容为空。"));
            return new ZzzAssetManifestValidationResult(fullRunRoot, null, issues);
        }

        ValidateManifestHeader(manifest, expectedRid, expectedSourceSummary, issues);
        Dictionary<string, ZzzAssetManifestFile> declaredFiles = ValidateDeclaredFiles(fullRunRoot, manifest, issues);
        ValidateManagedFileSet(fullRunRoot, manifest, declaredFiles, issues);
		ValidateIndependentGameConfigs(fullRunRoot, manifest, issues);
        return new ZzzAssetManifestValidationResult(fullRunRoot, manifest, issues);
    }

    private static void ValidateManifestHeader(
        ZzzAssetManifest manifest,
        string? expectedRid,
		string? expectedSourceSummary,
        ICollection<ZzzAssetManifestIssue> issues)
    {
        if (manifest.SchemaVersion != ZzzAssetManifest.CurrentSchemaVersion)
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.UnsupportedSchema, ManifestFileName, $"不支持的资源清单版本: {manifest.SchemaVersion}。"));
        }

        if (string.IsNullOrWhiteSpace(manifest.Rid))
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.RidMismatch, ManifestFileName, "资源清单缺少目标 RID。"));
        }
        else if (!string.IsNullOrWhiteSpace(expectedRid) &&
                 !string.Equals(manifest.Rid, expectedRid, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.RidMismatch, ManifestFileName, $"资源清单 RID 为 {manifest.Rid}，调用方要求 {expectedRid}。"));
        }

		if (!string.IsNullOrWhiteSpace(expectedSourceSummary) &&
			!string.Equals(manifest.SourceSummary, expectedSourceSummary, StringComparison.Ordinal))
		{
			issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.SourceSummaryMismatch, ManifestFileName, "资源清单来源摘要与调用方要求不一致。"));
		}

        string[] paths = manifest.Files.Select(file => file.Path).ToArray();
        string[] ordinalSorted = paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (!paths.SequenceEqual(ordinalSorted, StringComparer.Ordinal))
        {
            issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.InvalidPath, ManifestFileName, "资源清单 files 未按 Ordinal 路径排序。"));
        }
    }

    private static Dictionary<string, ZzzAssetManifestFile> ValidateDeclaredFiles(
        string runRoot,
        ZzzAssetManifest manifest,
        ICollection<ZzzAssetManifestIssue> issues)
    {
        Dictionary<string, ZzzAssetManifestFile> declaredFiles = new(StringComparer.Ordinal);
        Dictionary<string, string> caseInsensitivePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (ZzzAssetManifestFile file in manifest.Files)
        {
            string path = file.Path ?? string.Empty;
            if (!IsValidRelativePath(path))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.InvalidPath, path, "资源清单路径必须是无 .. 的 POSIX 相对路径。"));
                continue;
            }

            if (IsAggregatedYaml(path))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.AggregatedYaml, path, "聚合 YAML 不允许进入资源清单。"));
            }

            if (IsExcludedRuntimePath(path))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ExcludedRuntimeFile, path, "运行态缓存、日志、用户数据和 custom 文件不允许进入资源清单。"));
            }

            if (string.IsNullOrWhiteSpace(file.Category))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.UnknownCategory, path, "资源清单文件缺少分类。"));
            }

            if (!declaredFiles.TryAdd(path, file))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.DuplicatePath, path, "资源清单包含重复路径。"));
                continue;
            }

            if (caseInsensitivePaths.TryGetValue(path, out string? existingPath) &&
                !string.Equals(existingPath, path, StringComparison.Ordinal))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.CaseConflict, path, $"资源清单路径仅大小写不同: {existingPath} 与 {path}。"));
                continue;
            }

            caseInsensitivePaths[path] = path;
            if (file.Rids.Count == 0 || !file.Rids.Contains(manifest.Rid, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.RidMismatch, path, "文件条目不适用于资源清单 RID。"));
            }

            string absolutePath = GetPathUnderRunRoot(runRoot, path);
            if (!File.Exists(absolutePath))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.FileMissing, path, "资源清单文件不存在。"));
                continue;
            }

            FileInfo info = new(absolutePath);
            if (info.Length != file.Size)
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.SizeMismatch, path, $"资源清单大小为 {file.Size}，实际大小为 {info.Length}。"));
                continue;
            }

            string sha256 = CalculateSha256(absolutePath);
            if (!string.Equals(sha256, file.Sha256, StringComparison.Ordinal))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.Sha256Mismatch, path, "资源清单 SHA-256 与实际文件不一致。"));
            }
        }

        return declaredFiles;
    }

    private static void ValidateManagedFileSet(
        string runRoot,
        ZzzAssetManifest manifest,
        IReadOnlyDictionary<string, ZzzAssetManifestFile> declaredFiles,
        ICollection<ZzzAssetManifestIssue> issues)
    {
        foreach (string managedRoot in manifest.ManagedRoots)
        {
            if (!IsValidRelativePath(managedRoot))
            {
                issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.InvalidPath, managedRoot, "受管理目录必须是相对路径。"));
                continue;
            }

            string absoluteRoot = GetPathUnderRunRoot(runRoot, managedRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (string absolutePath in EnumerateFilesWithoutLinks(absoluteRoot))
            {
                string relativePath = NormalizePath(Path.GetRelativePath(runRoot, absolutePath));
                if (IsAggregatedYaml(relativePath))
                {
                    issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.AggregatedYaml, relativePath, "受管理目录中出现聚合 YAML。"));
                    continue;
                }

                if (IsExcludedRuntimePath(relativePath))
                {
                    issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ExcludedRuntimeFile, relativePath, "受管理目录中出现运行态缓存、日志、用户数据或 custom 文件。"));
                    continue;
                }

                if (IsMutablePath(relativePath, manifest.MutablePaths) && IsApprovedMutableRuntimePath(relativePath))
                {
                    continue;
                }

                if (!declaredFiles.ContainsKey(relativePath))
                {
                    issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ExtraManagedFile, relativePath, "受管理目录中出现未声明文件。"));
                }
            }
        }
    }

	private static void ValidateIndependentGameConfigs(
		string runRoot,
		ZzzAssetManifest manifest,
		ICollection<ZzzAssetManifestIssue> issues)
	{
		bool includesScreenInfo = manifest.Files.Any(file =>
			file.Path.StartsWith("assets/game_data/screen_info/", StringComparison.Ordinal));
		string[] autoBattleTemplates = manifest.Files
			.Select(file => file.Path)
			.Where(path => path.StartsWith("config/auto_battle/", StringComparison.Ordinal))
			.Select(path => path["config/auto_battle/".Length..])
			.Where(path => !path.Contains('/', StringComparison.Ordinal))
			.Select(GetAutoBattleTemplateName)
			.Where(name => name is not null)
			.Cast<string>()
			.Distinct(StringComparer.Ordinal)
			.ToArray();
		if (!includesScreenInfo && autoBattleTemplates.Length == 0)
		{
			return;
		}

		IReadOnlyList<ZzzGameConfigValidationIssue> configIssues = new ZzzGameConfigValidator().Validate(
			new OneDragonEnvironment(runRoot),
			includesScreenInfo,
			autoBattleTemplates);
		foreach (ZzzGameConfigValidationIssue issue in configIssues)
		{
			issues.Add(new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.GameConfigInvalid, issue.Path, issue.Message));
		}
	}

	private static string? GetAutoBattleTemplateName(string path)
	{
		if (path.EndsWith(".merged.yml", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		if (path.EndsWith(".sample.yml", StringComparison.OrdinalIgnoreCase))
		{
			return path[..^".sample.yml".Length];
		}
		return path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
			? path[..^".yml".Length]
			: null;
	}

    private static IEnumerable<string> EnumerateFilesWithoutLinks(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.TryPop(out string? current))
        {
            foreach (string directory in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                {
                    pending.Push(directory);
                }
            }

            foreach (string file in Directory.EnumerateFiles(current))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsMutablePath(string path, IReadOnlyList<string> patterns) =>
        patterns.Any(pattern => GlobMatches(path, pattern));

    private static bool GlobMatches(string path, string pattern)
    {
        string expression = Regex.Escape(NormalizePath(pattern))
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal)
            .Replace("\\?", "[^/]", StringComparison.Ordinal);
        return Regex.IsMatch(path, $"^{expression}$", RegexOptions.CultureInvariant);
    }

    private static bool IsValidRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !string.Equals(path, NormalizePath(path), StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(path) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part is "." or ".."))
        {
            return false;
        }

        return !path.Contains(':', StringComparison.Ordinal);
    }

    private static bool IsAggregatedYaml(string path) =>
        path.EndsWith("/_od_merged.yml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("/merged.yml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".merged.yml", StringComparison.OrdinalIgnoreCase);

    private static bool IsExcludedRuntimePath(string path)
    {
        string normalized = NormalizePath(path);
        string[] segments = normalized.Split('/');
        return normalized.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".cache", StringComparison.OrdinalIgnoreCase) ||
			(normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase) &&
			 segments.Skip(1).Any(segment => int.TryParse(segment, out _))) ||
            segments.Any(segment => segment.Equals("custom", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("cache", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("caches", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("uv_cache", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals(".install", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("log", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("app_run_record", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("account", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("accounts", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("user_data", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsApprovedMutableRuntimePath(string path)
    {
        string normalized = NormalizePath(path);
        string[] segments = normalized.Split('/');
        return normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase) &&
            (segments.Skip(1).Any(segment => int.TryParse(segment, out _)) ||
             normalized.Contains(".local.", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPathUnderRunRoot(string runRoot, string relativePath)
    {
        string fullRoot = runRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"资源路径越出运行根目录: {relativePath}");
        }

        return candidate;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string CalculateSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] buffer = new byte[HashBufferSize];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, bytesRead);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

using System.IO.Compression;
using System.Security.Cryptography;

namespace ZzzOd.AppHost.Resources;

/// <summary>
/// 按已验证资源清单生成发布 ZIP。
/// </summary>
public sealed class ZzzAssetManifestZipBuilder
{
	private const string ManifestFileName = "assets-manifest.json";

	private readonly ZzzAssetManifestValidator _manifestValidator;

	/// <summary>
	/// 初始化 ZIP 构建器。
	/// </summary>
	/// <param name="manifestValidator">资源清单校验器。</param>
	public ZzzAssetManifestZipBuilder(ZzzAssetManifestValidator? manifestValidator = null)
	{
		_manifestValidator = manifestValidator ?? new ZzzAssetManifestValidator();
	}

	/// <summary>
	/// 按 manifest 文件条目和显式发布条目生成 ZIP。
	/// </summary>
	/// <param name="stagingRoot">已生成 staging 根目录。</param>
	/// <param name="zipPath">目标 ZIP 路径。</param>
	/// <param name="releaseEntries">应用二进制等独立发布条目。</param>
	/// <returns>生成结果。</returns>
	public ZzzAssetManifestZipResult Create(
		string stagingRoot,
		string zipPath,
		IReadOnlyList<ZzzAssetManifestZipEntry>? releaseEntries = null)
	{
		ZzzAssetManifestValidationResult validation = _manifestValidator.Validate(stagingRoot);
		if (!validation.IsValid || validation.Manifest is null)
		{
			throw new InvalidOperationException("资源清单校验未通过，不能生成发布 ZIP。", new ZzzAssetManifestValidationException(validation.Issues));
		}

		string fullStagingRoot = validation.RunRoot;
		string fullZipPath = Path.GetFullPath(zipPath);
		string? zipDirectory = Path.GetDirectoryName(fullZipPath);
		if (string.IsNullOrWhiteSpace(zipDirectory))
		{
			throw new ArgumentException("目标 ZIP 路径缺少父目录。", nameof(zipPath));
		}

		IReadOnlyList<ZzzAssetManifestZipEntry> extras = releaseEntries ?? [];
		ValidateReleaseEntries(extras, validation.Manifest.Files.Select(file => file.Path));
		Directory.CreateDirectory(zipDirectory);
		string temporaryZipPath = fullZipPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream stream = new(temporaryZipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
			using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
			{
				foreach (ZzzAssetManifestFile file in validation.Manifest.Files)
				{
					AddFile(archive, GetPathUnderRoot(fullStagingRoot, file.Path), file.Path);
				}

				foreach (ZzzAssetManifestZipEntry entry in extras)
				{
					AddFile(archive, Path.GetFullPath(entry.SourcePath), entry.Path);
				}

				AddFile(archive, Path.Combine(fullStagingRoot, ManifestFileName), ManifestFileName);
			}

			ValidateCreatedZip(temporaryZipPath, validation.Manifest.Files, extras);
			File.Move(temporaryZipPath, fullZipPath, overwrite: true);
			return new ZzzAssetManifestZipResult(fullZipPath, validation.SourceSummary, validation.Manifest.Rid, validation.Manifest.Files.Count);
		}
		catch
		{
			if (File.Exists(temporaryZipPath))
			{
				File.Delete(temporaryZipPath);
			}

			throw;
		}
	}

	/// <summary>
	/// 校验 ZIP 中的资源条目是否仍与 staging manifest 一致。
	/// </summary>
	/// <param name="stagingRoot">已生成 staging 根目录。</param>
	/// <param name="zipPath">待校验 ZIP 路径。</param>
	/// <param name="releaseEntries">应用二进制等独立发布条目。</param>
	public void Validate(
		string stagingRoot,
		string zipPath,
		IReadOnlyList<ZzzAssetManifestZipEntry>? releaseEntries = null)
	{
		ZzzAssetManifestValidationResult validation = _manifestValidator.Validate(stagingRoot);
		if (!validation.IsValid || validation.Manifest is null)
		{
			throw new InvalidOperationException("资源清单校验未通过，ZIP 不可分发。", new ZzzAssetManifestValidationException(validation.Issues));
		}

		IReadOnlyList<ZzzAssetManifestZipEntry> extras = releaseEntries ?? [];
		ValidateReleaseEntries(extras, validation.Manifest.Files.Select(file => file.Path));
		if (!File.Exists(zipPath))
		{
			throw new FileNotFoundException("待校验 ZIP 不存在。", zipPath);
		}

		ValidateCreatedZip(Path.GetFullPath(zipPath), validation.Manifest.Files, extras);
	}

	private static void ValidateCreatedZip(
		string zipPath,
		IReadOnlyList<ZzzAssetManifestFile> manifestFiles,
		IReadOnlyList<ZzzAssetManifestZipEntry> extras)
	{
		using ZipArchive archive = ZipFile.OpenRead(zipPath);
		Dictionary<string, ZipArchiveEntry> entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.Ordinal);
		HashSet<string> expectedEntries = new(manifestFiles.Select(file => file.Path), StringComparer.Ordinal)
		{
			ManifestFileName,
		};
		foreach (ZzzAssetManifestZipEntry entry in extras)
		{
			expectedEntries.Add(entry.Path);
		}

		if (!expectedEntries.SetEquals(entries.Keys))
		{
			throw new InvalidOperationException("ZIP 条目集合与资源清单不一致。");
		}

		foreach (ZzzAssetManifestFile file in manifestFiles)
		{
			using Stream entryStream = entries[file.Path].Open();
			string sha256 = Convert.ToHexString(SHA256.HashData(entryStream));
			if (!string.Equals(sha256, file.Sha256, StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"ZIP 资源哈希不一致: {file.Path}");
			}
		}
	}

	private static void ValidateReleaseEntries(IReadOnlyList<ZzzAssetManifestZipEntry> entries, IEnumerable<string> manifestPaths)
	{
		HashSet<string> paths = new(manifestPaths, StringComparer.Ordinal)
		{
			ManifestFileName,
		};
		foreach (ZzzAssetManifestZipEntry entry in entries)
		{
			if (!IsSafeRelativePath(entry.Path) || !paths.Add(entry.Path))
			{
				throw new InvalidOperationException($"发布 ZIP 条目路径无效或重复: {entry.Path}");
			}

			if (!File.Exists(entry.SourcePath))
			{
				throw new FileNotFoundException("独立发布条目不存在。", entry.SourcePath);
			}
		}
	}

	private static void AddFile(ZipArchive archive, string sourcePath, string entryPath)
	{
		ZipArchiveEntry entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
		using Stream source = File.OpenRead(sourcePath);
		using Stream destination = entry.Open();
		source.CopyTo(destination);
	}

	private static string GetPathUnderRoot(string root, string relativePath)
	{
		string fullRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException($"资源路径越出 staging 根目录: {relativePath}");
		}

		return path;
	}

	private static bool IsSafeRelativePath(string path) =>
		!string.IsNullOrWhiteSpace(path) &&
		!Path.IsPathFullyQualified(path) &&
		!path.StartsWith("/", StringComparison.Ordinal) &&
		path.Split('/').All(segment => segment is not "" and not "." and not "..");
}

/// <summary>
/// 独立发布条目。
/// </summary>
/// <param name="SourcePath">待压缩的文件路径。</param>
/// <param name="Path">ZIP 内的 POSIX 相对路径。</param>
public sealed record ZzzAssetManifestZipEntry(string SourcePath, string Path);

/// <summary>
/// ZIP 生成结果。
/// </summary>
/// <param name="ZipPath">生成的 ZIP 路径。</param>
/// <param name="ManifestSourceSummary">资源清单来源摘要。</param>
/// <param name="Rid">资源清单 RID。</param>
/// <param name="ManagedFileCount">受 manifest 管理的资源文件数。</param>
public sealed record ZzzAssetManifestZipResult(string ZipPath, string ManifestSourceSummary, string Rid, int ManagedFileCount);

/// <summary>
/// 资源清单阻断 ZIP 生成时的异常。
/// </summary>
/// <param name="issues">资源清单问题。</param>
public sealed class ZzzAssetManifestValidationException(IReadOnlyList<ZzzAssetManifestIssue> issues) : Exception("资源清单校验未通过。")
{
	/// <summary>
	/// 资源清单问题。
	/// </summary>
	public IReadOnlyList<ZzzAssetManifestIssue> Issues { get; } = issues;
}

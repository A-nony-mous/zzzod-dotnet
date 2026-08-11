using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using ZzzOd.AppHost.Resources;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 按资源清单生成 ZIP 的测试。
/// </summary>
public sealed class ZzzAssetManifestZipBuilderTests : IDisposable
{
	private readonly string _runRoot = Path.Combine(Path.GetTempPath(), "zzzod-manifest-zip-tests", Guid.NewGuid().ToString("N"));

	public ZzzAssetManifestZipBuilderTests()
	{
		Directory.CreateDirectory(_runRoot);
	}

	/// <summary>
	/// ZIP 中的受管理资源集合和哈希应与 manifest 完全相同。
	/// </summary>
	[Fact]
	public void Create_ShouldUseOnlyManifestFilesAndExplicitReleaseEntries()
	{
		WriteFile("assets/zip-payload.bin", "资源内容");
		WriteManifest("assets/zip-payload.bin");
		string binaryPath = WriteFile("release/ZzzOd.Gui.exe", "二进制");
		string zipPath = Path.Combine(_runRoot, "release.zip");

		ZzzAssetManifestZipResult result = new ZzzAssetManifestZipBuilder().Create(
			_runRoot,
			zipPath,
			[new ZzzAssetManifestZipEntry(binaryPath, "ZzzOd.Gui.exe")]);

		using ZipArchive archive = ZipFile.OpenRead(zipPath);
		Assert.Equal(["ZzzOd.Gui.exe", "assets-manifest.json", "assets/zip-payload.bin"], archive.Entries.Select(entry => entry.FullName).OrderBy(path => path, StringComparer.Ordinal));
		using Stream payload = archive.GetEntry("assets/zip-payload.bin")!.Open();
		Assert.Equal(GetSha256("assets/zip-payload.bin"), Convert.ToHexString(SHA256.HashData(payload)));
		Assert.Equal("SOURCE-SUMMARY", result.ManifestSourceSummary);
		Assert.Equal("win-x64", result.Rid);
		Assert.Equal(1, result.ManagedFileCount);
	}

	/// <summary>
	/// 清单校验失败时不能产出 ZIP。
	/// </summary>
	[Theory]
	[InlineData("missing")]
	[InlineData("hash-mismatch")]
	[InlineData("extra-file")]
    public void Create_ShouldNotProduceZipWhenStagingIsInvalid(string invalidKind)
	{
		WriteFile("assets/zip-payload.bin", "资源内容");
		WriteManifest("assets/zip-payload.bin");
		switch (invalidKind)
		{
			case "missing":
				File.Delete(Path.Combine(_runRoot, "assets", "zip-payload.bin"));
				break;
			case "hash-mismatch":
				WriteFile("assets/zip-payload.bin", "篡改内容");
				break;
			case "extra-file":
				WriteFile("assets/unmanaged.bin", "多余资源");
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(invalidKind), invalidKind, null);
		}

		string zipPath = Path.Combine(_runRoot, "invalid.zip");

		Assert.Throws<InvalidOperationException>(() => new ZzzAssetManifestZipBuilder().Create(_runRoot, zipPath));
        Assert.False(File.Exists(zipPath));
    }

	/// <summary>
	/// ZIP 缺资源、多资源或解压后哈希不一致时不能通过分发校验。
	/// </summary>
	[Theory]
	[InlineData("missing")]
	[InlineData("extra")]
	[InlineData("tampered")]
	public void Validate_ShouldRejectMalformedZip(string invalidKind)
	{
		WriteFile("assets/zip-payload.bin", "资源内容");
		WriteManifest("assets/zip-payload.bin");
		string zipPath = Path.Combine(_runRoot, "release.zip");
		ZzzAssetManifestZipBuilder builder = new();
		builder.Create(_runRoot, zipPath);

		using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
		{
			switch (invalidKind)
			{
				case "missing":
					archive.GetEntry("assets/zip-payload.bin")!.Delete();
					break;
				case "extra":
					using (StreamWriter writer = new(archive.CreateEntry("assets/unmanaged.bin").Open()))
					{
						writer.Write("多余资源");
					}
					break;
				case "tampered":
					archive.GetEntry("assets/zip-payload.bin")!.Delete();
					using (StreamWriter writer = new(archive.CreateEntry("assets/zip-payload.bin").Open()))
					{
						writer.Write("篡改内容");
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(invalidKind), invalidKind, null);
			}
		}

		Assert.Throws<InvalidOperationException>(() => builder.Validate(_runRoot, zipPath));
	}

	private void WriteManifest(string relativePath)
	{
		ZzzAssetManifest manifest = new()
		{
			SchemaVersion = ZzzAssetManifest.CurrentSchemaVersion,
			Rid = "win-x64",
			GeneratedSource = "test",
			SourceSummary = "SOURCE-SUMMARY",
			ManagedRoots = ["assets"],
			Files =
			[
				new ZzzAssetManifestFile
				{
					Path = relativePath,
					Category = "static-assets",
					Required = true,
					Rids = ["win-x64"],
					Size = new FileInfo(Path.Combine(_runRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))).Length,
					Sha256 = GetSha256(relativePath),
					GeneratedSource = "test",
				},
			],
		};
		File.WriteAllText(Path.Combine(_runRoot, "assets-manifest.json"), JsonSerializer.Serialize(manifest));
	}

	private string WriteFile(string relativePath, string content)
	{
		string path = Path.Combine(_runRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
		return path;
	}

	private string GetSha256(string relativePath) =>
		Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_runRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))));

	public void Dispose()
	{
		if (Directory.Exists(_runRoot))
		{
			Directory.Delete(_runRoot, recursive: true);
		}
	}
}

using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using ZzzOd.AppHost.Resources;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 测试 staging 资源清单校验器。
/// </summary>
public sealed class ZzzAssetManifestValidatorTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-asset-manifest-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 初始化隔离运行根目录。
    /// </summary>
    public ZzzAssetManifestValidatorTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    /// <summary>
    /// 正确清单应通过校验。
    /// </summary>
    [Fact]
    public void Validate_ShouldPassForManagedFileWithChinesePath()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.True(result.IsValid);
        Assert.Equal("SOURCE-SUMMARY", result.SourceSummary);
    }

    /// <summary>
    /// 文件篡改应报告稳定哈希问题代码。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportSha256MismatchAfterTampering()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();
        WriteFile("assets/game_data/测试.yml", "篡改");

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.Sha256Mismatch);
    }

    /// <summary>
    /// 受管理目录的额外文件应阻断校验。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportExtraManagedFile()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();
        WriteFile("assets/game_data/新增.yml", "新增");

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.ExtraManagedFile && issue.Path == "assets/game_data/新增.yml");
    }

    /// <summary>
    /// 聚合 YAML 即使未在清单中声明也应阻断校验。
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectAggregatedYamlInManagedDirectory()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();
        WriteFile("assets/game_data/screen_info/_od_merged.yml", "[]");

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.AggregatedYaml && issue.Path == "assets/game_data/screen_info/_od_merged.yml");
    }

    /// <summary>
    /// 绝对路径、父级越界和仅大小写不同的重复路径应返回稳定问题代码。
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectUnsafeAndCaseConflictingPaths()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest(
            new ZzzAssetManifestFile { Path = "C:/escape.yml", Category = "static-assets", Rids = ["win-x64"], Size = 0, Sha256 = "" },
            new ZzzAssetManifestFile { Path = "assets/game_data/测试.yml", Category = "static-assets", Rids = ["win-x64"], Size = 6, Sha256 = GetSha256("assets/game_data/测试.yml") },
            new ZzzAssetManifestFile { Path = "assets/GAME_DATA/测试.yml", Category = "static-assets", Rids = ["win-x64"], Size = 6, Sha256 = GetSha256("assets/game_data/测试.yml") });

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.InvalidPath);
        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.CaseConflict);
    }

    private void WriteManifest(params ZzzAssetManifestFile[]? files)
    {
        ZzzAssetManifestFile[] actualFiles = files is { Length: > 0 }
            ? files
            :
            [
                new ZzzAssetManifestFile
                {
                    Path = "assets/game_data/测试.yml",
                    Category = "static-assets",
                    Required = true,
                    Rids = ["win-x64"],
                    Size = new FileInfo(Path.Combine(_rootDirectory, "assets", "game_data", "测试.yml")).Length,
                    Sha256 = GetSha256("assets/game_data/测试.yml"),
                    GeneratedSource = "workspace-root",
                },
            ];
        ZzzAssetManifest manifest = new()
        {
            SchemaVersion = ZzzAssetManifest.CurrentSchemaVersion,
            Rid = "win-x64",
            GeneratedSource = "workspace-root",
            SourceSummary = "SOURCE-SUMMARY",
            ManagedRoots = ["assets"],
            MutablePaths = [],
            Files = actualFiles,
        };
        string path = Path.Combine(_rootDirectory, "assets-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
    }

    private void WriteFile(string relativePath, string content)
    {
        string path = Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string GetSha256(string relativePath)
    {
        string path = Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZzzOd.AppHost.Backend;
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
    /// 同尺寸内容替换仍必须由 SHA-256 检出。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportSha256MismatchForSameSizeTampering()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        long originalSize = new FileInfo(Path.Combine(_rootDirectory, "assets", "game_data", "测试.yml")).Length;
        WriteManifest();
        WriteFile("assets/game_data/测试.yml", "篡改");
        Assert.Equal(originalSize, new FileInfo(Path.Combine(_rootDirectory, "assets", "game_data", "测试.yml")).Length);

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.Sha256Mismatch);
    }

    /// <summary>
    /// 清单声明的文件缺失时必须阻断。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportMissingDeclaredFile()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();
        File.Delete(Path.Combine(_rootDirectory, "assets", "game_data", "测试.yml"));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.FileMissing && issue.Path == "assets/game_data/测试.yml");
    }

    /// <summary>
    /// 调用方指定的 RID 与清单不一致时必须阻断。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportUnexpectedRid()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-arm64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.RidMismatch);
    }

    /// <summary>
    /// 清单来源摘要变化时调用方可作为完整性合同阻断。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportUnexpectedSourceSummary()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64", "OTHER-SOURCE-SUMMARY");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.SourceSummaryMismatch);
    }

    /// <summary>
    /// 不支持的 schemaVersion 必须阻断。
    /// </summary>
    [Fact]
    public void Validate_ShouldReportUnsupportedSchema()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest(schemaVersion: ZzzAssetManifest.CurrentSchemaVersion + 1);

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.UnsupportedSchema);
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
    /// 实例目录属于可变数据，运行时写入不应被误报为受管理资源。
    /// </summary>
    [Fact]
    public void Validate_ShouldAllowMutableInstanceConfigOutsideManifest()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteFile("config/01/game.yml", "game_path: C:/ZenlessZoneZero.exe");
        WriteManifest(["config/[0-9]*/**"], ["assets", "config"], CreateManifestFile("assets/game_data/测试.yml"));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// ApiHost 与一条龙运行时状态不应被当作发布资源。
    /// </summary>
    [Fact]
    public void Validate_ShouldAllowMutableHostRuntimeConfigOutsideManifest()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteFile("config/api_host.json", "{}");
        WriteFile("config/one_dragon.yml", "instance_idx: 1");
        WriteManifest(
            ["config/api_host.json", "config/one_dragon.yml"],
            ["assets", "config"],
            CreateManifestFile("assets/game_data/测试.yml"));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.True(result.IsValid);
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
    /// 旧自动战斗聚合文件即使被清单直接声明也必须阻断。
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectDeclaredLegacyMergedYaml()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteFile("config/auto_battle/测试.merged.yml", "scenes: []");
        WriteManifest(
            CreateManifestFile("assets/game_data/测试.yml"),
            CreateManifestFile("config/auto_battle/测试.merged.yml"));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.AggregatedYaml && issue.Path == "config/auto_battle/测试.merged.yml");
    }

    /// <summary>
    /// 运行态文件即使由 mutablePaths 声明也不得混入 staging。
    /// </summary>
    [Theory]
    [InlineData("assets/cache/state.bin")]
    [InlineData("assets/runtime.log")]
    [InlineData("assets/custom/override.yml")]
    [InlineData("config/1/battle_assistant.yml")]
    [InlineData("config/auto_battle/app_run_record/latest.yml")]
    public void Validate_ShouldRejectExcludedRuntimeFiles(string excludedPath)
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteFile(excludedPath, "运行态数据");
        WriteManifest(
            ["assets/**", "config/**"],
            CreateManifestFile("assets/game_data/测试.yml"),
            CreateManifestFile(excludedPath));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.ExcludedRuntimeFile && issue.Path == excludedPath);
    }

    /// <summary>
    /// 声明的独立画面文件必须能构建完整索引。
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectInvalidIndependentScreenConfig()
    {
        WriteFile("assets/game_data/screen_info/错误.yml", "screen_name: 错误画面\narea_list: []");
        WriteManifest(CreateManifestFile("assets/game_data/screen_info/错误.yml"));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.GameConfigInvalid && issue.Path == "assets/game_data/screen_info");
    }

    /// <summary>
    /// 自动战斗主策略引用缺失模板时必须阻断 staging。
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectMissingAutoBattleReference()
    {
        WriteFile("assets/game_data/screen_info/battle.yml", "screen_id: battle\nscreen_name: 战斗\narea_list: []");
        WriteFile("config/auto_battle/测试.yml", "scenes:\n  - triggers: []\n    handlers:\n      - state_template: 缺失模板");
        WriteManifest(
            CreateManifestFile("assets/game_data/screen_info/battle.yml"),
            CreateManifestFile("config/auto_battle/测试.yml"));

        ZzzAssetManifestValidationResult result = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");

        Assert.Contains(result.Issues, issue => issue.Code == ZzzAssetManifestIssueCode.GameConfigInvalid && issue.Path == "config/auto_battle/测试.yml");
    }

    /// <summary>
    /// 启动入口输出稳定问题代码，供 GUI 与 ApiHost 直接显示阻断原因。
    /// </summary>
    [Fact]
    public void StartupGate_ShouldWriteStableIssueCode()
    {
        using StringWriter writer = new();
        ZzzAssetManifestStartupGate.WriteIssues(
            [new ZzzAssetManifestIssue(ZzzAssetManifestIssueCode.ManifestMissing, "assets-manifest.json", "未找到资源清单。")],
            writer);

        Assert.Equal("ManifestMissing: assets-manifest.json 未找到资源清单。" + Environment.NewLine, writer.ToString());
    }

    /// <summary>
    /// 有效 staging 通过门禁后才允许创建运行时管理器。
    /// </summary>
    [Fact]
    public void StartupGate_ShouldAllowRuntimeManagerForValidStaging()
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();

        ZzzAssetManifestValidationResult validation = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");
        ZzzValidatedRunRoot validatedRunRoot = ZzzAssetManifestStartupGate.CreateValidatedRunRoot(validation);
        using ZzzRuntimeManager runtime = new(validatedRunRoot.Path, NullLogger<ZzzRuntimeManager>.Instance);

        Assert.Equal(Path.GetFullPath(_rootDirectory), runtime.RunRoot);
        Assert.False(runtime.HasContext);
    }

    /// <summary>
    /// 缺失、哈希错误、聚合 YAML 和多余受管理文件都必须在创建运行时管理器前被门禁阻断。
    /// </summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("hash-mismatch")]
    [InlineData("aggregated-yaml")]
    [InlineData("extra-managed-file")]
    public void StartupGate_ShouldBlockInvalidStagingBeforeRuntimeManagerInitialization(string invalidKind)
    {
        WriteFile("assets/game_data/测试.yml", "内容");
        WriteManifest();
        switch (invalidKind)
        {
            case "missing":
                File.Delete(Path.Combine(_rootDirectory, "assets-manifest.json"));
                break;
            case "hash-mismatch":
                WriteFile("assets/game_data/测试.yml", "篡改");
                break;
            case "aggregated-yaml":
                WriteFile("assets/game_data/screen_info/_od_merged.yml", "[]");
                break;
            case "extra-managed-file":
                WriteFile("assets/game_data/新增.yml", "新增");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidKind), invalidKind, null);
        }

        ZzzAssetManifestValidationResult validation = new ZzzAssetManifestValidator().Validate(_rootDirectory, "win-x64");
        bool runtimeManagerCreated = false;

        Assert.False(validation.IsValid);
        Assert.Throws<InvalidOperationException>(() =>
        {
            ZzzValidatedRunRoot validatedRunRoot = ZzzAssetManifestStartupGate.CreateValidatedRunRoot(validation);
            runtimeManagerCreated = true;
            using ZzzRuntimeManager _ = new(validatedRunRoot.Path, NullLogger<ZzzRuntimeManager>.Instance);
        });
        Assert.False(runtimeManagerCreated);
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

    private void WriteManifest(params ZzzAssetManifestFile[]? files) => WriteManifest([], ZzzAssetManifest.CurrentSchemaVersion, files);

    private void WriteManifest(IReadOnlyList<string> mutablePaths, params ZzzAssetManifestFile[]? files) =>
        WriteManifest(mutablePaths, ZzzAssetManifest.CurrentSchemaVersion, files);

    private void WriteManifest(IReadOnlyList<string> mutablePaths, IReadOnlyList<string> managedRoots, params ZzzAssetManifestFile[]? files) =>
        WriteManifest(mutablePaths, managedRoots, ZzzAssetManifest.CurrentSchemaVersion, files);

    private void WriteManifest(int schemaVersion, params ZzzAssetManifestFile[]? files) =>
        WriteManifest([], schemaVersion, files);

    private void WriteManifest(IReadOnlyList<string> mutablePaths, int schemaVersion, params ZzzAssetManifestFile[]? files)
        => WriteManifest(mutablePaths, ["assets"], schemaVersion, files);

    private void WriteManifest(IReadOnlyList<string> mutablePaths, IReadOnlyList<string> managedRoots, int schemaVersion, params ZzzAssetManifestFile[]? files)
    {
        ZzzAssetManifestFile[] actualFiles = files is { Length: > 0 }
            ? files
            :
            [
                CreateManifestFile("assets/game_data/测试.yml"),
            ];
        ZzzAssetManifest manifest = new()
        {
            SchemaVersion = schemaVersion,
            Rid = "win-x64",
            GeneratedSource = "workspace-root",
            SourceSummary = "SOURCE-SUMMARY",
            ManagedRoots = managedRoots,
            MutablePaths = mutablePaths,
            Files = actualFiles,
        };
        string path = Path.Combine(_rootDirectory, "assets-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest));
    }

    private ZzzAssetManifestFile CreateManifestFile(string relativePath) => new()
    {
        Path = relativePath,
        Category = "static-assets",
        Required = true,
        Rids = ["win-x64"],
        Size = new FileInfo(Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar))).Length,
        Sha256 = GetSha256(relativePath),
        GeneratedSource = "workspace-root",
    };

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

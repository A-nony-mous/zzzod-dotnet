using System.Text.RegularExpressions;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// 前卫视图与共享控件的配置访问边界审计。
/// 迁移期间只允许 2026-07-27 基线文件保留不超过基线数量的直接调用。
/// </summary>
[Trait("Category", "Audit")]
public sealed class ConfigAccessBoundaryAuditTests
{
    private static readonly Regex DirectAccessPattern = new(
        @"\b(?:GetConfigScope|SaveConfigScope)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, int> InitialBaseline =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Controls/ZzzRunPanel.cs"] = 1,
            ["Views/FrontierPages/ApplicationSettings/FrontierCoffeeAppSettingPage.axaml.cs"] = 3,
            ["Views/FrontierPages/ApplicationSettings/FrontierDailySignInSettingsFlyoutContent.axaml.cs"] = 2,
            ["Views/FrontierPages/ApplicationSettings/FrontierDriveDiscDismantleSettingsFlyoutContent.axaml.cs"] = 2,
            ["Views/FrontierPages/ApplicationSettings/FrontierIntelBoardSettingsFlyoutContent.axaml.cs"] = 3,
            ["Views/FrontierPages/ApplicationSettings/FrontierNotoriousHuntAppSettingPage.axaml.cs"] = 2,
            ["Views/FrontierPages/ApplicationSettings/FrontierShiyuDefenseAppSettingPage.axaml.cs"] = 3,
            ["Views/FrontierPages/ApplicationSettings/FrontierSuibianTempleAppSettingPage.axaml.cs"] = 2,
            ["Views/FrontierPages/ApplicationSettings/FrontierWitheredDomainAppSettingPage.axaml.cs"] = 2,
            ["Views/FrontierPages/DevTools/FrontierOperationDebugPage.cs"] = 3,
            ["Views/FrontierPages/DevTools/FrontierScreenshotHelperPage.cs"] = 2,
            ["Views/FrontierPages/GameAssistant/FrontierGameAssistantPages.cs"] = 5,
            ["Views/FrontierPages/Home/FrontierHomePage.axaml.cs"] = 4,
            ["Views/FrontierPages/OneDragon/FrontierNotifySettingsPage.cs"] = 3,
            ["Views/FrontierPages/OneDragon/FrontierPredefinedTeamPage.cs"] = 2,
            ["Views/FrontierPages/Settings/FrontierEnvironmentSettingsPage.axaml.cs"] = 3,
            ["Views/FrontierPages/Settings/FrontierGameSettingsPage.axaml.cs"] = 1,
            ["Views/FrontierPages/Settings/FrontierOverlaySettingsPage.axaml.cs"] = 1,
            ["Views/FrontierPages/Settings/FrontierPushSettingsPage.axaml.cs"] = 4,
            ["Views/FrontierPages/Settings/FrontierResourceDownloadPage.axaml.cs"] = 2,
            ["Views/FrontierPages/Standalone/FrontierStandaloneAppRunPage.axaml.cs"] = 3,
            ["Views/FrontierPages/WorldPatrol/FrontierWorldPatrolPage.cs"] = 2,
        };

    private sealed record WhitelistEntry(string File, int Count, string Reason);

    [Fact]
    public void DirectConfigAccessMustMatchShrinkingWhitelist()
    {
        string guiRoot = FindGuiRoot();
        IReadOnlyDictionary<string, WhitelistEntry> whitelist = LoadWhitelist(guiRoot);
        Dictionary<string, int> actual = EnumerateBoundaryFiles(guiRoot)
            .Select(path => new
            {
                File = Path.GetRelativePath(guiRoot, path).Replace('\\', '/'),
                Count = DirectAccessPattern.Matches(File.ReadAllText(path)).Count,
            })
            .Where(item => item.Count > 0)
            .ToDictionary(item => item.File, item => item.Count, StringComparer.OrdinalIgnoreCase);

        List<string> failures = [];
        foreach ((string file, int count) in actual)
        {
            if (!whitelist.TryGetValue(file, out WhitelistEntry? entry))
            {
                failures.Add($"未豁免的配置直调: {file} ({count})");
                continue;
            }

            if (entry.Count != count)
            {
                failures.Add($"调用数与豁免不一致: {file} 实际 {count}, 豁免 {entry.Count}");
            }
        }

        foreach ((string file, WhitelistEntry entry) in whitelist)
        {
            if (!InitialBaseline.TryGetValue(file, out int baselineCount))
            {
                failures.Add($"新增文件不得进入豁免: {file}");
                continue;
            }

            if (entry.Count > baselineCount)
            {
                failures.Add($"豁免调用数超过基线: {file} 当前 {entry.Count}, 基线 {baselineCount}");
            }

            if (!actual.ContainsKey(file))
            {
                failures.Add($"已失效的豁免必须删除: {file}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static IReadOnlyDictionary<string, WhitelistEntry> LoadWhitelist(string guiRoot)
    {
        string path = Path.Combine(guiRoot, "config-access-whitelist.md");
        Assert.True(File.Exists(path), $"缺少配置访问豁免文件: {path}");

        Dictionary<string, WhitelistEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('|'))
            {
                continue;
            }

            string[] parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !int.TryParse(parts[1], out int count))
            {
                continue;
            }

            Assert.True(count > 0, $"豁免调用数必须大于零: {line}");
            Assert.False(string.IsNullOrWhiteSpace(parts[2]), $"豁免条目缺理由: {line}");
            string file = parts[0].Replace('\\', '/');
            Assert.True(entries.TryAdd(file, new WhitelistEntry(file, count, parts[2])), $"重复豁免: {file}");
        }

        return entries;
    }

    private static IEnumerable<string> EnumerateBoundaryFiles(string guiRoot) =>
        Directory.EnumerateFiles(Path.Combine(guiRoot, "Views", "FrontierPages"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(guiRoot, "Controls"), "*.cs", SearchOption.AllDirectories));

    private static string FindGuiRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "zzzod-dotnet", "src", "ZzzOd.Gui");
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        throw new DirectoryNotFoundException("未找到 ZzzOd.Gui 源码目录。");
    }
}

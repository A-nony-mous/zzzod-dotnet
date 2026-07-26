using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// 审计生产源码中影响实机自动化的占位实现。
/// </summary>
[Trait("Category", "Audit")]
public sealed class ProductionPlaceholderAuditTests
{
	private sealed record PlaceholderFinding(string RelativePath, string Category, string Text)
	{
		public string Key => Key(RelativePath, Category, Text);
	}

	private static readonly IReadOnlySet<string> AllowedFindingKeys = new HashSet<string>(StringComparer.Ordinal)
	{
		Key("od-dotnet/src/OneDragon.Core.Windows/WindowsCoreModule.cs", "placeholder-comment", "/// OneDragon Windows 平台占位模块。")
	};

	[Fact]
	public void ProductionSource_ShouldNotIntroduceUntrackedPlaceholderCode()
	{
		IReadOnlyList<PlaceholderFinding> source = ScanProductionSource();
		string[] array = (from finding in source
			where !AllowedFindingKeys.Contains(finding.Key)
			select $"{finding.RelativePath}: {finding.Category}: {finding.Text}").ToArray();
		Assert.True(array.Length == 0, "发现未登记的生产源码占位实现：" + Environment.NewLine + string.Join(Environment.NewLine, array));
	}

	private static IReadOnlyList<PlaceholderFinding> ScanProductionSource()
	{
		string text = FindRepoRoot();
		string[] array = new string[2]
		{
			Path.Combine(text, "zzzod-dotnet", "src", "ZzzOd.GameLogic"),
			Path.Combine(text, "od-dotnet", "src")
		};
		List<PlaceholderFinding> list = new List<PlaceholderFinding>();
		string[] array2 = array;
		foreach (string path in array2)
		{
			foreach (string item in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
			{
				AddFindings(text, item, list);
			}
		}
		return list;
	}

	private static void AddFindings(string repoRoot, string filePath, List<PlaceholderFinding> findings)
	{
		string relativePath = Normalize(Path.GetRelativePath(repoRoot, filePath));
		foreach (string item in File.ReadLines(filePath))
		{
			string text = item.Trim();
			if (text.Contains("占位", StringComparison.Ordinal))
			{
				findings.Add(new PlaceholderFinding(relativePath, "placeholder-comment", text));
			}
			if (ContainsSuspiciousPlaceholderToken(text))
			{
				findings.Add(new PlaceholderFinding(relativePath, "suspicious-placeholder-token", text));
			}
			if (text.Contains("Task.Delay(10)", StringComparison.Ordinal))
			{
				findings.Add(new PlaceholderFinding(relativePath, "short-delay-completion", text));
			}
			if (text.Contains("将在截图节点中执行", StringComparison.Ordinal))
			{
				findings.Add(new PlaceholderFinding(relativePath, "not-wired-screenshot-node", text));
			}
			if (DefaultTrueDependencyCheckRegex().IsMatch(text))
			{
				findings.Add(new PlaceholderFinding(relativePath, "default-true-dependency-check", text));
			}
			if (FixedScreenReadinessSuccessRegex().IsMatch(text))
			{
				findings.Add(new PlaceholderFinding(relativePath, "fixed-screen-readiness-success", text));
			}
			if (FixedMoveAfterBattleSuccessRegex().IsMatch(text))
			{
				findings.Add(new PlaceholderFinding(relativePath, "fixed-move-after-battle-success", text));
			}
		}
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string fullName = directoryInfo.FullName;
			if (Directory.Exists(Path.Combine(fullName, "zzzod-dotnet", "src", "ZzzOd.GameLogic")) && Directory.Exists(Path.Combine(fullName, "od-dotnet", "src")))
			{
				return fullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 zzz-od-dotnet 仓库根目录。");
	}

	private static string Key(string relativePath, string category, string text)
	{
		return $"{Normalize(relativePath)}|{category}|{text.Trim()}";
	}

	private static string Normalize(string path)
	{
		return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
	}

	private static bool ContainsSuspiciousPlaceholderToken(string text)
	{
		if (text.Contains("ProductionPlaceholderAuditTests", StringComparison.Ordinal))
		{
			return false;
		}
		string[] source = new string[10] { "假设", "假定", "简单起见", "TODO", "todo", "placeholder", "Placeholder", "stub", "Stub", "NotImplementedException" };
		return source.Any((string token) => text.Contains(token, StringComparison.Ordinal));
	}

	/// <remarks>
	/// Pattern:<br />
	/// <code>IsVirtualGamepadInstalled\\(\\)\\s*=&gt;\\s*true</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match the string "IsVirtualGamepadInstalled()".<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match the string "=&gt;".<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match the string "true".<br />
	/// </code>
	/// </remarks>
	private static Regex DefaultTrueDependencyCheckRegex() => new Regex("IsVirtualGamepadInstalled\\(\\)\\s*=>\\s*true", RegexOptions.CultureInvariant);

	/// <remarks>
	/// Pattern:<br />
	/// <code>IsBattleScreenReady\\(ZContext context\\)\\s*=&gt;\\s*true</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match the string "IsBattleScreenReady(ZContext context)".<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match the string "=&gt;".<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match the string "true".<br />
	/// </code>
	/// </remarks>
	private static Regex FixedScreenReadinessSuccessRegex() => new Regex("IsBattleScreenReady\\(ZContext context\\)\\s*=>\\s*true", RegexOptions.CultureInvariant);

	/// <remarks>
	/// Pattern:<br />
	/// <code>MoveAfterBattle\\(ZContext context\\)\\s*=&gt;\\s*true</code><br />
	/// Explanation:<br />
	/// <code>
	/// ○ Match the string "MoveAfterBattle(ZContext context)".<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match the string "=&gt;".<br />
	/// ○ Match a whitespace character atomically any number of times.<br />
	/// ○ Match the string "true".<br />
	/// </code>
	/// </remarks>
	private static Regex FixedMoveAfterBattleSuccessRegex() => new Regex("MoveAfterBattle\\(ZContext context\\)\\s*=>\\s*true", RegexOptions.CultureInvariant);
}

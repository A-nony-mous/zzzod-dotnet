using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// GUI 真实配置基础测试。
/// </summary>
public sealed class RealConfigFoundationTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		public ZzzAppBackend Backend { get; }

		public BackendSession(string runRoot)
		{
			_runtime = new ZzzRuntimeManager(runRoot, NullLogger<ZzzRuntimeManager>.Instance);
			ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
			_battleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
			_logProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(runRoot), eventBus);
			Backend = new ZzzAppBackend(_runtime, eventBus, _battleAssistantRuntimeSource, _logProvider, new ZzzHostModeOptions(ZzzHostMode.Gui), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
		}

		public void Dispose()
		{
			_runtime.Dispose();
			_battleAssistantRuntimeSource.Dispose();
			_logProvider.Dispose();
		}
	}

	/// <summary>
	/// 工作区运行配置和仓库内配置副本应保持逐文件一致。
	/// </summary>
	[Fact]
	public void WorkspaceAndRepositoryConfigTreesMatch()
	{
		string path = FindWorkspaceRoot();
		string text = Path.Combine(path, "config");
		string text2 = Path.Combine(path, "zzzod-dotnet", "config");
		string[] relativeConfigFiles = GetRelativeConfigFiles(text);
		string[] relativeConfigFiles2 = GetRelativeConfigFiles(text2);
		Assert.DoesNotContain((IEnumerable<string>)relativeConfigFiles, (Predicate<string>)((string path2) => string.Equals(Path.GetExtension(path2), ".pkl", StringComparison.OrdinalIgnoreCase)));
		Assert.DoesNotContain((IEnumerable<string>)relativeConfigFiles2, (Predicate<string>)((string path2) => string.Equals(Path.GetExtension(path2), ".pkl", StringComparison.OrdinalIgnoreCase)));
		Assert.DoesNotContain((IEnumerable<string>)relativeConfigFiles, (Predicate<string>)((string text4) => text4.EndsWith(".yml_cache", StringComparison.OrdinalIgnoreCase)));
		Assert.DoesNotContain((IEnumerable<string>)relativeConfigFiles2, (Predicate<string>)((string text4) => text4.EndsWith(".yml_cache", StringComparison.OrdinalIgnoreCase)));
		Assert.Empty(Directory.EnumerateDirectories(text, "yml_cache", SearchOption.AllDirectories));
		Assert.Empty(Directory.EnumerateDirectories(text2, "yml_cache", SearchOption.AllDirectories));
		Assert.Equal<string[]>(relativeConfigFiles, relativeConfigFiles2);
		string[] array = relativeConfigFiles;
		foreach (string text3 in array)
		{
			if (!IsRuntimeStateFile(text3))
			{
				Assert.Equal(ReadStableConfigContent(Path.Combine(text, text3), text3), ReadStableConfigContent(Path.Combine(text2, text3), text3));
			}
		}
	}

	/// <summary>
	/// 空 run root 应保留真实空列表，并由底层配置对象提供 init 默认值。
	/// </summary>
	[Fact]
	public void EmptyRunRootKeepsConfigListsEmptyAndReadsInitDefaults()
	{
		string text = CreateTempRunRoot();
		try
		{
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendSession.Backend.GetConfigScope("game", 0);
			AutoBattleConfigProvider autoBattleConfigProvider = new AutoBattleConfigProvider(new OneDragonEnvironment(text));
			Assert.True(configScope.Success, configScope.Error);
			Assert.Equal(new GameConfig().TypeInputWay, configScope.Value.Values["type_input_way"]);
			Assert.Empty(autoBattleConfigProvider.GetAutoBattleOpConfigList("auto_battle"));
			Assert.Empty(autoBattleConfigProvider.GetAutoBattleOpConfigList("dodge"));
			Assert.False(Directory.Exists(Path.Combine(text, "config", "auto_battle")));
			Assert.False(Directory.Exists(Path.Combine(text, "config", "dodge")));
		}
		finally
		{
			DeleteRunRoot(text);
		}
	}

	/// <summary>
	/// 真实配置快照应保留列表和值，并支持写回后重新创建后端读取。
	/// </summary>
	[Fact]
	public void RealConfigSnapshotListsWriteBackAndReloadWithoutMutatingSource()
	{
		string text = FindWorkspaceRoot();
		string text2 = Path.Combine(text, "config");
		int value = FindInstanceWithGameConfig(text2);
		string text3 = Path.Combine(text2, value.ToString("00"), "game.yml");
		string expected = File.ReadAllText(text3);
		string text4 = CreateTempRunRoot();
		try
		{
			CopyFile(text3, Path.Combine(text4, "config", value.ToString("00"), "game.yml"));
			string[] expected2 = CopyRealConfigList(text2, text4, "auto_battle", 3);
			string[] expected3 = CopyRealConfigList(text2, text4, "dodge", 3);
			GameConfig current = new YamlConfig<GameConfig>(new OneDragonEnvironment(text), "game", null, value).Current;
			string text5 = (string.Equals(current.TypeInputWay, "input", StringComparison.Ordinal) ? "clipboard" : "input");
			using (BackendSession backendSession = new BackendSession(text4))
			{
				ZzzBackendResult<ZzzConfigScopeValuesDto> configScope = backendSession.Backend.GetConfigScope("game", value);
				AutoBattleConfigProvider autoBattleConfigProvider = new AutoBattleConfigProvider(new OneDragonEnvironment(text4));
				Assert.True(configScope.Success, configScope.Error);
				Assert.Equal(current.TypeInputWay, configScope.Value.Values["type_input_way"]);
				Assert.Equal<string[]>(expected2, (from item in autoBattleConfigProvider.GetAutoBattleOpConfigList("auto_battle")
					select item.Value?.ToString()).ToArray());
				Assert.Equal<string[]>(expected3, (from item in autoBattleConfigProvider.GetAutoBattleOpConfigList("dodge")
					select item.Value?.ToString()).ToArray());
				ZzzBackendResult<ZzzConfigScopeValuesDto> zzzBackendResult = backendSession.Backend.SaveConfigScope(new ZzzSaveConfigScopeRequest("game", new Dictionary<string, object> { ["type_input_way"] = text5 }, value));
				Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
				Assert.Equal(text5, zzzBackendResult.Value.Values["type_input_way"]);
			}
			using (BackendSession backendSession2 = new BackendSession(text4))
			{
				ZzzBackendResult<ZzzConfigScopeValuesDto> configScope2 = backendSession2.Backend.GetConfigScope("game", value);
				Assert.True(configScope2.Success, configScope2.Error);
				Assert.Equal(text5, configScope2.Value.Values["type_input_way"]);
			}
			Assert.Equal(expected, File.ReadAllText(text3));
		}
		finally
		{
			DeleteRunRoot(text4);
		}
	}

	private static string[] CopyRealConfigList(string sourceConfigRoot, string runRoot, string directoryName, int maximumFiles)
	{
		string path = Path.Combine(sourceConfigRoot, directoryName);
		string path2 = Path.Combine(runRoot, "config", directoryName);
		string[] array = Directory.EnumerateFiles(path, "*.yml", SearchOption.TopDirectoryOnly).OrderBy<string, string>((string path3) => Path.GetFileName(path3), StringComparer.Ordinal).Take(maximumFiles)
			.ToArray();
		Assert.NotEmpty(array);
		string[] array2 = array;
		foreach (string text in array2)
		{
			CopyFile(text, Path.Combine(path2, Path.GetFileName(text)));
		}
		AutoBattleConfigProvider autoBattleConfigProvider = new AutoBattleConfigProvider(new OneDragonEnvironment(runRoot));
		return (from item in autoBattleConfigProvider.GetAutoBattleOpConfigList(directoryName)
			select item.Value?.ToString() ?? item.Label).ToArray();
	}

	private static void CopyFile(string sourcePath, string targetPath)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
		File.Copy(sourcePath, targetPath, overwrite: true);
	}

	private static int FindInstanceWithGameConfig(string configRoot)
	{
		string text = (from path in Directory.EnumerateDirectories(configRoot)
			where int.TryParse(Path.GetFileName(path), out var _)
			select path).OrderBy<string, string>((string path) => path, StringComparer.Ordinal).FirstOrDefault((string path) => File.Exists(Path.Combine(path, "game.yml")));
		if (text != null)
		{
			return int.Parse(Path.GetFileName(text), CultureInfo.InvariantCulture);
		}
		throw new FileNotFoundException("真实配置中没有可用的 config/NN/game.yml。");
	}

	private static string[] GetRelativeConfigFiles(string configRoot)
	{
		return (from path in Directory.EnumerateFiles(configRoot, "*", SearchOption.AllDirectories)
			select Path.GetRelativePath(configRoot, path)).OrderBy<string, string>((string path) => path, StringComparer.Ordinal).ToArray();
	}

	private static bool IsRuntimeStateFile(string relativePath)
	{
		return relativePath.Replace('\\', '/').Contains("/app_run_record/", StringComparison.Ordinal);
	}

	private static string ReadStableConfigContent(string path, string relativePath)
	{
		string text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
		if (!string.Equals(relativePath, "custom.yml", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		return string.Join("\n", from line in text.Split('\n')
			where !line.StartsWith("last_", StringComparison.Ordinal) || !line.Contains("_fetch_time:", StringComparison.Ordinal)
			select line);
	}

	private static string FindWorkspaceRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "config")) && Directory.Exists(Path.Combine(directoryInfo.FullName, "zzzod-dotnet", "src", "ZzzOd.Gui")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("未找到 D:\\zzz-od-dotnet 工作区根目录。");
	}

	private static string CreateTempRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-real-config-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void DeleteRunRoot(string runRoot)
	{
		if (Directory.Exists(runRoot))
		{
			Directory.Delete(runRoot, recursive: true);
		}
	}
}

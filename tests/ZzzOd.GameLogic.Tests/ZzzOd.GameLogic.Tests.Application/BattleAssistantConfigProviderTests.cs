using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Application.BattleAssistant;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class BattleAssistantConfigProviderTests : IDisposable
{
	private readonly string _rootDirectory;

	public BattleAssistantConfigProviderTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_rootDirectory);
	}

	[Fact]
	public void BattleAssistantSettings_ExposePythonFieldsAndDefaults()
	{
		Assert.Contains((IEnumerable<BattleAssistantSettingField>)BattleAssistantSettings.Fields, (Predicate<BattleAssistantSettingField>)((BattleAssistantSettingField field) => field.Key == "dodge_assistant_config" && field.DefaultValue.Equals("闪避")));
		Assert.Contains((IEnumerable<BattleAssistantSettingField>)BattleAssistantSettings.Fields, (Predicate<BattleAssistantSettingField>)((BattleAssistantSettingField field) => field.Key == "screenshot_interval" && field.DefaultValue.Equals(0.02)));
		Assert.Contains((IEnumerable<BattleAssistantSettingField>)BattleAssistantSettings.Fields, (Predicate<BattleAssistantSettingField>)((BattleAssistantSettingField field) => field.Key == "control_method" && field.DefaultValue.Equals("keyboard")));
		Assert.Contains((IEnumerable<BattleAssistantSettingField>)BattleAssistantSettings.Fields, (Predicate<BattleAssistantSettingField>)((BattleAssistantSettingField field) => field.Key == "auto_battle_config" && field.DefaultValue.Equals("全配队通用")));
		Assert.DoesNotContain((IEnumerable<BattleAssistantSettingField>)BattleAssistantSettings.Fields, (Predicate<BattleAssistantSettingField>)((BattleAssistantSettingField field) => field.Key == "use_merged_file"));
		Assert.Contains((IEnumerable<BattleAssistantSettingField>)BattleAssistantSettings.Fields, (Predicate<BattleAssistantSettingField>)((BattleAssistantSettingField field) => field.Key == "auto_ultimate_enabled" && field.DefaultValue.Equals(false)));
	}

	[Fact]
	public void OperationTemplateConfigProvider_ListsRecursiveTemplatesLikePython()
	{
		string path = Path.Combine(_rootDirectory, "config", "auto_battle_operation");
		Directory.CreateDirectory(Path.Combine(path, "agent", "anby"));
		File.WriteAllText(Path.Combine(path, "基础攻击.yml"), "operations: []");
		File.WriteAllText(Path.Combine(path, "agent", "anby", "连招.sample.yml"), "operations: []");
		File.WriteAllText(Path.Combine(path, "失效.merged.yml"), "operations: []");
		File.WriteAllText(Path.Combine(path, "ignored.txt"), "");
		OperationTemplateConfigProvider operationTemplateConfigProvider = new OperationTemplateConfigProvider(new OneDragonEnvironment(_rootDirectory));
		IReadOnlyList<ConfigItem> operationTemplateConfigList = operationTemplateConfigProvider.GetOperationTemplateConfigList();
		Assert.Equal<object>((IEnumerable<object>?)new object[2] { "agent/anby/连招", "基础攻击" }, operationTemplateConfigList.Select((ConfigItem option) => option.Value));
		Assert.Equal(operationTemplateConfigList.Select((ConfigItem option) => option.Value), operationTemplateConfigList.Select((ConfigItem option) => option.Label));
	}

	[Fact]
	public void AutoBattleConfigProvider_ListsOnlyIndependentYamlConfigs()
	{
		string path = Path.Combine(_rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(Path.Combine(path, "nested"));
		File.WriteAllText(Path.Combine(path, "全配队通用.yml"), "scenes: []");
		File.WriteAllText(Path.Combine(path, "安比.sample.yml"), "scenes: []");
		File.WriteAllText(Path.Combine(path, "比利.merged.yml"), "scenes: []");
		File.WriteAllText(Path.Combine(path, "nested", "不应列出.yml"), "scenes: []");
		AutoBattleConfigProvider autoBattleConfigProvider = new AutoBattleConfigProvider(new OneDragonEnvironment(_rootDirectory));
		IReadOnlyList<ConfigItem> autoBattleOpConfigList = autoBattleConfigProvider.GetAutoBattleOpConfigList("auto_battle");
		Assert.Equal<object>((IEnumerable<object>?)new object[2] { "全配队通用", "安比" }, autoBattleOpConfigList.Select((ConfigItem option) => option.Value));
		Assert.EndsWith(Path.Combine("config", "auto_battle", "全配队通用.yml"), autoBattleConfigProvider.GetAutoBattleConfigFilePath("auto_battle", "全配队通用"), StringComparison.Ordinal);
	}

	[Fact]
	public void AutoBattleOperator_UsesYamlThenSampleAndIgnoresMergedFiles()
	{
		string directory = Path.Combine(_rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(directory);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		File.WriteAllText(Path.Combine(directory, "测试.merged.yml"), "scenes: []");
		File.WriteAllText(Path.Combine(directory, "测试.sample.yml"), "scenes: []");

		string samplePath = AutoBattleOperator.ResolveYamlPath(environment, "auto_battle", "测试", readFromMerged: true);
		string sameSamplePath = AutoBattleOperator.ResolveYamlPath(environment, "auto_battle", "测试", readFromMerged: false);
		File.WriteAllText(Path.Combine(directory, "测试.yml"), "scenes: []");
		string yamlPath = AutoBattleOperator.ResolveYamlPath(environment, "auto_battle", "测试", readFromMerged: false);

		Assert.EndsWith("测试.sample.yml", samplePath, StringComparison.Ordinal);
		Assert.Equal(samplePath, sameSamplePath);
		Assert.EndsWith("测试.yml", yamlPath, StringComparison.Ordinal);
		Assert.DoesNotContain(".merged.yml", samplePath, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain(".merged.yml", yamlPath, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void AutoBattleConfigProvider_DeletesOnlyPlainYamlWithPythonMissingOkSemantics()
	{
		string text = Path.Combine(_rootDirectory, "config", "auto_battle");
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, "测试.yml");
		string path2 = Path.Combine(text, "测试.sample.yml");
		string path3 = Path.Combine(text, "测试.merged.yml");
		File.WriteAllText(path, "scenes: []");
		File.WriteAllText(path2, "scenes: []");
		File.WriteAllText(path3, "scenes: []");
		AutoBattleConfigProvider provider = new AutoBattleConfigProvider(new OneDragonEnvironment(_rootDirectory));
		provider.DeleteAutoBattleOpConfig("auto_battle", "测试");
		provider.DeleteAutoBattleOpConfig("auto_battle", "测试");
		Assert.False(File.Exists(path));
		Assert.True(File.Exists(path2));
		Assert.True(File.Exists(path3));
		Assert.Throws<ArgumentException>(delegate
		{
			provider.DeleteAutoBattleOpConfig("auto_battle", "..\\越界");
		});
		Assert.Throws<ArgumentException>(delegate
		{
			provider.DeleteAutoBattleOpConfig("other", "测试");
		});
	}

	[Fact]
	public void BattleAssistantConfig_LoadsPythonFieldsWithSettingMetadata()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "battle_assistant.yml"), "dodge_assistant_config: 闪避-测试\nscreenshot_interval: 0.05\ncontrol_method: xbox\nauto_battle_config: 比利-测试\nuse_merged_file: false\nauto_ultimate_enabled: true");
		BattleAssistantConfig battleAssistantConfig = BattleAssistantConfig.Load(new OneDragonEnvironment(_rootDirectory), 0);
		Assert.Equal("闪避-测试", battleAssistantConfig.DodgeAssistantConfig);
		Assert.Equal(0.05, battleAssistantConfig.ScreenshotInterval);
		Assert.Equal("xbox", battleAssistantConfig.ControlMethod);
		Assert.Equal("比利-测试", battleAssistantConfig.AutoBattleConfig);
		Assert.False(battleAssistantConfig.UseMergedFile);
		Assert.True(battleAssistantConfig.LegacyUseMergedFileWasSpecified);
		Assert.True(battleAssistantConfig.AutoUltimateEnabled);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}

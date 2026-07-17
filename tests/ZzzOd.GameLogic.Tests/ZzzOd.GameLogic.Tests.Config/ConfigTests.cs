using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Tests.Config;

/// <summary>
/// 测试绝区零配置的序列化与反序列化。
/// </summary>
public sealed class ConfigTests : IDisposable
{
	private readonly string _rootDirectory;

	public ConfigTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_rootDirectory);
	}

	[Fact]
	public void GameConfig_ShouldDeserializeCorrectly()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "\ncontrol_method: xbox\nxbox_key_interact: x\nkey_dodge: q\n";
		File.WriteAllText(Path.Combine(text, "game.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<GameConfig> yamlConfig = new YamlConfig<GameConfig>(environment, "game", null, 0);
		Assert.Equal("xbox", yamlConfig.Current.ControlMethod);
		Assert.Equal("x", yamlConfig.Current.XboxKeyInteract);
		Assert.Equal("q", yamlConfig.Current.KeyDodge);
		Assert.Equal("mouse_left", yamlConfig.Current.KeyNormalAttack);
	}

	[Fact]
	public void GameConfig_ShouldSupportPythonCompatibleFieldsAndDefaults()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "gamepad_type: ds4\nturn_dx: -4.5\ngamepad_turn_speed: 1200\nxbox_key_press_time: 0.08\nds4_key_press_time: 0.06\noriginal_hdr_value: enabled\nxbox_action_compendium:\n  - lt\n  - x";
		File.WriteAllText(Path.Combine(text, "game.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<GameConfig> yamlConfig = new YamlConfig<GameConfig>(environment, "game", null, 0);
		Assert.Equal("ds4", yamlConfig.Current.ControlMethod);
		Assert.Equal(-4.5f, yamlConfig.Current.TurnDx);
		Assert.Equal(1200f, yamlConfig.Current.GamepadTurnSpeed);
		Assert.Equal(0.08f, yamlConfig.Current.XboxKeyPressTime);
		Assert.Equal(0.06f, yamlConfig.Current.Ds4KeyPressTime);
		Assert.Equal("enabled", yamlConfig.Current.OriginalHdrValue);
		Assert.False(yamlConfig.Current.BackgroundMode);
		Assert.Equal("xbox", yamlConfig.Current.BackgroundGamepadType);
		Assert.Equal(0.05f, yamlConfig.Current.MouseFlashDuration);
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "lt";
		span[1] = "x";
		Assert.Equal<List<string>>(list, yamlConfig.Current.XboxActionCompendium);
	}

	[Fact]
	public void GameConfig_ShouldSupportEnterGameLaunchFields()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "type_input_way: input\nlaunch_argument: true\nscreen_size: 2560x1440\nfull_screen: \"1\"\npopup_window: true\nmonitor: \"2\"\nlaunch_argument_advance: \"-force-d3d11\"";
		File.WriteAllText(Path.Combine(text, "game.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<GameConfig> yamlConfig = new YamlConfig<GameConfig>(environment, "game", null, 0);
		Assert.Equal("input", yamlConfig.Current.TypeInputWay);
		Assert.True(yamlConfig.Current.LaunchArgument);
		Assert.Equal("2560x1440", yamlConfig.Current.ScreenSize);
		Assert.Equal("1", yamlConfig.Current.FullScreen);
		Assert.True(yamlConfig.Current.PopupWindow);
		Assert.Equal("2", yamlConfig.Current.Monitor);
		Assert.Equal("-force-d3d11", yamlConfig.Current.LaunchArgumentAdvance);
	}

	[Fact]
	public void GameAccountConfig_ShouldSupportEnterGameAccountFields()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "game_path: 'D:\\Games\\ZenlessZoneZero Game\\ZenlessZoneZero.exe'\ngame_language: en\naccount: user@example.com\npassword: secret\nbilibili_account_name: BUser";
		File.WriteAllText(Path.Combine(text, "game_account.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<GameAccountConfig> yamlConfig = new YamlConfig<GameAccountConfig>(environment, "game_account", null, 0);
		Assert.Equal("D:\\Games\\ZenlessZoneZero Game\\ZenlessZoneZero.exe", yamlConfig.Current.GamePath);
		Assert.Equal("en", yamlConfig.Current.GameLanguage);
		Assert.Equal("user@example.com", yamlConfig.Current.Account);
		Assert.Equal("secret", yamlConfig.Current.Password);
		Assert.Equal("BUser", yamlConfig.Current.BilibiliAccountName);
		Assert.Equal(4, yamlConfig.Current.GameRefreshHourOffset);
	}

	[Fact]
	public void ModelConfig_ShouldDeserializeCorrectly()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		string contents = "\nflash_classifier_gpu: true\nlost_void_det: yolov8n-736-lost-void-det-20250622\n";
		File.WriteAllText(Path.Combine(text, "model.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<ZzzOd.GameLogic.Config.ModelConfig> yamlConfig = new YamlConfig<ZzzOd.GameLogic.Config.ModelConfig>(environment, "model");
		Assert.True(yamlConfig.Current.FlashClassifierGpu);
		Assert.Equal("yolov8n-640-flash-20250906", yamlConfig.Current.FlashClassifierBackup);
		Assert.Equal("yolov8n-736-lost-void-det-20250622", yamlConfig.Current.LostVoidDet);
		Assert.Equal("yolov8n-736-lost-void-det-20250921", yamlConfig.Current.LostVoidDetBackup);
		Assert.Equal("yolov8s-736-hollow-zero-event-1130", yamlConfig.Current.HollowZeroEventBackup);
		Assert.False(yamlConfig.Current.LostVoidDetGpu);
	}

	[Fact]
	public void ModelConfig_ShouldResolveOcrProfileFromStableField()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		string contents = "ocr_profile: v6-small";
		File.WriteAllText(Path.Combine(text, "model.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<ZzzOd.GameLogic.Config.ModelConfig> yamlConfig = new YamlConfig<ZzzOd.GameLogic.Config.ModelConfig>(environment, "model");
		Assert.Equal("v6-small", yamlConfig.Current.OcrProfile);
		Assert.Null(yamlConfig.Current.Ocr);
		Assert.Equal("v6-small", yamlConfig.Current.ResolveOcrProfile().Profile.Id);
	}

	[Fact]
	public void ModelConfig_ShouldResolveLegacyOcrField()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		string contents = "ocr: ppocrv6";
		File.WriteAllText(Path.Combine(text, "model.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<ZzzOd.GameLogic.Config.ModelConfig> yamlConfig = new YamlConfig<ZzzOd.GameLogic.Config.ModelConfig>(environment, "model");
		Assert.Null(yamlConfig.Current.OcrProfile);
		Assert.Equal("ppocrv6", yamlConfig.Current.Ocr);
		Assert.Equal("v6-small", yamlConfig.Current.ResolveOcrProfile().Profile.Id);
		Assert.True(yamlConfig.Current.ResolveOcrProfile().UsedLegacySelection);
	}

	[Fact]
	public void ModelConfig_ShouldPreferStableOcrProfileOverLegacyOcr()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		string contents = "ocr_profile: v5-server\nocr: ppocrv6";
		File.WriteAllText(Path.Combine(text, "model.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<ZzzOd.GameLogic.Config.ModelConfig> yamlConfig = new YamlConfig<ZzzOd.GameLogic.Config.ModelConfig>(environment, "model");
		Assert.Equal("v5-server", yamlConfig.Current.OcrProfile);
		Assert.Equal("ppocrv6", yamlConfig.Current.Ocr);
		Assert.Equal("v5-server", yamlConfig.Current.ResolveOcrProfile().Profile.Id);
		Assert.False(yamlConfig.Current.ResolveOcrProfile().UsedLegacySelection);
	}

	[Fact]
	public void EnvConfig_AndProjectConfig_ShouldDeserializePythonCompatibleFields()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "env.yml"), "screenshot_method: bitblt\nunknown_future_field: ignored");
		File.WriteAllText(Path.Combine(text, "project.yml"), "project_name: ZenlessZoneZero-OneDragon\ngithub_homepage: https://github.com/OneDragon-Anything/ZenlessZoneZero-OneDragon\nnotice_url: https://one-dragon.com/notice/zzz/notice.json\nqq_link: https://pd.qq.com/g/onedrag00n\nhome_page_link: https://one-dragon.com/zzz/zh/home.html\ndoc_link: https://docs.qq.com/doc/p/example\nscreen_standard_width: 1600\nscreen_standard_height: 900\nunknown_future_field: ignored");
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<EnvConfig> yamlConfig = new YamlConfig<EnvConfig>(environment, "env");
		YamlConfig<ProjectConfig> yamlConfig2 = new YamlConfig<ProjectConfig>(environment, "project");
		Assert.Equal("bitblt", yamlConfig.Current.ScreenshotMethod);
		Assert.Equal("ZenlessZoneZero-OneDragon", yamlConfig2.Current.ProjectName);
		Assert.Equal("https://github.com/OneDragon-Anything/ZenlessZoneZero-OneDragon", yamlConfig2.Current.GithubHomepage);
		Assert.Equal("https://one-dragon.com/notice/zzz/notice.json", yamlConfig2.Current.NoticeUrl);
		Assert.Equal("https://pd.qq.com/g/onedrag00n", yamlConfig2.Current.QqLink);
		Assert.Equal("https://one-dragon.com/zzz/zh/home.html", yamlConfig2.Current.HomePageLink);
		Assert.Equal("https://docs.qq.com/doc/p/example", yamlConfig2.Current.DocLink);
		Assert.Equal(1600, yamlConfig2.Current.ScreenStandardWidth);
		Assert.Equal(900, yamlConfig2.Current.ScreenStandardHeight);
	}

	[Fact]
	public void EnvConfig_AndProjectConfig_ShouldUsePythonDefaults()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<EnvConfig> yamlConfig = new YamlConfig<EnvConfig>(environment, "env");
		YamlConfig<ProjectConfig> yamlConfig2 = new YamlConfig<ProjectConfig>(environment, "project");
		Assert.Equal("auto", yamlConfig.Current.ScreenshotMethod);
		Assert.Equal(string.Empty, yamlConfig2.Current.ProjectName);
		Assert.Equal(string.Empty, yamlConfig2.Current.GithubHomepage);
		Assert.Equal(string.Empty, yamlConfig2.Current.NoticeUrl);
		Assert.Equal(string.Empty, yamlConfig2.Current.QqLink);
		Assert.Equal(string.Empty, yamlConfig2.Current.HomePageLink);
		Assert.Equal(string.Empty, yamlConfig2.Current.DocLink);
		Assert.Equal(1920, yamlConfig2.Current.ScreenStandardWidth);
		Assert.Equal(1080, yamlConfig2.Current.ScreenStandardHeight);
	}

	[Fact]
	public void ModelConfig_ShouldPreserveDiscoveredModelNamesAndReportNonDefaultModel()
	{
		string text = Path.Combine(_rootDirectory, "config");
		Directory.CreateDirectory(text);
		File.WriteAllText(Path.Combine(text, "model.yml"), "flash_classifier: unknown-flash\nhollow_zero_event: unknown-event\nlost_void_det: unknown-lost-void");
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<ZzzOd.GameLogic.Config.ModelConfig> yamlConfig = new YamlConfig<ZzzOd.GameLogic.Config.ModelConfig>(environment, "model");
		Assert.Equal("unknown-flash", yamlConfig.Current.FlashClassifier);
		Assert.Equal("unknown-event", yamlConfig.Current.HollowZeroEvent);
		Assert.Equal("unknown-lost-void", yamlConfig.Current.LostVoidDet);
		Assert.True(yamlConfig.Current.UsingOldModel());
		yamlConfig.Current.LostVoidDet = "yolov8n-736-lost-void-det-20250622";
		Assert.True(yamlConfig.Current.UsingOldModel());
	}

	[Fact]
	public void TeamConfig_ShouldDeserializeCorrectly()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "\nteam_list:\n  - name: 测试队伍1\n    auto_battle: 全配队通用\n    agent_id_list:\n      - agent1\n      - agent2\n";
		File.WriteAllText(Path.Combine(text, "team.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<TeamConfig> yamlConfig = new YamlConfig<TeamConfig>(environment, "team", null, 0);
		Assert.Equal(20, yamlConfig.Current.TeamList.Count);
		Assert.Equal("测试队伍1", yamlConfig.Current.TeamList[0].Name);
		Assert.Equal("agent1", yamlConfig.Current.TeamList[0].AgentIdList[0]);
		Assert.Equal("unknown", yamlConfig.Current.TeamList[0].AgentIdList[2]);
	}

	[Fact]
	public void TeamConfig_ShouldFillDefaultTeamsOnLoad()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "team_list:\n  - name: \"\"\n    auto_battle: \"\"\n    agent_id_list:\n      - anby\n      - nicole";
		File.WriteAllText(Path.Combine(text, "team.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		YamlConfig<TeamConfig> yamlConfig = new YamlConfig<TeamConfig>(environment, "team", null, 0);
		Assert.Equal(20, yamlConfig.Current.TeamList.Count);
		Assert.Equal(0, yamlConfig.Current.TeamList[0].Idx);
		Assert.Equal("编队1", yamlConfig.Current.TeamList[0].Name);
		Assert.Equal("全配队通用", yamlConfig.Current.TeamList[0].AutoBattle);
		int num = 3;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "anby";
		span[1] = "nicole";
		span[2] = "unknown";
		Assert.Equal<List<string>>(list, yamlConfig.Current.TeamList[0].AgentIdList);
		Assert.Equal(19, yamlConfig.Current.TeamList[19].Idx);
		Assert.Equal("编队20", yamlConfig.Current.TeamList[19].Name);
		Assert.Equal("全配队通用", yamlConfig.Current.TeamList[19].AutoBattle);
		num = 3;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<string> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = "unknown";
		span2[1] = "unknown";
		span2[2] = "unknown";
		Assert.Equal<List<string>>(list2, yamlConfig.Current.TeamList[19].AgentIdList);
	}

	[Fact]
	public void BattleAssistantConfig_ShouldDeserializePythonCompatibleFields()
	{
		string text = Path.Combine(_rootDirectory, "config", "00");
		Directory.CreateDirectory(text);
		string contents = "dodge_assistant_config: 闪避-测试\nscreenshot_interval: 0.05\ncontrol_method: xbox\nauto_battle_config: 比利-测试\nuse_merged_file: false\nauto_ultimate_enabled: true\nunknown_future_field: ignored";
		File.WriteAllText(Path.Combine(text, "battle_assistant.yml"), contents);
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		BattleAssistantConfig battleAssistantConfig = BattleAssistantConfig.Load(environment, 0);
		Assert.Equal("闪避-测试", battleAssistantConfig.DodgeAssistantConfig);
		Assert.Equal(0.05, battleAssistantConfig.ScreenshotInterval);
		Assert.Equal("xbox", battleAssistantConfig.ControlMethod);
		Assert.Equal("比利-测试", battleAssistantConfig.AutoBattleConfig);
		Assert.False(battleAssistantConfig.UseMergedFile);
		Assert.True(battleAssistantConfig.AutoUltimateEnabled);
	}

	[Fact]
	public void BattleAssistantConfig_ShouldUsePythonDefaults()
	{
		OneDragonEnvironment environment = new OneDragonEnvironment(_rootDirectory);
		BattleAssistantConfig battleAssistantConfig = BattleAssistantConfig.Load(environment, 0);
		Assert.Equal("闪避", battleAssistantConfig.DodgeAssistantConfig);
		Assert.Equal(0.02, battleAssistantConfig.ScreenshotInterval);
		Assert.Equal("keyboard", battleAssistantConfig.ControlMethod);
		Assert.Equal("全配队通用", battleAssistantConfig.AutoBattleConfig);
		Assert.True(battleAssistantConfig.UseMergedFile);
		Assert.False(battleAssistantConfig.AutoUltimateEnabled);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}

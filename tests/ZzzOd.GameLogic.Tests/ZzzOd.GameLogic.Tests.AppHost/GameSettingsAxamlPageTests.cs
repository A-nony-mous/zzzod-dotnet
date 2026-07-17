using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Operations.EnterGame;
using ZzzOd.Gui.Services.Windows;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class GameSettingsAxamlPageTests
{
	private sealed class RecordingHdrStore : IAutoHdrPreferenceStore
	{
		public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

		public List<string> Reads { get; } = new List<string>();

		public List<string> Deletes { get; } = new List<string>();

		public string? ReadValue(string gamePath)
		{
			Reads.Add(gamePath);
			return Values.GetValueOrDefault(gamePath);
		}

		public void WriteValue(string gamePath, string value)
		{
			Values[gamePath] = value;
		}

		public void DeleteValue(string gamePath)
		{
			Deletes.Add(gamePath);
			Values.Remove(gamePath);
		}
	}

	[Fact]
	public void AxamlPageKeepsPythonOrderTextAndFluentControls()
	{
		string text = File.ReadAllText(FindWorkspaceFile("zzzod-dotnet", "src", "ZzzOd.Gui", "Pages", "Settings", "ZzzGameSettingsPage.axaml"));
		string actualString = File.ReadAllText(FindWorkspaceFile("zzzod-dotnet", "src", "ZzzOd.Gui", "Pages", "Settings", "ZzzGameSettingsPage.cs"));
		Assert.True(text.IndexOf("Content=\"设置说明\"", StringComparison.Ordinal) < text.IndexOf("Text=\"游戏基础\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Text=\"游戏基础\"", StringComparison.Ordinal) < text.IndexOf("Content=\"输入方式\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Content=\"输入方式\"", StringComparison.Ordinal) < text.IndexOf("Header=\"后台模式（测试版）\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Header=\"后台模式（测试版）\"", StringComparison.Ordinal) < text.IndexOf("Content=\"切换 HDR 状态\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Content=\"切换 HDR 状态\"", StringComparison.Ordinal) < text.IndexOf("Header=\"启动参数\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Header=\"启动参数\"", StringComparison.Ordinal) < text.IndexOf("Text=\"按键设置\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Text=\"按键设置\"", StringComparison.Ordinal) < text.IndexOf("Content=\"操控方式\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Content=\"操控方式\"", StringComparison.Ordinal) < text.IndexOf("Header=\"键盘按键\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Header=\"键盘按键\"", StringComparison.Ordinal) < text.IndexOf("Header=\"手柄按键\"", StringComparison.Ordinal));
		Assert.Contains("SettingsExpander", text, StringComparison.Ordinal);
		Assert.Contains("SettingsExpanderItem", text, StringComparison.Ordinal);
		Assert.Contains("FAComboBox", text, StringComparison.Ordinal);
		Assert.Contains("NumberBox", text, StringComparison.Ordinal);
		Assert.Contains("ToggleSwitch", text, StringComparison.Ordinal);
		Assert.Contains("InfoBar", text, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzSettingCard", text, StringComparison.Ordinal);
		Assert.DoesNotContain("对应 Python", text, StringComparison.Ordinal);
		Assert.DoesNotContain("PageModel", text, StringComparison.Ordinal);
		Assert.Contains("游戏路径、输入方式等基础设置，建议首次使用前检查一遍", text, StringComparison.Ordinal);
		Assert.Contains("出现剪切板失败时切换到输入法", actualString, StringComparison.Ordinal);
		Assert.Contains("如果你不知道这是做什么的 请不要填写", text, StringComparison.Ordinal);
		Assert.Contains("未检测到 vgamepad / ViGEmBus，请先安装虚拟手柄驱动", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("Save(\"hdr\"", actualString, StringComparison.Ordinal);
	}

	[Fact]
	public void GameConfigDefaultsUsePythonCompatibleFullGamepadValues()
	{
		GameConfig gameConfig = new GameConfig();
		Assert.Equal("xbox_a", gameConfig.XboxKeyInteract);
		Assert.Equal("xbox_ls_up", gameConfig.XboxKeyMoveW);
		Assert.Equal("xbox_r_thumb", gameConfig.XboxKeyLock);
		Assert.Equal("ds4_cross", gameConfig.Ds4KeyInteract);
		Assert.Equal("ds4_ls_up", gameConfig.Ds4KeyMoveW);
		Assert.Equal("ds4_r_thumb", gameConfig.Ds4KeyLock);
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "xbox_lt";
		span[1] = "xbox_a";
		Assert.Equal<List<string>>(list, gameConfig.XboxActionCompendium);
		num = 2;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<string> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = "ds4_l2";
		span2[1] = "ds4_cross";
		Assert.Equal<List<string>>(list2, gameConfig.Ds4ActionCompendium);
	}

	[Fact]
	public void ManualHdrServiceWritesExactPythonSettingsPageValues()
	{
		RecordingHdrStore recordingHdrStore = new RecordingHdrStore();
		ZzzWindowsManualAutoHdrService zzzWindowsManualAutoHdrService = new ZzzWindowsManualAutoHdrService(recordingHdrStore);
		Assert.True(zzzWindowsManualAutoHdrService.SetEnabled("D:\\Games\\ZenlessZoneZero.exe", enabled: false));
		Assert.Equal("AutoHDREnable=2096;", recordingHdrStore.Values["D:\\Games\\ZenlessZoneZero.exe"]);
		Assert.True(zzzWindowsManualAutoHdrService.SetEnabled("D:\\Games\\ZenlessZoneZero.exe", enabled: true));
		Assert.Equal("AutoHDREnable=2097;", recordingHdrStore.Values["D:\\Games\\ZenlessZoneZero.exe"]);
		Assert.Empty(recordingHdrStore.Reads);
		Assert.Empty(recordingHdrStore.Deletes);
	}

	private static string FindWorkspaceFile(params string[] relativeParts)
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string fullName = directoryInfo.FullName;
			int num = 0;
			string[] array = new string[1 + relativeParts.Length];
			array[num] = fullName;
			num++;
			ReadOnlySpan<string> readOnlySpan = new ReadOnlySpan<string>(relativeParts);
			readOnlySpan.CopyTo(new Span<string>(array).Slice(num, readOnlySpan.Length));
			num += readOnlySpan.Length;
			string text = Path.Combine(new ReadOnlySpan<string>(array));
			if (File.Exists(text))
			{
				return text;
			}
		}
		throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, relativeParts));
	}
}

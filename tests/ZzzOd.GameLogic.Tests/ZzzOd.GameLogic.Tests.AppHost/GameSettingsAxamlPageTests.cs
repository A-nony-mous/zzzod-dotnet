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

}

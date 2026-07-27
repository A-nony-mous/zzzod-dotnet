using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;

namespace ZzzOd.GameLogic.Tests.AppHost;

[Trait("Category", "GuiHeavy")]
public sealed class LifeOnLineSettingsFlyoutTests
{
	[Fact]
	public void BackendReadsDailyRunTimesFromRealRunRecordFile()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-life-on-line-settings", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(text, "config", "00", "app_run_record"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "models"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "template"));
		Directory.CreateDirectory(Path.Combine(text, "assets", "game_data", "screen_info"));
		File.WriteAllText(Path.Combine(text, "config", "one_dragon.yml"), "instance_list:\n- idx: 0\n  name: '00'\n  active: true\n  active_in_od: true");
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "config";
		buffer[2] = "00";
		buffer[3] = "app_run_record";
		buffer[4] = "life_on_line.yml";
		File.WriteAllText(Path.Combine(buffer), "daily_run_times: 9\n");
		ZzzRuntimeManager zzzRuntimeManager = new ZzzRuntimeManager(text, NullLogger<ZzzRuntimeManager>.Instance);
		ZzzBackendEventBus eventBus = new ZzzBackendEventBus();
		ZzzBattleAssistantRuntimeSource zzzBattleAssistantRuntimeSource = new ZzzBattleAssistantRuntimeSource();
		ZzzLogFanOutLoggerProvider zzzLogFanOutLoggerProvider = new ZzzLogFanOutLoggerProvider(new ZzzRunRoot(text), eventBus);
		try
		{
			ZzzAppBackend zzzAppBackend = new ZzzAppBackend(zzzRuntimeManager, eventBus, zzzBattleAssistantRuntimeSource, zzzLogFanOutLoggerProvider, new ZzzHostModeOptions(ZzzHostMode.ApiOnly), new ZzzApiOptions(), NullLogger<ZzzAppBackend>.Instance);
			ZzzBackendResult<ZzzLifeOnLineRunRecordDto> lifeOnLineRunRecord = zzzAppBackend.GetLifeOnLineRunRecord(0);
			Assert.True(lifeOnLineRunRecord.Success, lifeOnLineRunRecord.Error);
			Assert.NotNull(lifeOnLineRunRecord.Value);
			Assert.Equal(0, lifeOnLineRunRecord.Value.InstanceIndex);
			Assert.Equal(9, lifeOnLineRunRecord.Value.DailyRunTimes);
		}
		finally
		{
			zzzRuntimeManager.Dispose();
			zzzBattleAssistantRuntimeSource.Dispose();
			zzzLogFanOutLoggerProvider.Dispose();
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
		}
	}
}

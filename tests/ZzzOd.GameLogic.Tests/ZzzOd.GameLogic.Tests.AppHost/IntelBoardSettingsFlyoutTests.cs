using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.AppHost;
using ZzzOd.AppHost.Backend;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Config;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class IntelBoardSettingsFlyoutTests
{
	private sealed class BackendSession : IDisposable
	{
		private readonly ZzzRuntimeManager _runtime;

		private readonly ZzzBattleAssistantRuntimeSource _battleAssistantRuntimeSource;

		private readonly ZzzLogFanOutLoggerProvider _logProvider;

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

	[Fact]
	public void BackendResetPersistsAllPythonProgressFieldsToRealRunRecord()
	{
		string text = CreateTempRunRoot();
		try
		{
			string text2 = Path.Combine(text, "config", "03", "app_run_record");
			Directory.CreateDirectory(text2);
			File.WriteAllText(Path.Combine(text2, "intel_board.yml"), "dt: \"20260706\"\nprogress_complete: true\nnotorious_hunt_count: 4\nexpert_challenge_count: 7\nbase_exp: 1250");
			using BackendSession backendSession = new BackendSession(text);
			ZzzBackendResult<bool> zzzBackendResult = backendSession.Backend.ResetIntelBoardProgress(3);
			Assert.True(zzzBackendResult.Success, zzzBackendResult.Error);
			Assert.True(zzzBackendResult.Value);
			IntelBoardConfig config = IntelBoardConfig.Load(new OneDragonEnvironment(text), 3, "one_dragon");
			IntelBoardRunRecord intelBoardRunRecord = IntelBoardRunRecord.Load(new OneDragonEnvironment(text), 3, config);
			Assert.False(intelBoardRunRecord.ProgressComplete);
			Assert.Equal(0, intelBoardRunRecord.NotoriousHuntCount);
			Assert.Equal(0, intelBoardRunRecord.ExpertChallengeCount);
			Assert.Equal(0, intelBoardRunRecord.BaseExp);
		}
		finally
		{
			Directory.Delete(text, recursive: true);
		}
	}

	private static string CreateTempRunRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-intel-board-settings", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}
}

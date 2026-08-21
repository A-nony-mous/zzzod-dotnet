using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Runtime;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Application.EmailApp;
using ZzzOd.GameLogic.Application.EngagementReward;
using ZzzOd.GameLogic.Application.HouHouBakery;
using ZzzOd.GameLogic.Application.RiduWeekly;
using ZzzOd.GameLogic.Application.TrigramsCollection;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class DailyApplicationScreenScopeParityTests
{
	private sealed class ScopeProbe(string appId)
	{
		public bool IsExpectedScopeActive { get; private set; }

		public bool IsOtherAppScopeActive { get; private set; }

		public OperationResult Observe(ZContext context)
		{
			IsExpectedScopeActive = context.ScreenContext.IsScreenActive(appId + " 本地页面");
			IsOtherAppScopeActive = context.ScreenContext.IsScreenActive("email 本地页面") && appId != "email";
			return new OperationResult(IsSuccess: true, "scope observed");
		}
	}

	private sealed class ScopeEngagementRewardFlow(ScopeProbe probe) : IEngagementRewardAppFlow
	{
		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(probe.Observe(context));
		}
	}

	private sealed class ScopeHouHouBakeryFlow(ScopeProbe probe) : IHouHouBakeryFlow
	{
		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(probe.Observe(context));
		}
	}

	private sealed class ScopeRiduWeeklyFlow(ScopeProbe probe) : IRiduWeeklyAppFlow
	{
		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(probe.Observe(context));
		}
	}

	private sealed class ScopeTrigramsCollectionFlow(ScopeProbe probe) : ITrigramsCollectionFlow
	{
		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(probe.Observe(context));
		}
	}

	private sealed class ScopeEmailFlow(ScopeProbe probe) : IEmailAppFlow
	{
		public Task<OperationResult> RunAsync(ZContext context, CancellationToken cancellationToken)
		{
			return Task.FromResult(probe.Observe(context));
		}
	}

	private sealed class ScopeReadyController : ControllerBase
	{
		public override bool IsGameWindowReady => true;

		public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
		{
			return true;
		}

		public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
		}

		public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
		}

		public override void InputText(string text)
		{
		}

		public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
		}

		protected override Mat? GetScreenshot(bool independent = false)
		{
			return null;
		}
	}

	public static TheoryData<string> ApplicationIds => new TheoryData<string> { "engagement_reward", "hou_hou_bakery", "ridu_weekly", "trigrams_collection", "email" };

	[Theory]
	[MemberData("ApplicationIds", new object[] { })]
	public async Task DailyApplication_UsesPythonEquivalentScreenScopeForTheWholeFlow(string appId)
	{
		string rootDirectory = CreateTempRoot();
		try
		{
			WriteScreenYaml(rootDirectory);
			using ZContext context = new ZContext(new OneDragonEnvironment(rootDirectory, rootDirectory));
			context.AttachController(new ScopeReadyController());
			context.ScreenContext.Reload();
			ScopeProbe probe = new ScopeProbe(appId);
			IApplication app = CreateApp(context, appId, probe);
			Assert.True((await app.ExecuteAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2L))).IsSuccess);
			Assert.True(probe.IsExpectedScopeActive);
			Assert.False(probe.IsOtherAppScopeActive);
			Assert.Null(context.ScreenContext.ActiveScreenNames);
		}
		finally
		{
			Directory.Delete(rootDirectory, recursive: true);
		}
	}

	private static IApplication CreateApp(ZContext context, string appId, ScopeProbe probe)
	{
		if (1 == 0)
		{
		}
		IApplication result = appId switch
		{
			"engagement_reward" => new EngagementRewardApp(context, null, new ScopeEngagementRewardFlow(probe)), 
			"hou_hou_bakery" => new HouHouBakeryApp(context, null, new ScopeHouHouBakeryFlow(probe)), 
			"ridu_weekly" => new RiduWeeklyApp(context, null, new ScopeRiduWeeklyFlow(probe)), 
			"trigrams_collection" => new TrigramsCollectionApp(context, null, new ScopeTrigramsCollectionFlow(probe)), 
			"email" => new EmailApp(context, null, new ScopeEmailFlow(probe)), 
			_ => throw new ArgumentOutOfRangeException("appId", appId, null), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static string CreateTempRoot()
	{
		string text = Path.Combine(Path.GetTempPath(), "zzzod-dotnet-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		return text;
	}

	private static void WriteScreenYaml(string rootDirectory)
	{
		string text = Path.Combine(rootDirectory, "assets", "game_data", "screen_info");
		Directory.CreateDirectory(text);
		ScreenSeed.WriteScreens(text, "- screen_id: global\n  screen_name: 全局页面\n  area_list: []\n- screen_id: engagement_reward_local\n  screen_name: engagement_reward 本地页面\n  app_id: engagement_reward\n  area_list: []\n- screen_id: hou_hou_bakery_local\n  screen_name: hou_hou_bakery 本地页面\n  app_id: hou_hou_bakery\n  area_list: []\n- screen_id: ridu_weekly_local\n  screen_name: ridu_weekly 本地页面\n  app_id: ridu_weekly\n  area_list: []\n- screen_id: trigrams_collection_local\n  screen_name: trigrams_collection 本地页面\n  app_id: trigrams_collection\n  area_list: []\n- screen_id: email_local\n  screen_name: email 本地页面\n  app_id: email\n  area_list: []");
	}
}

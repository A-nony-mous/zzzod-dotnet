using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using Xunit;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// Tests real-game preflight session resolution order.
/// </summary>
public sealed class RealGamePreflightRunnerTests
{
	/// <summary>
	/// Missing game window runs the full open-and-enter flow before selected applications.
	/// </summary>
	[Fact]
	public async Task ResolveSession_RunsOpenAndEnterGame_WhenWindowDoesNotExist()
	{
		List<string> calls = new List<string>();
		List<string> summary = new List<string>();
		OperationResult result = await RealGamePreflightRunner.ResolveSessionAsync(windowExists: false, windowReady: false, null, delegate
		{
			calls.Add("open");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
		}, summary);
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Equal<List<string>>(new List<string>(1) { "open" }, calls);
		Assert.Contains((IEnumerable<string>)summary, (Predicate<string>)((string item) => item.Contains("OpenAndEnterGame", StringComparison.Ordinal)));
	}

	/// <summary>
	/// Application runs are blocked when an existing game window cannot be initialized.
	/// </summary>
	[Fact]
	public async Task ResolveSession_ReturnsFailure_WhenExistingWindowIsNotReady()
	{
		List<string> calls = new List<string>();
		List<string> summary = new List<string>();
		OperationResult result = await RealGamePreflightRunner.ResolveSessionAsync(windowExists: true, windowReady: false, "窗口句柄无效", delegate
		{
			calls.Add("open");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
		}, summary);
		Assert.False(result.IsSuccess);
		Assert.Equal("窗口句柄无效", result.Status);
		Assert.Empty(calls);
		Assert.Contains("窗口句柄无效", (IEnumerable<string>)summary);
	}

	/// <summary>
	/// Existing non-world screens are recovered before selected applications run.
	/// </summary>
	[Fact]
	public async Task ResolveSession_RecoversExistingNonWorldWindow_BeforeApplication()
	{
		List<string> calls = new List<string>();
		OperationResult result = await RealGamePreflightRunner.ResolveSessionAsync(windowExists: true, windowReady: true, null, delegate
		{
			calls.Add("open");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: false, "未到达大世界"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
		});
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Equal<List<string>>(new List<string>(2) { "wait", "back" }, calls);
	}

	/// <summary>
	/// A confirmed world screen completes preflight without recovery.
	/// </summary>
	[Fact]
	public async Task ResolveExistingWindow_ReturnsWorld_WhenWaitNormalWorldSucceeds()
	{
		List<string> calls = new List<string>();
		List<string> summary = new List<string>();
		OperationResult result = await RealGamePreflightRunner.ResolveExistingWindowAsync(delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "返回大世界"));
		}, delegate
		{
			calls.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
		}, summary);
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Equal<List<string>>(new List<string>(1) { "wait" }, calls);
		Assert.Contains((IEnumerable<string>)summary, (Predicate<string>)((string item) => item.Contains("runnable world state", StringComparison.Ordinal)));
	}

	/// <summary>
	/// A non-world screen attempts normal-world recovery before login handling.
	/// </summary>
	[Fact]
	public async Task ResolveExistingWindow_TriesBackToNormalWorld_BeforeEnterGame()
	{
		List<string> calls = new List<string>();
		List<string> summary = new List<string>();
		OperationResult result = await RealGamePreflightRunner.ResolveExistingWindowAsync(delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: false, "未到达大世界"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: true, "大世界-普通"));
		}, delegate
		{
			calls.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
		}, summary);
		Assert.True(result.IsSuccess);
		Assert.Equal("大世界-普通", result.Status);
		Assert.Equal<List<string>>(new List<string>(2) { "wait", "back" }, calls);
		Assert.Contains((IEnumerable<string>)summary, (Predicate<string>)((string item) => item.Contains("Trying BackToNormalWorld", StringComparison.Ordinal)));
	}

	/// <summary>
	/// Login handling runs after normal-world recovery fails.
	/// </summary>
	[Fact]
	public async Task ResolveExistingWindow_RunsEnterGame_WhenBackToNormalWorldFails()
	{
		List<string> calls = new List<string>();
		List<string> summary = new List<string>();
		OperationResult result = await RealGamePreflightRunner.ResolveExistingWindowAsync(delegate
		{
			calls.Add("wait");
			return Task.FromResult(new OperationResult(IsSuccess: false, "未到达大世界"));
		}, delegate
		{
			calls.Add("back");
			return Task.FromResult(new OperationResult(IsSuccess: false, "未知画面"));
		}, delegate
		{
			calls.Add("enter");
			return Task.FromResult(new OperationResult(IsSuccess: true, "进入游戏"));
		}, summary);
		Assert.True(result.IsSuccess);
		Assert.Equal("进入游戏", result.Status);
		Assert.Equal<List<string>>(new List<string>(3) { "wait", "back", "enter" }, calls);
		Assert.Contains((IEnumerable<string>)summary, (Predicate<string>)((string item) => item.Contains("Executing EnterGame", StringComparison.Ordinal)));
	}

	/// <summary>
	/// NPC dialogue snapshots are blocked before BackToNormalWorld can advance account dialogue.
	/// </summary>
	[Fact]
	public void ScreenGuard_BlocksNpcDialogueWithoutRecognizedScreen()
	{
		RealGamePreflightGuardResult realGamePreflightGuardResult = RealGamePreflightScreenGuard.Evaluate(new RealGamePreflightScreenState(null, null, new string[2] { "安东", "绳匠" }));
		Assert.True(realGamePreflightGuardResult.IsBlocked);
		Assert.Contains("NPC dialogue", realGamePreflightGuardResult.Reason, StringComparison.Ordinal);
	}

	/// <summary>
	/// Dialogue can be blocked even when OCR misses 绳匠 and only reads the speaker plus dialogue line.
	/// </summary>
	[Fact]
	public void ScreenGuard_BlocksNpcDialogueLineWithoutRopeProxyText()
	{
		RealGamePreflightGuardResult realGamePreflightGuardResult = RealGamePreflightScreenGuard.Evaluate(new RealGamePreflightScreenState(null, null, new string[3] { "24H", "安东", "为什么会有人自愿喝这种东西啊！" }));
		Assert.True(realGamePreflightGuardResult.IsBlocked);
		Assert.Contains("安东", realGamePreflightGuardResult.Reason, StringComparison.Ordinal);
	}

	/// <summary>
	/// Intel board commission details are closeable app pages, so preflight can recover them with BackToNormalWorld.
	/// </summary>
	[Fact]
	public void ScreenGuard_AllowsIntelBoardCommissionDetail()
	{
		RealGamePreflightGuardResult realGamePreflightGuardResult = RealGamePreflightScreenGuard.Evaluate(new RealGamePreflightScreenState(null, null, new string[5] { "有大师姐撑腰的", "可接取01天04时", "成功挑战1次指定恶名狩猎即可达成委托", "高危预警！恶名狩猎目标现身空洞，急寻援手！", "接取委托" }));
		Assert.False(realGamePreflightGuardResult.IsBlocked);
	}

	/// <summary>
	/// Intel board list pages are closeable app pages, so preflight can recover them with BackToNormalWorld.
	/// </summary>
	[Fact]
	public void ScreenGuard_AllowsIntelBoardListPage()
	{
		RealGamePreflightGuardResult realGamePreflightGuardResult = RealGamePreflightScreenGuard.Evaluate(new RealGamePreflightScreenState(null, null, new string[4] { "周期剩余时间:04天13时", "委托管理", "周期内可获取0/1000", "暂无已发布或已接取的委托，请点击发布委托或前往情报板接取委托" }));
		Assert.False(realGamePreflightGuardResult.IsBlocked);
	}

	/// <summary>
	/// Intel board running commission details are closeable app pages, so preflight can recover them with BackToNormalWorld.
	/// </summary>
	[Fact]
	public void ScreenGuard_AllowsIntelBoardRunningCommissionDetail()
	{
		RealGamePreflightGuardResult realGamePreflightGuardResult = RealGamePreflightScreenGuard.Evaluate(new RealGamePreflightScreenState(null, null, new string[5] { "委托进行中26分31秒", "成功挑战1次指定恶名狩猎即可达成委托", "高危预警！恶名狩猎目标现身空洞，急寻援手！", "前往", "放弃委托" }));
		Assert.False(realGamePreflightGuardResult.IsBlocked);
	}

	/// <summary>
	/// Recognized screens continue through the normal preflight resolver.
	/// </summary>
	[Fact]
	public void ScreenGuard_AllowsRecognizedWorldScreen()
	{
		RealGamePreflightGuardResult realGamePreflightGuardResult = RealGamePreflightScreenGuard.Evaluate(new RealGamePreflightScreenState("大世界-普通", "大世界-普通", new string[2] { "安东", "绳匠" }));
		Assert.False(realGamePreflightGuardResult.IsBlocked);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Controller;
using OneDragon.Core.Runtime;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations.EnterGame;

namespace ZzzOd.GameLogic.Application.OneDragonApp;

/// <summary>
/// 按 BaselineParity OneDragonApp 的实例顺序执行应用组。
/// </summary>
public sealed class ZOneDragonApp : ZApplication
{
	/// <summary>所有实例运行完成。</summary>
	public const string StatusAllDone = "全部结束";

	/// <summary>继续下一个实例。</summary>
	public const string StatusNext = "下一个";

	/// <summary>未登录时继续下一个实例。</summary>
	public const string StatusNoLogin = "下一个";

	private readonly int _requestedInstanceIndex;

	private readonly string _groupId;

	private readonly IZOneDragonCompletionPlatform _completionPlatform;

	private readonly Func<ZContext, CancellationToken, Task<OperationResult>>? _enterGameAsync;

	private readonly Func<ZContext, CancellationToken, Task<OperationResult>>? _switchAccountAsync;

	/// <summary>
	/// 初始化一条龙应用。
	/// </summary>
	public ZOneDragonApp(
		ZContext context,
		int instanceIndex,
		string groupId = "one_dragon",
		ZApplicationRunRecord? runRecord = null,
		IZOneDragonCompletionPlatform? completionPlatform = null,
		Func<ZContext, CancellationToken, Task<OperationResult>>? enterGameAsync = null,
		Func<ZContext, CancellationToken, Task<OperationResult>>? switchAccountAsync = null)
		: base(context, "one_dragon", runRecord, "一条龙")
	{
		_requestedInstanceIndex = instanceIndex;
		_groupId = (string.IsNullOrWhiteSpace(groupId) ? "one_dragon" : groupId);
		_completionPlatform = completionPlatform ?? new WindowsOneDragonCompletionPlatform();
		_enterGameAsync = enterGameAsync;
		_switchAccountAsync = switchAccountAsync;
	}

	/// <inheritdoc />
	protected override Task<OperationResult> EnterGameAsync(CancellationToken cancellationToken)
	{
		try
		{
			EnsureActiveInstanceSelected();
		}
		catch (InvalidOperationException ex)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, ex.Message));
		}
		return base.EnterGameAsync(cancellationToken);
	}

	/// <inheritdoc />
	protected override async Task<OperationResult> ExecuteCoreAsync(CancellationToken cancellationToken)
	{
		OneDragonConfig oneDragonConfig = LoadOneDragonConfig();
		int startIndex;
		OneDragonInstanceConfigItem[] instances = ResolveInstances(oneDragonConfig, out startIndex);
		int currentIndex = startIndex;
		if (base.Context.InstanceIndex != instances[currentIndex].Idx)
		{
			base.Context.SwitchInstance(instances[currentIndex].Idx);
		}
		List<ZOneDragonApplicationResult> results = new List<ZOneDragonApplicationResult>();
		int nextInstanceIndex;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int currentInstanceIndex = instances[currentIndex].Idx;
			await RunGroupAsync(currentInstanceIndex, results, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			int nextIndex = currentIndex + 1;
			if (nextIndex >= instances.Length)
			{
				nextIndex = 0;
			}
			nextInstanceIndex = instances[nextIndex].Idx;
			if (GameAccountConfig.IsDifferentGamePath(base.Context.Environment, currentInstanceIndex, nextInstanceIndex))
			{
				ControllerBase lastController = base.Context.Controller;
				OperationResult closeResult = await _completionPlatform.CloseGameAsync(lastController, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!closeResult.IsSuccess)
				{
					return ZApplication.Fail(closeResult.Status, new ZOneDragonRunSummary(currentInstanceIndex, _groupId, results));
				}
				base.Context.SwitchInstance(nextInstanceIndex);
				currentIndex = nextIndex;
				if (currentIndex == startIndex)
				{
					return HasFailure(results)
						? ZApplication.Fail("一条龙应用执行失败", new ZOneDragonRunSummary(nextInstanceIndex, _groupId, results))
						: await CompleteNaturallyAsync(nextInstanceIndex, results, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				OperationResult enterResult = await ExecuteEnterGameAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!enterResult.IsSuccess)
				{
					return ZApplication.Fail(enterResult.Status, new ZOneDragonRunSummary(nextInstanceIndex, _groupId, results));
				}
				continue;
			}
			base.Context.SwitchInstance(nextInstanceIndex);
			currentIndex = nextIndex;
			if (instances.Length > 1)
			{
				OperationResult switchResult = await ExecuteSwitchAccountAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!switchResult.IsSuccess)
				{
					return ZApplication.Fail(switchResult.Status, new ZOneDragonRunSummary(nextInstanceIndex, _groupId, results));
				}
			}
			if (currentIndex == startIndex)
			{
				break;
			}
		}
		return HasFailure(results)
			? ZApplication.Fail("一条龙应用执行失败", new ZOneDragonRunSummary(nextInstanceIndex, _groupId, results))
			: await CompleteNaturallyAsync(nextInstanceIndex, results, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// 一整轮实例轮转中是否存在应用执行失败。
	/// </summary>
	private static bool HasFailure(IEnumerable<ZOneDragonApplicationResult> results)
	{
		return results.Any((ZOneDragonApplicationResult result) => !result.IsSuccess);
	}

	private OneDragonConfig LoadOneDragonConfig()
	{
		OneDragonEnvironment environment = base.Context.Environment;
		IReadOnlyList<string> subDirectories = Array.Empty<string>();
		return new YamlConfig<OneDragonConfig>(environment, "one_dragon", null, null, subDirectories).Current;
	}

	private OneDragonInstanceConfigItem[] ResolveInstances(OneDragonConfig oneDragonConfig, out int startIndex)
	{
		OneDragonInstanceConfigItem current = oneDragonConfig.InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Active);
		if (current == null)
		{
			throw new InvalidOperationException("未找到当前启用的实例。");
		}
		IReadOnlyList<OneDragonInstanceConfigItem> readOnlyList = (string.Equals(oneDragonConfig.InstanceRun, "全部实例", StringComparison.Ordinal) ? ((IReadOnlyList<OneDragonInstanceConfigItem>)oneDragonConfig.InstanceList.Where((OneDragonInstanceConfigItem item) => item.ActiveInOneDragon).ToArray()) : ((IReadOnlyList<OneDragonInstanceConfigItem>)new OneDragonInstanceConfigItem[1] { current }));
		if (readOnlyList.Count == 0)
		{
			readOnlyList = new OneDragonInstanceConfigItem[] { current };
		}
		OneDragonInstanceConfigItem[] array = readOnlyList.ToArray();
		startIndex = Array.FindIndex(array, (OneDragonInstanceConfigItem item) => item.Idx == current.Idx);
		if (startIndex < 0)
		{
			startIndex = 0;
		}
		if (_requestedInstanceIndex != current.Idx)
		{
			base.Context.Logger.Warning("一条龙请求实例 {RequestedInstanceIndex} 与当前启用实例 {ActiveInstanceIndex} 不一致，按当前启用实例运行。", _requestedInstanceIndex, current.Idx);
		}
		return array;
	}

	private void EnsureActiveInstanceSelected()
	{
		OneDragonInstanceConfigItem oneDragonInstanceConfigItem = LoadOneDragonConfig().InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Active);
		if (oneDragonInstanceConfigItem == null)
		{
			throw new InvalidOperationException("未找到当前启用的实例。");
		}
		if (base.Context.InstanceIndex != oneDragonInstanceConfigItem.Idx)
		{
			base.Context.SwitchInstance(oneDragonInstanceConfigItem.Idx);
		}
	}

	private async Task RunGroupAsync(int instanceIndex, ICollection<ZOneDragonApplicationResult> results, CancellationToken cancellationToken)
	{
		YamlConfig<OneDragonApplicationGroupConfig> config = new YamlConfig<OneDragonApplicationGroupConfig>(base.Context.Environment, "_group", null, instanceIndex, new string[] { _groupId });
		foreach (OneDragonApplicationConfigItem item in config.Current.AppList)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (string.Equals(item.AppId, "one_dragon", StringComparison.Ordinal))
			{
				continue;
			}
			if (!item.Enabled)
			{
				results.Add(new ZOneDragonApplicationResult(instanceIndex, item.AppId, IsSuccess: true, "应用未启用 " + item.AppId));
				continue;
			}
			if (!base.Context.RunContext.IsAppRegistered(item.AppId))
			{
				throw new InvalidOperationException("未找到应用 " + item.AppId);
			}
			IApplication application = base.Context.RunContext.GetApplication(item.AppId, instanceIndex, _groupId);
			string appName = base.Context.RunContext.GetApplicationName(item.AppId);
			IApplicationRunRecord runRecord = base.Context.RunContext.GetRunRecord(item.AppId, instanceIndex);
			runRecord.CheckAndUpdateStatus();
			if (runRecord is ZApplicationRunRecord zRunRecord && zRunRecord.IsDone)
			{
				results.Add(new ZOneDragonApplicationResult(instanceIndex, item.AppId, IsSuccess: true, "应用已完成 " + appName));
				continue;
			}
			OperationResult result = await application.ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			results.Add(new ZOneDragonApplicationResult(instanceIndex, item.AppId, result.IsSuccess, result.Status));
		}
	}

	private async Task<OperationResult> ExecuteEnterGameAsync(CancellationToken cancellationToken)
	{
		return _enterGameAsync != null
			? await _enterGameAsync(base.Context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)
			: await new OpenAndEnterGame(base.Context).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<OperationResult> ExecuteSwitchAccountAsync(CancellationToken cancellationToken)
	{
		return _switchAccountAsync != null
			? await _switchAccountAsync(base.Context, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)
			: await new SwitchAccount(base.Context).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<OperationResult> CompleteNaturallyAsync(int instanceIndex, ICollection<ZOneDragonApplicationResult> results, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		string afterDone = LoadOneDragonConfig().AfterDone;
		OperationResult afterDoneResult = await ExecuteAfterDoneAsync(afterDone, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!afterDoneResult.IsSuccess)
		{
			return ZApplication.Fail(afterDoneResult.Status, new ZOneDragonRunSummary(instanceIndex, _groupId, results.ToArray()));
		}
		return ZApplication.Success(StatusAllDone, new ZOneDragonRunSummary(instanceIndex, _groupId, results.ToArray()));
	}

	private async Task<OperationResult> ExecuteAfterDoneAsync(string? afterDone, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		switch (afterDone)
		{
		case null:
		case "":
		case "无":
			base.Context.Logger.Information("一条龙自然完成，结束后操作为无");
			return new OperationResult(IsSuccess: true, "无");
		case "关闭游戏":
		case "关机":
			OperationResult closeResult = await _completionPlatform.CloseGameAsync(base.Context.Controller, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			base.Context.Logger.Information("一条龙结束后关闭游戏，结果 {IsSuccess}，状态 {Status}", closeResult.IsSuccess, closeResult.Status ?? "无");
			if (!closeResult.IsSuccess)
			{
				return closeResult;
			}
			if (afterDone == "关闭游戏")
			{
				return closeResult;
			}
			OperationResult shutdownResult = await _completionPlatform.ShutdownAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			base.Context.Logger.Information("一条龙结束后关机，结果 {IsSuccess}，状态 {Status}", shutdownResult.IsSuccess, shutdownResult.Status ?? "无");
			return shutdownResult;
		default:
			return new OperationResult(IsSuccess: false, "不支持的结束后操作 " + afterDone);
		}
	}
}

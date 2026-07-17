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

	private static readonly TimeSpan CloseGameRetryDelay = TimeSpan.FromSeconds(3L);

	private static readonly TimeSpan AfterCloseGameDelay = TimeSpan.FromSeconds(10L);

	private readonly int _requestedInstanceIndex;

	private readonly string _groupId;

	/// <summary>
	/// 初始化一条龙应用。
	/// </summary>
	public ZOneDragonApp(ZContext context, int instanceIndex, string groupId = "one_dragon", ZApplicationRunRecord? runRecord = null)
		: base(context, "one_dragon", runRecord, "一条龙")
	{
		_requestedInstanceIndex = instanceIndex;
		_groupId = (string.IsNullOrWhiteSpace(groupId) ? "one_dragon" : groupId);
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
		int startIndex;
		OneDragonInstanceConfigItem[] instances = ResolveInstances(out startIndex);
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
				OperationResult closeResult = await CloseGameAndWaitAsync(lastController, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!closeResult.IsSuccess)
				{
					return ZApplication.Fail(closeResult.Status, new ZOneDragonRunSummary(currentInstanceIndex, _groupId, results));
				}
				base.Context.SwitchInstance(nextInstanceIndex);
				currentIndex = nextIndex;
				if (currentIndex == startIndex)
				{
					return ZApplication.Success("全部结束", new ZOneDragonRunSummary(nextInstanceIndex, _groupId, results));
				}
				OperationResult enterResult = await new OpenAndEnterGame(base.Context).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
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
				OperationResult switchResult = await new SwitchAccount(base.Context).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
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
		return ZApplication.Success("全部结束", new ZOneDragonRunSummary(nextInstanceIndex, _groupId, results));
	}

	private OneDragonInstanceConfigItem[] ResolveInstances(out int startIndex)
	{
		OneDragonEnvironment environment = base.Context.Environment;
		IReadOnlyList<string> subDirectories = Array.Empty<string>();
		YamlConfig<OneDragonConfig> yamlConfig = new YamlConfig<OneDragonConfig>(environment, "one_dragon", null, null, subDirectories);
		OneDragonInstanceConfigItem current = yamlConfig.Current.InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Active);
		if (current == null)
		{
			throw new InvalidOperationException("未找到当前启用的实例。");
		}
		IReadOnlyList<OneDragonInstanceConfigItem> readOnlyList = (string.Equals(yamlConfig.Current.InstanceRun, "全部实例", StringComparison.Ordinal) ? ((IReadOnlyList<OneDragonInstanceConfigItem>)yamlConfig.Current.InstanceList.Where((OneDragonInstanceConfigItem item) => item.ActiveInOneDragon).ToArray()) : ((IReadOnlyList<OneDragonInstanceConfigItem>)new OneDragonInstanceConfigItem[1] { current }));
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
		OneDragonEnvironment environment = base.Context.Environment;
		IReadOnlyList<string> subDirectories = Array.Empty<string>();
		YamlConfig<OneDragonConfig> yamlConfig = new YamlConfig<OneDragonConfig>(environment, "one_dragon", null, null, subDirectories);
		OneDragonInstanceConfigItem oneDragonInstanceConfigItem = yamlConfig.Current.InstanceList.FirstOrDefault((OneDragonInstanceConfigItem item) => item.Active);
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
			if (!base.Context.RunContext.IsAppRegistered(item.AppId))
			{
				throw new InvalidOperationException("未找到应用 " + item.AppId);
			}
			IApplication application = base.Context.RunContext.GetApplication(item.AppId, instanceIndex, _groupId);
			string appName = base.Context.RunContext.GetApplicationName(item.AppId);
			if (!item.Enabled)
			{
				results.Add(new ZOneDragonApplicationResult(instanceIndex, item.AppId, IsSuccess: true, "应用未启用 " + appName));
				continue;
			}
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

	private static async Task<OperationResult> CloseGameAndWaitAsync(ControllerBase? controller, CancellationToken cancellationToken)
	{
		if (controller == null)
		{
			return new OperationResult(IsSuccess: false, "未初始化游戏控制器。");
		}
		int retryCount = 0;
		while (controller.IsGameWindowReady && retryCount <= 3)
		{
			controller.CloseGame();
			await Task.Delay(CloseGameRetryDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			retryCount++;
		}
		if (controller.IsGameWindowReady)
		{
			return new OperationResult(IsSuccess: false, "检查是否关闭成功");
		}
		await Task.Delay(AfterCloseGameDelay, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new OperationResult(IsSuccess: true);
	}
}

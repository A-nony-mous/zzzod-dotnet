using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.SuibianTemple;

/// <summary>
/// 随便观应用入口节点图。
/// </summary>
public sealed class SuibianTempleOperation : ZOperation
{
	/// <summary>不在随便观入口。</summary>
	public const string StatusNotInTemple = "不在随便观";

	/// <summary>已在随便观入口。</summary>
	public const string StatusInTemple = "随便观-入口";

	/// <summary>未开启自动托管。</summary>
	public const string StatusAutoManageDisabled = "未开启自动托管";

	/// <summary>功能未开启。</summary>
	public const string StatusDisabled = "未开启";

	private readonly SuibianTempleConfig _config;

	private readonly ISuibianTempleOperationServices _services;

	/// <summary>
	/// 初始化随便观节点图。
	/// </summary>
	public SuibianTempleOperation(ZContext context, SuibianTempleConfig config, ISuibianTempleOperationServices? services = null)
		: base(context, "随便观")
	{
		_config = config;
		_services = services ?? new DefaultSuibianTempleOperationServices();
	}

	/// <summary>识别初始画面。</summary>
	[OperationNode("识别初始画面", IsStartNode = true)]
	public OperationRoundResult CheckInitialScreen()
	{
		return _services.IsInTempleEntry(base.ZContext, base.LastScreenshot) ? RoundSuccess("随便观-入口") : RoundSuccess("不在随便观");
	}

	/// <summary>传送。</summary>
	[NodeFrom("识别初始画面", Status = "不在随便观")]
	[OperationNode("传送")]
	public Task<OperationRoundResult> Transport()
	{
		return _services.TransportAsync(base.ZContext).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>前往随便观。</summary>
	[NodeFrom("传送")]
	[OperationNode("前往随便观", TimeoutSeconds = 60.0, NodeMaxRetryTimes = 999)]
	public OperationRoundResult GoToSuibianTemple()
	{
		OperationResult operationResult = _services.GoToTempleEntry(base.ZContext, base.LastScreenshot, _config);
		if (operationResult.IsSuccess)
		{
			return (operationResult.Status == "随便观-入口") ? RoundSuccess(operationResult.Status) : RoundWait(operationResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundRetry(operationResult.Status ?? "未识别当前画面", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>处理自动托管。</summary>
	[NodeFrom("识别初始画面", Status = "随便观-入口")]
	[NodeFrom("前往随便观")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理自动托管")]
	public Task<OperationRoundResult> HandleAutoManage()
	{
		if (!_config.AutoManageEnabled)
		{
			return Task.FromResult(RoundSuccess("未开启自动托管"));
		}
		return _services.HandleAutoManageAsync(base.ZContext, _config).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理游历。</summary>
	[NodeFrom("处理自动托管", Status = "未开启自动托管")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理游历")]
	public Task<OperationRoundResult> HandleAdventureSquad()
	{
		return _services.HandleAdventureSquadAsync(base.ZContext, _config, claim: true, !_config.YumChaSin).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理饮茶仙。</summary>
	[NodeFrom("处理游历")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理饮茶仙")]
	public Task<OperationRoundResult> HandleYumChaSin()
	{
		if (!_config.YumChaSin)
		{
			return Task.FromResult(RoundSuccess("未开启"));
		}
		return _services.HandleYumChaSinAsync(base.ZContext, _config).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>饮茶仙后处理游历。</summary>
	[NodeFrom("处理饮茶仙")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("饮茶仙后处理游历")]
	public Task<OperationRoundResult> HandleAdventureSquadAfterYumChaSin()
	{
		return _services.HandleAdventureSquadAsync(base.ZContext, _config, claim: false, dispatch: true).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理制造坊。</summary>
	[NodeFrom("处理饮茶仙", Status = "未开启")]
	[NodeFrom("饮茶仙后处理游历")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理制造坊")]
	public Task<OperationRoundResult> HandleCraft()
	{
		return _services.HandleCraftAsync(base.ZContext, _config).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理售卖铺。</summary>
	[NodeFrom("处理制造坊")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理售卖铺")]
	public Task<OperationRoundResult> HandleSalesStall()
	{
		return _services.HandleSalesStallAsync(base.ZContext, _config).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理好物铺。</summary>
	[NodeFrom("处理售卖铺")]
	[NodeFrom("处理自动托管")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理好物铺")]
	public Task<OperationRoundResult> HandleGoodGoods()
	{
		if (!_config.GoodGoodsPurchaseEnabled)
		{
			return Task.FromResult(RoundSuccess("未开启"));
		}
		return _services.HandleGoodGoodsAsync(base.ZContext, _config).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理邦巢。</summary>
	[NodeFrom("处理好物铺")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理邦巢")]
	public Task<OperationRoundResult> HandleBooBox()
	{
		if (!_config.BooBoxPurchaseEnabled)
		{
			return Task.FromResult(RoundSuccess("未开启"));
		}
		return _services.HandleBooBoxAsync(base.ZContext, _config).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}

	/// <summary>处理德丰大押。</summary>
	[NodeFrom("处理邦巢")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, Detail = true)]
	[OperationNode("处理德丰大押")]
	public Task<OperationRoundResult> HandlePawnshop()
	{
		return Task.FromResult(RoundSuccess("未开启"));
	}

	/// <summary>完成后返回。</summary>
	[NodeFrom("处理德丰大押")]
	[OperationNode("完成后返回")]
	public Task<OperationRoundResult> BackAtLast()
	{
		return _services.BackToNormalWorldAsync(base.ZContext).ContinueWith((Task<OperationResult> task) => RoundByOperationResult(task.Result), TaskScheduler.Default);
	}
}

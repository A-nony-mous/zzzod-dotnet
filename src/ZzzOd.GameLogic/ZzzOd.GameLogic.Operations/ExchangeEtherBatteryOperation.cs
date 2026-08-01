using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Operations;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 从快捷手册资源栏进入以太电池合成，并完成电池兑换。
/// </summary>
public sealed class ExchangeEtherBatteryOperation : ZOperation
{
	/// <summary>继续兑换。</summary>
	public const string StatusContinueExchange = "继续兑换";

	private readonly ChargePlanConfig _config;
	private readonly ChargePlanItem _plan;

	/// <summary>
	/// 初始化以太电池兑换操作。
	/// </summary>
	public ExchangeEtherBatteryOperation(ZContext context, ChargePlanItem plan, ChargePlanConfig? config = null)
		: base(context, "兑换以太电池")
	{
		_plan = plan;
		_config = config ?? ChargePlanConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), "one_dragon");
	}

	/// <summary>
	/// 点击快捷手册右上角以太电池图标。
	/// </summary>
	[OperationNode("点击以太电池", IsStartNode = true)]
	public OperationRoundResult ClickEtherBattery()
	{
		return RoundByFindAndClickArea(
			LastScreenshot,
			"快捷手册",
			"以太电池",
			null,
			TimeSpan.FromSeconds(1.0),
			TimeSpan.FromSeconds(0.5));
	}

	/// <summary>
	/// 点击获取/合成按钮。
	/// </summary>
	[NodeFrom("点击以太电池")]
	[OperationNode("点击合成入口")]
	public OperationRoundResult ClickSynthesizeEntry()
	{
		return RoundByOcrAndClick(
			LastScreenshot,
			"[获取]合成",
			null,
			0.5,
			null,
			TimeSpan.FromSeconds(1.0),
			TimeSpan.FromSeconds(0.5));
	}

	/// <summary>
	/// 等待进入道具处理界面。
	/// </summary>
	[NodeFrom("点击合成入口")]
	[OperationNode("等待道具处理", TimeoutSeconds = 20)]
	public OperationRoundResult WaitItemProcess()
	{
		string? currentScreen = CheckAndUpdateCurrentScreen(LastScreenshot, ["道具处理"]);
		if (string.IsNullOrEmpty(currentScreen))
		{
			return RoundRetry("等待道具处理", null, TimeSpan.FromSeconds(0.5));
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 检查可合成项目及素材是否充足。
	/// </summary>
	[NodeFrom("等待道具处理")]
	[NodeFrom("兑换完成", Status = StatusContinueExchange)]
	[OperationNode("检查合成素材")]
	public OperationRoundResult CheckMaterial()
	{
		var area = ZContext.ScreenContext.GetArea("道具处理", "详情标题");
		var ocrResult = RoundByOcr(LastScreenshot, "以太电池", area: area);
		if (!ocrResult.IsSuccess)
		{
			return RoundFail("当前可合成项目不是以太电池");
		}

		var result = RoundByFindArea(LastScreenshot, "道具处理", "合成素材不足");
		if (result.IsSuccess)
		{
			// 返回失败让体力计划app自动跳过计划
			return RoundFail("合成素材不足");
		}

		return RoundSuccess();
	}

	/// <summary>
	/// 点击合成按钮。
	/// </summary>
	[NodeFrom("检查合成素材")]
	[OperationNode("点击合成")]
	public OperationRoundResult ClickSynthesize()
	{
		return RoundByFindAndClickArea(
			LastScreenshot,
			"道具处理",
			"按钮-合成",
			null,
			TimeSpan.FromSeconds(1.0),
			TimeSpan.FromSeconds(0.5));
	}

	/// <summary>
	/// 确认合成二次弹窗。
	/// </summary>
	[NodeFrom("点击合成")]
	[OperationNode("确认合成")]
	public OperationRoundResult ConfirmSynthesize()
	{
		return RoundByFindAndClickArea(
			LastScreenshot,
			"道具处理-合成确认",
			"按钮-确认",
			null,
			TimeSpan.FromSeconds(1.0),
			TimeSpan.FromSeconds(0.5));
	}

	/// <summary>
	/// 确认获得奖励弹窗。
	/// </summary>
	[NodeFrom("确认合成")]
	[OperationNode("确认获得")]
	public OperationRoundResult ConfirmObtained()
	{
		var result = RoundByFindArea(LastScreenshot, "道具处理-获得", "标题-获得");
		if (result.IsSuccess)
		{
			return RoundByFindAndClickArea(
				LastScreenshot,
				"道具处理-获得",
				"按钮-确认",
				null,
				TimeSpan.FromSeconds(0.8),
				TimeSpan.FromSeconds(0.5));
		}

		result = RoundByFindArea(LastScreenshot, "道具处理", "标题-道具处理");
		if (result.IsSuccess)
		{
			return RoundSuccess();
		}

		return RoundRetry("等待获得弹窗", null, TimeSpan.FromSeconds(0.5));
	}

	/// <summary>
	/// 兑换完成，更新计划运行次数。
	/// </summary>
	[NodeFrom("确认获得")]
	[NodeFrom("确认获得", Success = false)]
	[OperationNode("兑换完成")]
	public OperationRoundResult FinishExchange()
	{
		_config.AddPlanRunTimes(_plan);

		if (_plan.RunTimes < _plan.PlanTimes)
		{
			return RoundSuccess(StatusContinueExchange, TimeSpan.FromSeconds(0.5));
		}

		return RoundSuccess();
	}
}

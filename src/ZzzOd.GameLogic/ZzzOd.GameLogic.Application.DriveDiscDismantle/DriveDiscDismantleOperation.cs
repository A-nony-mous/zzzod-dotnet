using System;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.DriveDiscDismantle;

/// <summary>
/// 驱动盘拆解流程。
/// </summary>
public sealed class DriveDiscDismantleOperation : ZOperation
{
	private const string SalvageScreenName = "仓库-驱动仓库-驱动盘拆解";

	private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1L);

	private readonly DriveDiscDismantleConfig _config;

	private readonly IDriveDiscDismantleOperationServices _services;

	/// <summary>
	/// 初始化驱动盘拆解流程。
	/// </summary>
	public DriveDiscDismantleOperation(ZContext context, DriveDiscDismantleConfig config, IDriveDiscDismantleOperationServices? services = null)
		: base(context, "驱动盘拆解")
	{
		_config = config;
		_services = services ?? new DefaultDriveDiscDismantleOperationServices();
	}

	/// <summary>
	/// 开始前返回。
	/// </summary>
	[OperationNode("开始前返回", IsStartNode = true)]
	public async Task<OperationRoundResult> BackAtFirst()
	{
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 前往分解画面。
	/// </summary>
	[NodeFrom("开始前返回")]
	[OperationNode("前往分解画面")]
	public OperationRoundResult GotoSalvage()
	{
		Mat? screen = base.LastScreenshot ?? Screenshot();
		TimeSpan? retryDelay = OneSecond;
		return RoundByGotoScreen(screen, "仓库-驱动仓库-驱动盘拆解", null, null, retryDelay);
	}

	/// <summary>
	/// 快速选择。
	/// </summary>
	[NodeFrom("前往分解画面")]
	[OperationNode("快速选择")]
	public OperationRoundResult ClickFilter()
	{
		return ClickSalvageArea("按钮-快速选择");
	}

	/// <summary>
	/// 选择等级。
	/// </summary>
	[NodeFrom("快速选择")]
	[OperationNode("选择等级")]
	public OperationRoundResult ChooseLevel()
	{
		return ClickSalvageArea("按钮-" + _config.DismantleLevel);
	}

	/// <summary>
	/// 选择弃置。
	/// </summary>
	[NodeFrom("选择等级")]
	[OperationNode("选择弃置")]
	public OperationRoundResult ChooseAbandon()
	{
		if (!_config.DismantleAbandon)
		{
			return RoundSuccess("无需选择");
		}
		return ClickSalvageArea("按钮-全选已弃置");
	}

	/// <summary>
	/// 快速选择确认。
	/// </summary>
	[NodeFrom("选择等级", Success = false)]
	[NodeFrom("选择弃置")]
	[NodeFrom("选择弃置", Success = false)]
	[OperationNode("快速选择确认")]
	public OperationRoundResult ClickFilterConfirm()
	{
		return ClickSalvageArea("按钮-快速选择-确认");
	}

	/// <summary>
	/// 点击拆解。
	/// </summary>
	[NodeFrom("快速选择确认")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentSuccess)]
	[OperationNode("点击拆解")]
	public OperationRoundResult ClickSalvage()
	{
		return ClickSalvageArea("按钮-拆解");
	}

	/// <summary>
	/// 点击拆解确认。
	/// </summary>
	[NodeFrom("点击拆解")]
	[OperationNode("点击拆解确认")]
	public OperationRoundResult ClickSalvageConfirm()
	{
		return ClickSalvageArea("按钮-拆解-确认");
	}

	/// <summary>
	/// 完成后返回。
	/// </summary>
	[NodeFrom("点击拆解确认")]
	[NodeFrom("点击拆解确认", Success = false)]
	[OperationNode("完成后返回")]
	public async Task<OperationRoundResult> BackAtLast()
	{
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	private OperationRoundResult ClickSalvageArea(string areaName)
	{
		Mat? screen = base.LastScreenshot ?? Screenshot();
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = OneSecond;
		return RoundByFindAndClickArea(screen, "仓库-驱动仓库-驱动盘拆解", areaName, null, successDelay, retryDelay);
	}
}

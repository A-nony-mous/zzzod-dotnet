using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 处理菜单态和副本内的恢复电量弹窗。
/// </summary>
public sealed class RestoreCharge : ZOperation
{
	/// <summary>储蓄电量来源。</summary>
	public const string SourceBackupCharge = "储蓄电量";

	/// <summary>以太电池来源。</summary>
	public const string SourceEtherBattery = "以太电池";

	/// <summary>电量不足状态。</summary>
	public const string StatusChargeNotEnough = "电量不足";

	/// <summary>恢复成功状态。</summary>
	public const string StatusRestoreSuccess = "恢复电量成功";

	private readonly ChargePlanConfig _config;

	private readonly int? _requiredCharge;

	private readonly bool _isMenu;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private bool _skipBackupCharge;

	/// <summary>
	/// 战斗后重试入口。
	/// </summary>
	public bool IsAfterBattleRetry { get; set; }

	/// <summary>
	/// 初始化恢复电量操作。
	/// </summary>
	public RestoreCharge(ZContext context, int? requiredCharge = null, bool isMenu = false, ChargePlanConfig? config = null, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null)
		: base(context, "恢复电量")
	{
		_requiredCharge = requiredCharge;
		_isMenu = isMenu;
		_config = config ?? ChargePlanConfig.Load(context.Environment, context.RunContext.CurrentInstanceIndex.GetValueOrDefault(), context.RunContext.CurrentGroupId ?? "one_dragon");
		_retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(500L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_skipBackupCharge = false;
		return Task.CompletedTask;
	}

	[NodeFrom("关闭快捷使用", Status = "重新选择电量来源")]
	[OperationNode("打开恢复界面", IsStartNode = true)]
	private OperationRoundResult ClickChargeText()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恢复电量", "标题-恢复电量");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess();
		}
		if (_isMenu)
		{
			return RoundByFindAndClickArea(base.LastScreenshot, "菜单", "文本-电量", _preClickDelay, _retryDelay, _retryDelay);
		}
		if (IsAfterBattleRetry)
		{
			return RoundByFindAndClickArea(base.LastScreenshot, "战斗画面", "战斗结果-再来一次", _preClickDelay, _retryDelay, _retryDelay);
		}
		return RoundByFindAndClickArea(base.LastScreenshot, "实战模拟室", "下一步", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("打开恢复界面")]
	[OperationNode("选择电量来源")]
	private OperationRoundResult SelectChargeSource()
	{
		IReadOnlyList<string> chargeSourceList = GetChargeSourceList();
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("恢复电量", "类型");
		Mat? lastScreenshot = base.LastScreenshot;
		OneDragon.Core.Abstractions.Geometry.Point? offset = new OneDragon.Core.Abstractions.Geometry.Point(0, -100);
		TimeSpan? retryDelay = _retryDelay;
		return RoundByOcrAndClickByPriority(lastScreenshot, chargeSourceList, area, 0.5, offset, null, retryDelay);
	}

	[NodeFrom("选择电量来源")]
	[OperationNode("确认电量来源")]
	private OperationRoundResult ConfirmChargeSource()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "恢复电量", "确认", _preClickDelay, _retryDelay, _retryDelay);
		return operationRoundResult.IsSuccess ? RoundSuccess(base.PreviousNode.Status, null, _retryDelay) : RoundRetry("未找到确认按钮", null, _retryDelay);
	}

	[NodeFrom("确认电量来源")]
	[OperationNode("识别当前数量")]
	private OperationRoundResult SetChargeAmount()
	{
		string status = base.PreviousNode.Status;
		if (status == null)
		{
			return RoundRetry("未识别到电量来源", null, _retryDelay);
		}
		if (ShouldProbeSourceInMenu())
		{
			OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恢复电量", "标题-快捷使用");
			if (!operationRoundResult.IsSuccess)
			{
				return RoundRetry("未识别到快捷使用", null, _retryDelay);
			}
		}
		int? amountByArea = GetAmountByArea("当前数量");
		if (!amountByArea.HasValue)
		{
			return RoundRetry("未识别到当前数量", null, _retryDelay);
		}
		if (ShouldProbeSourceInMenu())
		{
			if (IsSourceChargeEnough(status, amountByArea.Value))
			{
				return RoundSuccess("继续前往副本", amountByArea.Value, _retryDelay);
			}
			if (ShouldReselectSource(status))
			{
				_skipBackupCharge = true;
				return RoundSuccess("重新选择电量来源", amountByArea.Value, _retryDelay);
			}
			return RoundSuccess("电量不足", amountByArea.Value, _retryDelay);
		}
		int? amountByArea2 = GetAmountByArea("兑换数量-数字输入框");
		if (!amountByArea2.HasValue)
		{
			return RoundRetry("未识别到兑换数量", null, _retryDelay);
		}
		if (amountByArea2.Value > amountByArea.Value)
		{
			return RoundRetry("兑换数量大于当前数量", null, _retryDelay);
		}
		if (!ShouldConfirmRestore(amountByArea.Value, amountByArea2.Value))
		{
			if (ShouldReselectSource(status))
			{
				_skipBackupCharge = true;
				return RoundSuccess("重新选择电量来源", amountByArea2.Value, _retryDelay);
			}
			return RoundSuccess("电量不足", amountByArea2.Value, _retryDelay);
		}
		return RoundSuccess(status, amountByArea2.Value, _retryDelay);
	}

	[NodeFrom("识别当前数量", Status = "继续前往副本")]
	[NodeFrom("识别当前数量", Status = "重新选择电量来源")]
	[NodeFrom("识别当前数量", Status = "电量不足")]
	[OperationNode("关闭快捷使用")]
	private OperationRoundResult CloseQuickUsePopup()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恢复电量", "标题-快捷使用");
		if (!operationRoundResult.IsSuccess)
		{
			return RoundSuccess(base.PreviousNode.Status, base.PreviousNode.Data, _retryDelay);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(base.LastScreenshot, "菜单", "关闭", _preClickDelay, _retryDelay, _retryDelay);
		return operationRoundResult2.IsSuccess ? RoundRetry("尝试关闭快捷使用", null, _retryDelay) : RoundRetry("未关闭快捷使用", null, _retryDelay);
	}

	[NodeFrom("识别当前数量", Status = "储蓄电量")]
	[NodeFrom("识别当前数量", Status = "以太电池")]
	[OperationNode("确认恢复电量")]
	private OperationRoundResult ConfirmRestoreCharge()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "恢复电量", "确认", _preClickDelay, TimeSpan.FromSeconds(1L), _retryDelay);
	}

	[NodeFrom("确认恢复电量")]
	[OperationNode("恢复后处理")]
	private OperationRoundResult ConfirmAfterRestore()
	{
		string[] array = new string[2] { "标题-获得", "标题-快捷使用" };
		foreach (string areaName in array)
		{
			OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "恢复电量", areaName);
			if (operationRoundResult.IsSuccess)
			{
				OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(base.LastScreenshot, "恢复电量", "确认", _preClickDelay, _retryDelay, _retryDelay);
				return operationRoundResult2.IsSuccess ? RoundWait("等待恢复完成", null, _retryDelay) : RoundRetry("恢复电量失败", null, _retryDelay);
			}
		}
		return RoundSuccess("恢复电量成功", null, _retryDelay);
	}

	private IReadOnlyList<string> GetChargeSourceList()
	{
		RestoreChargeMode restoreChargeMode = RestoreChargeMode.FromDisplayName(_config.RestoreCharge);
		if (restoreChargeMode == RestoreChargeMode.BackupOnly)
		{
			return new string[] { "储蓄电量" };
		}
		if (restoreChargeMode == RestoreChargeMode.EtherOnly)
		{
			return new string[] { "以太电池" };
		}
		if (restoreChargeMode == RestoreChargeMode.Both)
		{
			IReadOnlyList<string> result;
			if (!_skipBackupCharge)
			{
				IReadOnlyList<string> readOnlyList = new string[2] { "储蓄电量", "以太电池" };
				result = readOnlyList;
			}
			else
			{
				IReadOnlyList<string> readOnlyList = new string[] { "以太电池" };
				result = readOnlyList;
			}
			return result;
		}
		return Array.Empty<string>();
	}

	private bool ShouldProbeSourceInMenu()
	{
		int result;
		if (_isMenu)
		{
			int? requiredCharge = _requiredCharge;
			result = (requiredCharge.HasValue ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static bool ShouldConfirmRestore(int currentAmount, int exchangeAmount)
	{
		return exchangeAmount <= currentAmount && exchangeAmount < currentAmount;
	}

	private bool IsSourceChargeEnough(string source, int currentAmount)
	{
		int? requiredCharge = _requiredCharge;
		if (!requiredCharge.HasValue)
		{
			return true;
		}
		if (1 == 0)
		{
		}
		bool result = ((source == "储蓄电量") ? (currentAmount >= _requiredCharge.Value) : (!(source == "以太电池") || currentAmount * 60 >= _requiredCharge.Value));
		if (1 == 0)
		{
		}
		return result;
	}

	private bool ShouldReselectSource(string source)
	{
		return string.Equals(source, "储蓄电量", StringComparison.Ordinal) && RestoreChargeMode.FromDisplayName(_config.RestoreCharge) == RestoreChargeMode.Both;
	}

	private int? GetAmountByArea(string areaName)
	{
		if (base.LastScreenshot == null)
		{
			return null;
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("恢复电量", areaName);
		if (area == null)
		{
			return null;
		}
		using Mat image = CvImageUtils.Crop(base.LastScreenshot, area.Rect);
		string value = base.ZContext.OcrService.Matcher.RunOcrSingleLine(image);
		return StringUtils.GetPositiveDigits(value);
	}
}

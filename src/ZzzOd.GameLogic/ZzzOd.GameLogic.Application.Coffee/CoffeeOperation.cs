using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.WorldPatrol.Operations;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;
using ZzzOd.GameLogic.Operations.Turning;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡店流程。
/// </summary>
public sealed class CoffeeOperation : ZOperation
{
	/// <summary>不占用上限的咖啡。</summary>
	public const string StatusExtraCoffee = "不占用上限的咖啡";

	/// <summary>没有增益的咖啡。</summary>
	public const string StatusWithoutBenefit = "没有增益的咖啡";

	private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1L);

	private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(500L);

	private static readonly Scalar CoffeeTextMaskLower = new Scalar(220.0, 220.0, 220.0);

	private static readonly Scalar CoffeeTextMaskUpper = new Scalar(255.0, 255.0, 255.0);

	private readonly CoffeeConfig _config;

	private readonly ChargePlanConfig _chargePlanConfig;

	private readonly CoffeeSelectionService _selectionService;

	private readonly Func<DateTimeOffset> _now;

	private readonly Func<ZContext, CoffeeConfig, Task<OperationResult>> _transportAsync;

	private readonly Func<ZContext, Task<OperationResult>> _waitNormalWorldAsync;

	private readonly Func<ZContext, Task<OperationResult>> _backToNormalWorldAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _combatSimulationAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _areaPatrolAsync;

	private readonly Func<ZContext, ChargePlanItem, Task<OperationResult>> _expertChallengeAsync;

	private readonly Func<ZContext, CoffeeConfig, Task<OperationResult>> _chargePlanAfterwardsAsync;

	private readonly Func<ZContext, CoffeeConfig, Mat?, OperationRoundResult> _moveAndInteract;

	private readonly AngleTurnCompensator? _turnCompensator;

	private readonly HashSet<string> _hadCoffeeList = new HashSet<string>(StringComparer.Ordinal);

	private bool _retriedTransport;

	private ZzzOd.GameLogic.GameData.Coffee? _chosenCoffee;

	private ChargePlanItem? _chargePlan;

	/// <summary>
	/// 初始化咖啡店流程。
	/// </summary>
	public CoffeeOperation(ZContext context, CoffeeConfig config, ChargePlanConfig chargePlanConfig, CoffeeSelectionService? selectionService = null, Func<DateTimeOffset>? now = null, Func<ZContext, CoffeeConfig, Task<OperationResult>>? transportAsync = null, Func<ZContext, Task<OperationResult>>? waitNormalWorldAsync = null, Func<ZContext, Task<OperationResult>>? backToNormalWorldAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? combatSimulationAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? areaPatrolAsync = null, Func<ZContext, ChargePlanItem, Task<OperationResult>>? expertChallengeAsync = null, Func<ZContext, CoffeeConfig, Task<OperationResult>>? chargePlanAfterwardsAsync = null, Func<ZContext, CoffeeConfig, Mat?, OperationRoundResult>? moveAndInteract = null, AngleTurnCompensator? turnCompensator = null)
		: base(context, "咖啡店")
	{
		_config = config;
		_chargePlanConfig = chargePlanConfig;
		_selectionService = selectionService ?? new CoffeeSelectionService();
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
		_transportAsync = transportAsync ?? new Func<ZContext, CoffeeConfig, Task<OperationResult>>(DefaultTransportAsync);
		_waitNormalWorldAsync = waitNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultWaitNormalWorldAsync);
		_backToNormalWorldAsync = backToNormalWorldAsync ?? new Func<ZContext, Task<OperationResult>>(DefaultBackToNormalWorldAsync);
		_combatSimulationAsync = combatSimulationAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new CombatSimulation(ctx, plan, _chargePlanConfig).ExecuteAsync()));
		_areaPatrolAsync = areaPatrolAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new AreaPatrol(ctx, plan, _chargePlanConfig).ExecuteAsync()));
		_expertChallengeAsync = expertChallengeAsync ?? ((Func<ZContext, ChargePlanItem, Task<OperationResult>>)((ZContext ctx, ChargePlanItem plan) => new ExpertChallenge(ctx, plan, _chargePlanConfig).ExecuteAsync()));
		_chargePlanAfterwardsAsync = chargePlanAfterwardsAsync ?? new Func<ZContext, CoffeeConfig, Task<OperationResult>>(DefaultChargePlanAfterwardsAsync);
		_moveAndInteract = moveAndInteract ?? new Func<ZContext, CoffeeConfig, Mat, OperationRoundResult>(DefaultMoveAndInteract);
		_turnCompensator = turnCompensator ?? ((context.Controller is ZPcController controller) ? new AngleTurnCompensator(controller) : null);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_turnCompensator?.Reset();
		_retriedTransport = false;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 传送到咖啡店。
	/// </summary>
	[NodeFrom("等待咖啡店加载", Success = false)]
	[OperationNode("传送", IsStartNode = true)]
	public async Task<OperationRoundResult> Transport()
	{
		if (string.Equals(base.PreviousNode.Name, "等待咖啡店加载", StringComparison.Ordinal))
		{
			if (_retriedTransport)
			{
				return RoundFail("等待咖啡店加载失败，重传送超限");
			}
			_retriedTransport = true;
		}
		_turnCompensator?.ClearPendingSample();
		return RoundByOperationResult(await _transportAsync(base.ZContext, _config).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 等待大世界或点单入口加载。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("等待大世界加载", NodeMaxRetryTimes = 60)]
	public async Task<OperationRoundResult> WaitWorld()
	{
		OperationRoundResult order = RoundByFindArea(base.LastScreenshot, "咖啡店", "点单");
		if (order.IsSuccess)
		{
			return RoundSuccess(order.Status);
		}
		OperationResult result = await _waitNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false);
		return result.IsSuccess ? RoundSuccess(result.Status) : RoundRetry(result.Status, null, RetryDelay);
	}

	/// <summary>
	/// 移动到店员前并交互。
	/// </summary>
	[NodeFrom("等待大世界加载")]
	[OperationNode("移动交互", NodeMaxRetryTimes = 10)]
	public OperationRoundResult MoveAndInteract()
	{
		OperationRoundResult operationRoundResult = _moveAndInteract(base.ZContext, _config, base.LastScreenshot);
		if (!operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		return string.Equals(_config.TransportPoint, CoffeeTransportPoint.FailumeHeights.Value, StringComparison.Ordinal) ? RoundSuccess("对话点单") : operationRoundResult;
	}

	/// <summary>
	/// 等待咖啡店点单界面加载。
	/// </summary>
	[NodeFrom("移动交互")]
	[OperationNode("等待咖啡店加载", NodeMaxRetryTimes = 10)]
	public OperationRoundResult WaitCoffeeShop()
	{
		return RoundByFindArea(base.LastScreenshot, "咖啡店", "点单", RetryDelay, RetryDelay);
	}

	/// <summary>
	/// 在咖啡列表中选择目标咖啡。
	/// </summary>
	[NodeFrom("等待大世界加载", Status = "点单")]
	[NodeFrom("等待咖啡店加载")]
	[NodeFrom("电量确认", Status = "不占用上限的咖啡")]
	[OperationNode("选择咖啡")]
	public OperationRoundResult ChooseCoffee()
	{
		int currentDayOfWeek = GetCurrentDayOfWeek();
		if (string.Equals(_config.ChooseWay, "优先体力计划", StringComparison.Ordinal))
		{
			_chargePlanConfig.ResetPlans();
		}
		IReadOnlyList<string> coffeeToChoose = _selectionService.GetCoffeeToChoose(_config, _chargePlanConfig, base.ZContext.CompendiumService, currentDayOfWeek, _hadCoffeeList);
		OperationRoundResult operationRoundResult = ClickCoffeeByOcr(base.LastScreenshot, coffeeToChoose);
		if (operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		if (currentDayOfWeek == 7 && base.ZContext.Controller != null)
		{
			OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("咖啡店", "咖啡列表");
			if (area != null)
			{
				base.ZContext.Controller.DragTo(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(-200, 0), area.Center);
			}
		}
		return RoundRetry("没找到目标咖啡", null, RetryDelay);
	}

	/// <summary>
	/// 点击点单。
	/// </summary>
	[NodeFrom("选择咖啡")]
	[OperationNode("点单")]
	public OperationRoundResult OrderCoffee()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "咖啡店", "点单");
		if (!operationRoundResult.IsSuccess || _chosenCoffee == null)
		{
			return RoundRetry(operationRoundResult.Status, null, RetryDelay);
		}
		_hadCoffeeList.Add(_chosenCoffee.CoffeeName);
		if (_chosenCoffee.Extra)
		{
			return RoundSuccess("不占用上限的咖啡", null, ShortDelay);
		}
		return _chosenCoffee.WithoutBenefit ? RoundSuccess("没有增益的咖啡", null, ShortDelay) : RoundSuccess(null, null, TimeSpan.FromSeconds(3L));
	}

	/// <summary>
	/// 确认额外咖啡。
	/// </summary>
	[NodeFrom("点单", Status = "不占用上限的咖啡")]
	[OperationNode("不占用点单确认")]
	public OperationRoundResult ExtraOrderConfirm()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = RetryDelay;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "咖啡店", "对话框确认", null, successDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status, null, RetryDelay);
		}
		Mat? lastScreenshot2 = base.LastScreenshot;
		successDelay = RetryDelay;
		OperationRoundResult operationRoundResult2 = RoundByFindAndClickArea(lastScreenshot2, "咖啡店", "不可贪杯确认", null, successDelay);
		return operationRoundResult2.IsSuccess ? RoundSuccess(operationRoundResult2.Status, null, RetryDelay) : RoundRetry(null, null, RetryDelay);
	}

	/// <summary>
	/// 点单后跳过动画。
	/// </summary>
	[NodeFrom("点单")]
	[NodeFrom("不占用点单确认")]
	[OperationNode("点单后跳过")]
	public OperationRoundResult SkipAfterOrder()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "咖啡店", "电量确认");
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		OperationRoundResult operationRoundResult2 = RoundByFindArea(base.LastScreenshot, "咖啡店", "点单后跳过");
		if (operationRoundResult2.IsSuccess)
		{
			OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("咖啡店", "点单后跳过");
			if (area == null)
			{
				return RoundFail("区域未配置 点单后跳过");
			}
			if (base.ZContext.Controller == null)
			{
				return RoundRetry("控制器不可用", null, RetryDelay);
			}
			base.ZContext.Controller.DragTo(area.Center, area.LeftTop, TimeSpan.FromMilliseconds(200L));
			if (!base.ZContext.Controller.Click())
			{
				return RoundRetry("点击失败 点单后跳过", null, RetryDelay);
			}
			return RoundSuccess(operationRoundResult2.Status, null, RetryDelay);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = RetryDelay;
		OperationRoundResult operationRoundResult3 = RoundByFindAndClickArea(lastScreenshot, "咖啡店", "不可贪杯确认", null, successDelay);
		return operationRoundResult3.IsSuccess ? RoundSuccess(operationRoundResult3.Status, null, RetryDelay) : RoundRetry(operationRoundResult3.Status, null, RetryDelay);
	}

	/// <summary>
	/// 处理澄辉坪店员对话点单。
	/// </summary>
	[NodeFrom("移动交互", Status = "对话点单")]
	[OperationNode("对话选咖啡", NodeMaxRetryTimes = 20)]
	public OperationRoundResult DialogChooseCoffee()
	{
		OperationRoundResult operationRoundResult = RoundByFindArea(base.LastScreenshot, "咖啡店", "对话框标题-汀曼大师");
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry("等待对话框加载", null, ShortDelay);
		}
		if (RoundByFindArea(base.LastScreenshot, "咖啡店", "对话框-明天再来").IsSuccess)
		{
			return RoundSuccess("已喝过", null, RetryDelay);
		}
		if (string.Equals(_config.ChooseWay, "优先体力计划", StringComparison.Ordinal))
		{
			_chargePlanConfig.ResetPlans();
		}
		IReadOnlyList<string> coffeeToChoose = _selectionService.GetCoffeeToChoose(_config, _chargePlanConfig, base.ZContext.CompendiumService, GetCurrentDayOfWeek(), _hadCoffeeList);
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("咖啡店", "右侧选项区域");
		string[] targetTextList = coffeeToChoose.Select(base.ZContext.GameTextResolver).ToArray();
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = RetryDelay;
		TimeSpan? retryDelay = RetryDelay;
		OperationRoundResult result = RoundByOcrAndClickByPriority(lastScreenshot, targetTextList, area, 0.5, null, successDelay, retryDelay);
		string text = (result.IsSuccess ? coffeeToChoose.FirstOrDefault((string coffeeName) => string.Equals(base.ZContext.GameTextResolver(coffeeName), result.Status, StringComparison.Ordinal)) : null);
		if (text != null && base.ZContext.CompendiumService.NameToCoffee.TryGetValue(text, out ZzzOd.GameLogic.GameData.Coffee value))
		{
			_chosenCoffee = value;
			_hadCoffeeList.Add(text);
			return RoundSuccess("已点单", null, RetryDelay);
		}
		return RoundRetry("等待对话框", null, RetryDelay);
	}

	/// <summary>
	/// 点击电量确认。
	/// </summary>
	[NodeFrom("点单后跳过")]
	[NodeFrom("对话选咖啡", Status = "已点单")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentSuccess)]
	[OperationNode("电量确认")]
	public OperationRoundResult ChargeConfirm()
	{
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(base.LastScreenshot, "咖啡店", "电量确认");
		if (operationRoundResult.IsSuccess && _chosenCoffee != null)
		{
			return _chosenCoffee.Extra ? RoundSuccess("不占用上限的咖啡", null, RetryDelay) : RoundSuccess(null, null, RetryDelay);
		}
		return RoundRetry(null, null, RetryDelay);
	}

	/// <summary>
	/// 按配置选择前往挑战或确认结束。
	/// </summary>
	[NodeFrom("电量确认")]
	[OperationNode("选择前往")]
	public OperationRoundResult ChooseGo()
	{
		if (_chosenCoffee == null)
		{
			return RoundFail("未选择咖啡");
		}
		if (_chosenCoffee.WithoutBenefit)
		{
			return RoundSuccess("没有加成");
		}
		TimeSpan? successDelay;
		TimeSpan? retryDelay;
		if (string.Equals(_config.ChallengeWay, "不挑战", StringComparison.Ordinal))
		{
			Mat? lastScreenshot = base.LastScreenshot;
			successDelay = RetryDelay;
			retryDelay = RetryDelay;
			return RoundByFindAndClickArea(lastScreenshot, "咖啡店", "对话框确认", null, successDelay, retryDelay);
		}
		if (string.Equals(_config.ChallengeWay, "只挑战体力计划", StringComparison.Ordinal) && !_chargePlanConfig.PlanList.Any((ChargePlanItem plan) => CoffeeSelectionService.IsCoffeeForPlan(_chosenCoffee, plan)))
		{
			Mat? lastScreenshot2 = base.LastScreenshot;
			retryDelay = RetryDelay;
			successDelay = RetryDelay;
			return RoundByFindAndClickArea(lastScreenshot2, "咖啡店", "对话框确认", null, retryDelay, successDelay);
		}
		Mat? lastScreenshot3 = base.LastScreenshot;
		successDelay = RetryDelay;
		retryDelay = RetryDelay;
		return RoundByFindAndClickArea(lastScreenshot3, "咖啡店", "对话框前往", null, successDelay, retryDelay);
	}

	/// <summary>
	/// 传送到咖啡对应副本。
	/// </summary>
	[NodeFrom("选择前往", Status = "对话框前往")]
	[OperationNode("传送副本")]
	public OperationRoundResult TransportMission()
	{
		if (_chosenCoffee == null)
		{
			return RoundFail("未选择咖啡");
		}
		if (_chosenCoffee.WithoutBenefit)
		{
			return RoundFail("没有增益的咖啡");
		}
		ChargePlanItem matchedPlan = _chargePlanConfig.PlanList.FirstOrDefault((ChargePlanItem plan) => CoffeeSelectionService.IsCoffeeForPlan(_chosenCoffee, plan));
		_chargePlan = BuildChargePlan(_chosenCoffee, matchedPlan);
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("咖啡店", "对话框确认");
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = TimeSpan.FromSeconds(5L);
		TimeSpan? retryDelay = RetryDelay;
		OperationRoundResult operationRoundResult = RoundByOcrAndClick(lastScreenshot, "确认", area, 0.6, null, successDelay, retryDelay);
		return operationRoundResult.IsSuccess ? RoundSuccess(_chargePlan.CategoryName, null, TimeSpan.FromSeconds(5L)) : RoundRetry(operationRoundResult.Status, null, RetryDelay);
	}

	/// <summary>
	/// 执行实战模拟室。
	/// </summary>
	[NodeFrom("传送副本", Status = "实战模拟室")]
	[OperationNode("实战模拟室")]
	public async Task<OperationRoundResult> CombatSimulation()
	{
		if (_chargePlan == null)
		{
			return RoundFail("未生成挑战计划");
		}
		return RoundByOperationResult(await _combatSimulationAsync(base.ZContext, _chargePlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 执行区域巡防。
	/// </summary>
	[NodeFrom("传送副本", Status = "区域巡防")]
	[OperationNode("区域巡防")]
	public async Task<OperationRoundResult> AreaPatrol()
	{
		if (_chargePlan == null)
		{
			return RoundFail("未生成挑战计划");
		}
		return RoundByOperationResult(await _areaPatrolAsync(base.ZContext, _chargePlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 执行专业挑战室。
	/// </summary>
	[NodeFrom("传送副本", Status = "专业挑战室")]
	[OperationNode("专业挑战室")]
	public async Task<OperationRoundResult> ExpertChallenge()
	{
		if (_chargePlan == null)
		{
			return RoundFail("未生成挑战计划");
		}
		return RoundByOperationResult(await _expertChallengeAsync(base.ZContext, _chargePlan).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("不占用点单确认", Status = "不可贪杯确认")]
	[NodeFrom("点单后跳过", Status = "不可贪杯确认")]
	[NodeFrom("对话选咖啡", Status = "已喝过")]
	[NodeFrom("选择前往", Status = "对话框确认")]
	[NodeFrom("选择前往", Status = "没有加成")]
	[NodeFrom("实战模拟室")]
	[NodeFrom("区域巡防")]
	[NodeFrom("专业挑战室")]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _backToNormalWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 咖啡后按配置运行体力计划。
	/// </summary>
	[NodeFrom("返回大世界")]
	[OperationNode("结束后运行体力计划")]
	public async Task<OperationRoundResult> ChargePlanAfterwards()
	{
		if (!_config.RunChargePlanAfterwards)
		{
			return RoundSuccess("无需运行");
		}
		return RoundByOperationResult(await _chargePlanAfterwardsAsync(base.ZContext, _config).ConfigureAwait(continueOnCapturedContext: false));
	}

	internal OperationRoundResult ClickCoffeeByOcrForTesting(Mat? screen, IReadOnlyList<string> coffeeNames)
	{
		return ClickCoffeeByOcr(screen, coffeeNames);
	}

	internal static Mat CreateCoffeeListOcrImage(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		ArgumentNullException.ThrowIfNull(screen, "screen");
		ArgumentNullException.ThrowIfNull(area, "area");
		using Mat mat = CvImageUtils.Crop(screen, area.Rect);
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		Cv2.InRange(mat, CoffeeTextMaskLower, CoffeeTextMaskUpper, mat2);
		using Mat mat4 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
		Cv2.Dilate(mat2, mat3, mat4);
		Mat mat5 = new Mat();
		Cv2.BitwiseAnd(mat, mat, mat5, mat3);
		return mat5;
	}

	private OperationRoundResult ClickCoffeeByOcr(Mat? screen, IReadOnlyList<string> coffeeNames)
	{
		if (screen == null)
		{
			return RoundRetry("未获取截图", null, RetryDelay);
		}
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("点击失败", null, RetryDelay);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("咖啡店", "咖啡列表");
		if (area == null)
		{
			return RoundFail("区域未配置 咖啡列表");
		}
		IReadOnlyList<OcrMatchResult> coffeeListOcrResults = GetCoffeeListOcrResults(screen, area);
		string[] targetWords = coffeeListOcrResults.Select((OcrMatchResult result) => result.Text).ToArray();
		double[] array = new double[3] { 0.8, 0.6, 0.4 };
		foreach (double percent in array)
		{
			foreach (string item in coffeeNames.Where((string name) => !string.IsNullOrWhiteSpace(name)))
			{
				string text = base.ZContext.GameTextResolver(item);
				int? num2 = StringUtils.FindBestMatchByDifflib(text, targetWords);
				if (num2.HasValue)
				{
					OcrMatchResult ocrMatchResult = coffeeListOcrResults[num2.Value];
					if (StringUtils.FindByLcs(text, ocrMatchResult.Text, percent) && CoffeeStrengthWordsMatch(item, ocrMatchResult.Text) && base.ZContext.CompendiumService.NameToCoffee.TryGetValue(item, out ZzzOd.GameLogic.GameData.Coffee value))
					{
						_chosenCoffee = value;
						OneDragon.Core.Abstractions.Geometry.Point value2 = ocrMatchResult.Center + new OneDragon.Core.Abstractions.Geometry.Point(0, -50);
						Thread.Sleep(ShortDelay);
						ControllerBase? controller = base.ZContext.Controller;
						OneDragon.Core.Abstractions.Geometry.Point? position = value2;
						bool pcAlt = area.PcAlt;
						string gamepadKey = area.GamepadKey;
						return controller.Click(position, null, pcAlt, gamepadKey) ? RoundSuccess(_chosenCoffee.CoffeeName, null, ShortDelay) : RoundRetry("点击失败 " + item, null, RetryDelay);
					}
				}
			}
		}
		return RoundRetry("没找到目标咖啡", null, RetryDelay);
	}

	private IReadOnlyList<OcrMatchResult> GetCoffeeListOcrResults(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		using Mat image = CreateCoffeeListOcrImage(screen, area);
		IReadOnlyList<OcrMatchResult> results = base.ZContext.OcrService.GetOcrResultListForCrop(
			image,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1);
		return results.Select(delegate(OcrMatchResult result)
		{
			result.AddOffset(area.LeftTop);
			return result;
		}).ToArray();
	}

	private ChargePlanItem BuildChargePlan(ZzzOd.GameLogic.GameData.Coffee coffee, ChargePlanItem? matchedPlan)
	{
		return new ChargePlanItem
		{
			TabName = (coffee.Tab?.TabName ?? string.Empty),
			CategoryName = (coffee.Category?.CategoryName ?? string.Empty),
			MissionTypeName = (coffee.MissionType?.MissionTypeName ?? string.Empty),
			MissionName = coffee.Mission?.MissionName,
			PredefinedTeamIndex = _config.PredefinedTeamIndex,
			AutoBattleConfig = _config.AutoBattle,
			RunTimes = 0,
			PlanTimes = 1,
			CardNum = (matchedPlan?.CardNum ?? _config.CardNum)
		};
	}

	private int GetCurrentDayOfWeek()
	{
		int dayOfWeek = (int)_now().ToUniversalTime().ToOffset(TimeSpan.FromHours(base.ZContext.GameAccountConfig.GameRefreshHourOffset)).DayOfWeek;
		return (dayOfWeek == 0) ? 7 : dayOfWeek;
	}

	private static bool CoffeeStrengthWordsMatch(string expected, string actual)
	{
		if (expected.Contains('浓', StringComparison.Ordinal) != actual.Contains('浓', StringComparison.Ordinal))
		{
			return false;
		}
		return expected.Contains('淡', StringComparison.Ordinal) == actual.Contains('淡', StringComparison.Ordinal);
	}

	private static Task<OperationResult> DefaultTransportAsync(ZContext context, CoffeeConfig config)
	{
		if (!CoffeeTransportPoint.TryFromValue(config.TransportPoint, out CoffeeTransportPoint point))
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "无效咖啡传送点 " + config.TransportPoint));
		}
		return new Transport(context, point.AreaName, point.TransportPointName, waitAtLast: false).ExecuteAsync();
	}

	private static Task<OperationResult> DefaultWaitNormalWorldAsync(ZContext context)
	{
		return new WaitNormalWorld(context, checkOnce: true).ExecuteAsync();
	}

	private static Task<OperationResult> DefaultBackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}

	private static Task<OperationResult> DefaultChargePlanAfterwardsAsync(ZContext context, CoffeeConfig config)
	{
		IApplication application = context.RunContext.GetApplication("charge_plan", config.InstanceIndex, "one_dragon");
		return application.ExecuteAsync(CancellationToken.None);
	}

	private OperationRoundResult DefaultMoveAndInteract(ZContext context, CoffeeConfig config, Mat? screen)
	{
		if (!(context.Controller is ZPcController zPcController))
		{
			return RoundRetry("控制器不可用", null, RetryDelay);
		}
		if (string.Equals(config.TransportPoint, CoffeeTransportPoint.SixthStreet.Value, StringComparison.Ordinal))
		{
			if (_turnCompensator == null)
			{
				return RoundRetry("转向控制器不可用", null, RetryDelay);
			}
			OperationRoundResult operationRoundResult = TurnToAngleHelper.TurnToAngle(GetMiniMapAngle(context, screen), _turnCompensator, 180.0, "转向正西");
			if (!operationRoundResult.IsSuccess)
			{
				WriteMoveAndInteractTurnFailureEvidence(context, screen, operationRoundResult);
				return operationRoundResult;
			}
			zPcController.MoveW(press: true, TimeSpan.FromSeconds(1L), release: true);
		}
		string text = null;
		string beforeScreenshotPath = null;
		CoffeeMoveAndInteractRecognitionSummary beforeSummary = null;
		if (ActionLevelDebugEvidenceWriter.IsEnabled)
		{
			text = ActionLevelDebugEvidenceWriter.CreateFileStem("coffee-move-and-interact");
			beforeScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(text, "before", screen);
			beforeSummary = GetCoffeeMoveAndInteractRecognitionSummary(context, screen);
		}
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		zPcController.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		if (text != null)
		{
			Thread.Sleep(TimeSpan.FromSeconds(1L));
			Mat mat = Screenshot();
			CoffeeMoveAndInteractRecognitionSummary coffeeMoveAndInteractRecognitionSummary = GetCoffeeMoveAndInteractRecognitionSummary(context, mat);
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(text, "after", mat);
			WriteMoveAndInteractEvidence(text, beforeScreenshotPath, beforeSummary, afterScreenshotPath, coffeeMoveAndInteractRecognitionSummary);
		}
		return RoundSuccess();
	}

	private static MiniMapAngleResult GetMiniMapAngle(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new MiniMapAngleResult(PlayMaskFound: false, null);
		}
		WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = context.WorldPatrolService.CutMiniMap(context, screen);
		return new MiniMapAngleResult(worldPatrolMiniMapSnapshot.PlayMaskFound, worldPatrolMiniMapSnapshot.ViewAngle);
	}

	private CoffeeMoveAndInteractRecognitionSummary GetCoffeeMoveAndInteractRecognitionSummary(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new CoffeeMoveAndInteractRecognitionSummary
			{
				ActiveScreenName = null,
				PointOrderResult = "未获取截图",
				FailureReason = "未获取截图"
			};
		}
		OperationRoundResult operationRoundResult = RoundByFindArea(screen, "咖啡店", "点单");
		return new CoffeeMoveAndInteractRecognitionSummary
		{
			ActiveScreenName = ScreenUtils.GetMatchScreenName(context, screen),
			MiniMapAngle = GetMiniMapAngle(context, screen),
			PointOrderResult = (operationRoundResult.Status ?? string.Empty),
			PointOrderVisible = operationRoundResult.IsSuccess,
			OcrTexts = (from result in context.OcrService.GetOcrResultList(screen)
				orderby result.Y, result.X
				select result.Text).ToArray(),
			FailureReason = (operationRoundResult.IsSuccess ? null : operationRoundResult.Status)
		};
	}

	private void WriteMoveAndInteractEvidence(string fileStem, string? beforeScreenshotPath, CoffeeMoveAndInteractRecognitionSummary? beforeSummary, string? afterScreenshotPath, CoffeeMoveAndInteractRecognitionSummary afterSummary)
	{
		ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
		{
			FileStem = fileStem,
			AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("coffee"),
			OperationName = base.OperationName,
			NodeName = "移动交互",
			DotNetMethod = "ZzzOd.GameLogic.Application.Coffee.CoffeeOperation.MoveAndInteract()",
			BaselineParityRequirement = "Coffee move_and_interact turns to absolute angle 180 at 六分街咖啡店, moves forward for 1 second, waits 1 second, then presses interact for 0.2 seconds.",
			BeforeScreenshotPath = beforeScreenshotPath,
			BeforeRecognitionSummary = beforeSummary,
			ActionKind = "turn_move_key_press",
			ActionTarget = "target_angle=180; key=w; key=f",
			ExpectedNextState = "咖啡店点单 page, 点单 visible",
			AfterScreenshotPath = afterScreenshotPath,
			AfterRecognitionSummary = afterSummary,
			TransitionResult = (afterSummary.PointOrderVisible ? "entered_coffee_shop" : "point_order_not_visible"),
			FailureReason = (afterSummary.PointOrderVisible ? null : afterSummary.FailureReason),
			RetryStoppedBecauseOfSuspectedLoop = false
		});
	}

	private void WriteMoveAndInteractTurnFailureEvidence(ZContext context, Mat? screen, OperationRoundResult turnResult)
	{
		if (ActionLevelDebugEvidenceWriter.IsEnabled)
		{
			string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem("coffee-move-and-interact-turn");
			string text = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", screen);
			CoffeeMoveAndInteractRecognitionSummary coffeeMoveAndInteractRecognitionSummary = GetCoffeeMoveAndInteractRecognitionSummary(context, screen);
			ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
			{
				FileStem = fileStem,
				AppId = ActionLevelDebugEvidenceWriter.GetApplicationId("coffee"),
				OperationName = base.OperationName,
				NodeName = "移动交互",
				DotNetMethod = "ZzzOd.GameLogic.Application.Coffee.CoffeeOperation.MoveAndInteract()",
				BaselineParityRequirement = "Coffee move_and_interact uses turn_to_angle target_angle=180 before moving and interacting at 六分街咖啡店.",
				BeforeScreenshotPath = text,
				BeforeRecognitionSummary = coffeeMoveAndInteractRecognitionSummary,
				ActionKind = "turn_to_angle",
				ActionTarget = "target_angle=180",
				ExpectedNextState = "mini map angle recognized and facing west",
				AfterScreenshotPath = text,
				AfterRecognitionSummary = coffeeMoveAndInteractRecognitionSummary,
				TransitionResult = "turn_not_ready",
				FailureReason = turnResult.Status,
				RetryStoppedBecauseOfSuspectedLoop = false
			});
		}
	}
}

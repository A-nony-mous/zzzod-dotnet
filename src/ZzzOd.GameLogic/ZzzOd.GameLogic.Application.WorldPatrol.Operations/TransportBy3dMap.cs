using System;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Screen;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 通过 3D 地图传送到锄大地路线起点。
/// </summary>
public sealed class TransportBy3dMap : ZOperation
{
	private readonly WorldPatrolArea _targetArea;

	private readonly string _targetTransportName;

	private readonly ITransportBy3dMapServices _services;

	private CancellationToken _executionCancellationToken;

	/// <summary>
	/// 初始化 3D 地图传送操作。
	/// </summary>
	public TransportBy3dMap(ZContext context, WorldPatrolArea targetArea, string targetTransportName, ITransportBy3dMapServices? services = null)
		: base(context, "传送")
	{
		_targetArea = targetArea;
		_targetTransportName = targetTransportName;
		_services = services ?? new DefaultTransportBy3dMapServices();
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_executionCancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 初始回到大世界。
	/// </summary>
	[OperationNode("初始回到大世界", IsStartNode = true)]
	public async Task<OperationRoundResult> BackAtFirst()
	{
		string currentScreen = _services.CheckCurrentScreen(base.ZContext, base.LastScreenshot, new string[] { "3D地图" });
		if (string.Equals(currentScreen, "3D地图", StringComparison.Ordinal))
		{
			return RoundSuccess(currentScreen);
		}
		return RoundByOperationResult(await _services.BackToNormalWorldAsync(base.ZContext, _executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 打开 3D 地图。
	/// </summary>
	[NodeFrom("初始回到大世界")]
	[OperationNode("打开3D地图")]
	public OperationRoundResult OpenMap()
	{
		if (string.Equals(_services.CheckCurrentScreen(base.ZContext, base.LastScreenshot, new string[] { "3D地图" }), "3D地图", StringComparison.Ordinal))
		{
			return RoundSuccess("3D地图");
		}
		return _services.Open3dMap(base.ZContext, base.LastScreenshot) ? RoundWait("点击打开3D地图", null, TimeSpan.FromSeconds(1L)) : RoundRetry("未发现地图", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 选择区域。
	/// </summary>
	[NodeFrom("选择子区域", Success = false)]
	[NodeFrom("关闭区域信息弹窗")]
	[NodeFrom("初始回到大世界", Status = "3D地图")]
	[NodeFrom("打开3D地图")]
	[OperationNode("选择区域", NodeMaxRetryTimes = 20)]
	public OperationRoundResult ChooseArea()
	{
		string areaName = _targetArea.ParentArea?.AreaName ?? _targetArea.AreaName;
		OperationResult operationResult = _services.ChooseArea(base.ZContext, base.LastScreenshot, areaName, _targetArea);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 展开子区域列表。
	/// </summary>
	[NodeFrom("选择区域")]
	[OperationNode("展开子区域列表")]
	public OperationRoundResult ExpandSubArea()
	{
		if (_targetArea.ParentArea == null)
		{
			return RoundSuccess("无需选择");
		}
		OperationResult operationResult = _services.ExpandSubArea(base.ZContext, base.LastScreenshot);
		return RoundByOperationResult(operationResult);
	}

	/// <summary>
	/// 选择子区域。
	/// </summary>
	[NodeFrom("展开子区域列表")]
	[OperationNode("选择子区域", NodeMaxRetryTimes = 6)]
	public OperationRoundResult ChooseSubArea()
	{
		OperationResult operationResult = _services.ChooseSubArea(base.ZContext, base.LastScreenshot, _targetArea.AreaName);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 打开筛选。
	/// </summary>
	[NodeFrom("展开子区域列表", Status = "无需选择")]
	[NodeFrom("选择子区域")]
	[OperationNode("打开筛选")]
	public OperationRoundResult OpenFilter()
	{
		OperationResult operationResult = _services.OpenFilter(base.ZContext, base.LastScreenshot);
		return string.Equals(operationResult.Status, "标题-标识点筛选", StringComparison.Ordinal) ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 筛选传送点。
	/// </summary>
	[NodeFrom("打开筛选")]
	[OperationNode("筛选传送点")]
	public OperationRoundResult ChooseFilter()
	{
		string targetWord = (_targetArea.IsHollow ? "裂隙信标" : "传送");
		OperationResult operationResult = _services.ChooseFilter(base.ZContext, base.LastScreenshot, targetWord);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 关闭筛选。
	/// </summary>
	[NodeFrom("筛选传送点")]
	[OperationNode("关闭筛选")]
	public OperationRoundResult CloseFilter()
	{
		OperationResult operationResult = _services.CloseFilter(base.ZContext, base.LastScreenshot);
		return string.Equals(operationResult.Status, "3D地图", StringComparison.Ordinal) ? RoundSuccess(operationResult.Status) : RoundWait("关闭筛选", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 最小缩放。
	/// </summary>
	[NodeFrom("关闭筛选")]
	[OperationNode("最小缩放")]
	public OperationRoundResult ClickMiniScale()
	{
		return RoundByOperationResult(_services.ClickMiniScale(base.ZContext));
	}

	/// <summary>
	/// 初始化传送点搜索。
	/// </summary>
	[NodeFrom("最小缩放")]
	[OperationNode("初始化传送点搜索")]
	public OperationRoundResult InitTransportPointSearch()
	{
		OperationResult operationResult = _services.InitTransportPointSearch(base.ZContext, _targetArea, _targetTransportName);
		return RoundByOperationResult(operationResult);
	}

	/// <summary>
	/// 搜索传送点循环。
	/// </summary>
	[NodeFrom("初始化传送点搜索")]
	[OperationNode("搜索传送点循环", NodeMaxRetryTimes = 8)]
	public OperationRoundResult SearchTransportPointLoop()
	{
		OperationResult operationResult = _services.SearchTransportPoint(base.ZContext, base.LastScreenshot, _targetTransportName, _executionCancellationToken);
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 关闭区域信息弹窗。
	/// </summary>
	[NodeFrom("搜索传送点循环", Success = false)]
	[OperationNode("关闭区域信息弹窗")]
	public OperationRoundResult CloseAreaInfoPopup()
	{
		_services.CloseAreaInfoPopup(base.ZContext, base.LastScreenshot);
		return RoundSuccess();
	}

	/// <summary>
	/// 点击前往。
	/// </summary>
	[NodeFrom("搜索传送点循环")]
	[OperationNode("点击前往")]
	public OperationRoundResult ClickGo()
	{
		if (base.NodeClicked)
		{
			if (base.LastScreenshot == null)
			{
				return RoundRetry("未获取截图", null, TimeSpan.FromSeconds(1L));
			}
			FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindArea(base.ZContext, base.LastScreenshot, "3D地图", "按钮-前往");
			return (findAreaResultEnum == FindAreaResultEnum.True) ? RoundWait("按钮-前往", null, TimeSpan.FromSeconds(1L)) : RoundSuccess("按钮-前往", null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult = _services.ClickGo(base.ZContext, base.LastScreenshot);
		return operationResult.IsSuccess ? OnAreaClicked("3D地图", "按钮-前往", TimeSpan.FromSeconds(1L), waitForConfirmation: true) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 等待画面加载。
	/// </summary>
	[NodeFrom("点击前往")]
	[OperationNode("等待画面加载")]
	public async Task<OperationRoundResult> BackAtLast()
	{
		return RoundByOperationResult(await _services.WaitNormalWorldAfterTransportAsync(base.ZContext, _targetArea, _executionCancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}
}

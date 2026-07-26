using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Utils;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业主流程。
/// </summary>
public sealed class RandomPlayOperation : ZOperation
{
	/// <summary>已选择全部录像带。</summary>
	public const string StatusAllVideoChoose = "已选择全部录像带";

	/// <summary>正在营业。</summary>
	public const string StatusAlreadyRunning = "正在营业";

	private readonly RandomPlayConfig _config;

	private readonly RandomPlayRunRecord _runRecord;

	private readonly IRandomPlayOperationServices _services;

	private readonly Func<DateTimeOffset> _now;

	private readonly List<string> _needVideoThemes = new List<string>();

	private int _currentIndex;

	private bool _retriedTransport;

	/// <summary>
	/// 当前待选择录像带主题。
	/// </summary>
	public IReadOnlyList<string> NeedVideoThemes => _needVideoThemes;

	/// <summary>
	/// 初始化录像店营业主流程。
	/// </summary>
	public RandomPlayOperation(ZContext context, RandomPlayConfig config, RandomPlayRunRecord runRecord, IRandomPlayOperationServices? services = null, Func<DateTimeOffset>? now = null)
		: base(context, "录像店营业")
	{
		_config = config;
		_runRecord = runRecord;
		_services = services ?? new DefaultRandomPlayOperationServices();
		_now = now ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.Now));
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_needVideoThemes.Clear();
		_currentIndex = 0;
		_retriedTransport = false;
		return Task.CompletedTask;
	}

	/// <summary>
	/// 根据运行日期决定优先宣传员位置。
	/// </summary>
	public static int GetPromoterSlotIndex(string? dt)
	{
		if (!string.IsNullOrWhiteSpace(dt))
		{
			if (char.IsDigit(dt[dt.Length - 1]))
			{
				return (dt[dt.Length - 1] - 48) % 2 + 1;
			}
		}
		return 1;
	}

	/// <summary>
	/// 在候选主题中选出与识别文本最相近的一个，未达相似度下限时返回 null。
	/// </summary>
	public static string? FindBestTheme(string? ocrText, IReadOnlyList<string> candidates)
	{
		if (string.IsNullOrEmpty(ocrText))
		{
			return null;
		}
		int? index = StringUtils.FindBestMatchByDifflib(ocrText, candidates);
		return index.HasValue ? candidates[index.Value] : null;
	}

	private string GetCurrentGameRefreshDt()
	{
		return _now().ToUniversalTime().ToOffset(TimeSpan.FromHours(_runRecord.GameRefreshHourOffset)).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// 传送到录像店营业入口。
	/// </summary>
	[NodeFrom("等待经营画面加载", Success = false)]
	[OperationNode("传送", IsStartNode = true)]
	public async Task<OperationRoundResult> Transport()
	{
		if (string.Equals(base.PreviousNode.Name, "等待经营画面加载", StringComparison.Ordinal))
		{
			if (_retriedTransport)
			{
				return RoundFail("等待经营画面加载失败，重传送超限");
			}
			_retriedTransport = true;
		}
		RandomPlayTransportPoint point = RandomPlayTransportPoint.FromValue(_config.TransportPoint);
		_services.ClearPendingTurnSample();
		return RoundByOperationResult(await _services.TransportAsync(base.ZContext, point).ConfigureAwait(continueOnCapturedContext: false));
	}

	/// <summary>
	/// 移动并交互进入经营页面。
	/// </summary>
	[NodeFrom("传送")]
	[OperationNode("移动交互", NodeMaxRetryTimes = 10)]
	public async Task<OperationRoundResult> MoveAndInteract()
	{
		return await _services.MoveAndInteractAsync(base.ZContext, _config, base.LastScreenshot).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// 等待经营页面加载。
	/// </summary>
	[NodeFrom("移动交互")]
	[OperationNode("等待经营画面加载", NodeMaxRetryTimes = 10)]
	public OperationRoundResult WaitRun()
	{
		if (_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "昨日账本"))
		{
			_services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "按钮-关闭");
			return RoundRetry("昨日账本", null, TimeSpan.FromSeconds(1L));
		}
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "经营状况");
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(operationResult.Status);
		}
		OperationResult operationResult2 = _services.ClickText(base.ZContext, base.LastScreenshot, "查看经营状况", "影像店营业", "右侧选项区域");
		if (operationResult2.IsSuccess)
		{
			return RoundRetry(operationResult2.Status, null, TimeSpan.FromSeconds(1L));
		}
		return RoundRetry("等待经营画面", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 识别是否已经营业。
	/// </summary>
	[NodeFrom("等待经营画面加载")]
	[OperationNode("识别营业状态")]
	public OperationRoundResult CheckRunning()
	{
		return _services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "正在营业") ? RoundSuccess("正在营业") : RoundSuccess();
	}

	/// <summary>
	/// 关闭经营页面。
	/// </summary>
	[NodeFrom("识别营业状态", Status = "正在营业")]
	[OperationNode("关闭经营页面")]
	public OperationRoundResult CloseBusinessPage()
	{
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "返回");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 点击宣传员入口。
	/// </summary>
	[NodeFrom("识别营业状态")]
	[OperationNode("点击宣传员入口")]
	public OperationRoundResult ClickPromoterEntry()
	{
		OperationResult operationResult = _services.ClickArea(base.ZContext, "影像店营业", "宣传员入口");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 选择宣传员。
	/// </summary>
	[NodeFrom("点击宣传员入口")]
	[OperationNode("选择宣传员")]
	public OperationRoundResult ChoosePromoter()
	{
		if (!_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "选择宣传员"))
		{
			return RoundRetry("未找到 选择宣传员", null, GetRemainingScreenshotRoundDelay(TimeSpan.FromSeconds(1L)));
		}
		if (_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "换下"))
		{
			OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "返回");
			return RoundByOperationResult(operationResult, null, retryOnFail: true);
		}
		int promoterSlotIndex = GetPromoterSlotIndex(GetCurrentGameRefreshDt());
		if (string.Equals(_config.AgentName1, "随机", StringComparison.Ordinal) || string.Equals(_config.AgentName2, "随机", StringComparison.Ordinal))
		{
			_services.ClickArea(base.ZContext, "影像店营业", $"宣传员-{promoterSlotIndex}", TimeSpan.FromSeconds(1L));
			OperationResult operationResult2 = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "确认");
			return operationResult2.IsSuccess ? RoundSuccess(operationResult2.Status) : RoundRetry(operationResult2.Status, null, TimeSpan.FromSeconds(1L));
		}
		string[] source = ((promoterSlotIndex != 1) ? new string[2] { _config.AgentName2, _config.AgentName1 } : new string[2] { _config.AgentName1, _config.AgentName2 });
		foreach (string item in source.Where((string item) => !string.IsNullOrWhiteSpace(item)))
		{
			if (_services.TrySelectAgent(base.ZContext, base.LastScreenshot, item))
			{
				OperationResult operationResult3 = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "确认");
				return operationResult3.IsSuccess ? RoundSuccess(operationResult3.Status) : RoundRetry(operationResult3.Status, null, TimeSpan.FromSeconds(1L));
			}
		}
		if (base.NodeRetryTimes >= 2)
		{
			OperationResult operationResult4 = _services.ClickArea(base.ZContext, "影像店营业", "宣传员-1");
			if (operationResult4.IsSuccess)
			{
				OperationResult operationResult5 = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "确认");
				return operationResult5.IsSuccess ? RoundSuccess(operationResult5.Status) : RoundRetry(operationResult5.Status, null, TimeSpan.FromSeconds(1L));
			}
		}
		_services.ScrollPromoterList(base.ZContext);
		return RoundRetry("所有候选代理人匹配失败", null, TimeSpan.FromMilliseconds(500L));
	}

	/// <summary>
	/// 识别录像带主题。
	/// </summary>
	[NodeFrom("选择宣传员")]
	[OperationNode("识别录像带主题")]
	public OperationRoundResult CheckVideoTheme()
	{
		if (!_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "经营状况"))
		{
			return RoundRetry("未找到 经营状况", null, TimeSpan.FromSeconds(1L));
		}
		_needVideoThemes.Clear();
		foreach (string item in _services.ReadVideoThemes(base.ZContext, base.LastScreenshot))
		{
			_needVideoThemes.Add(item);
		}
		foreach (string item2 in RandomPlayVideoThemes.All)
		{
			if (_needVideoThemes.Count >= 3)
			{
				break;
			}
			if (!_needVideoThemes.Contains<string>(item2, StringComparer.Ordinal))
			{
				_needVideoThemes.Add(item2);
			}
		}
		return RoundSuccess();
	}

	/// <summary>
	/// 点击录像带入口。
	/// </summary>
	[NodeFrom("识别录像带主题")]
	[OperationNode("点击录像带入口")]
	public OperationRoundResult ClickVideoEntry()
	{
		OperationResult operationResult = _services.ClickArea(base.ZContext, "影像店营业", "录像带入口");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 识别推荐上架。
	/// </summary>
	[NodeFrom("点击录像带入口")]
	[OperationNode("识别推荐上架")]
	public OperationRoundResult CheckRecommended()
	{
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "推荐上架");
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		return _services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "上架筛选") ? RoundSuccess("上架筛选", null, TimeSpan.FromSeconds(1L)) : RoundRetry("未找到 上架筛选", null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 打开上架筛选。
	/// </summary>
	[NodeFrom("识别推荐上架", Status = "上架筛选")]
	[NodeFrom("上架")]
	[NodeFrom("上架", Success = false)]
	[OperationNode("上架筛选")]
	public OperationRoundResult ClickFilter()
	{
		if (_currentIndex >= _needVideoThemes.Count)
		{
			return RoundSuccess("已选择全部录像带");
		}
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "上架筛选");
		if (!operationResult.IsSuccess)
		{
			return RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		_currentIndex++;
		return RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 选择主题。
	/// </summary>
	[NodeFrom("上架筛选")]
	[OperationNode("选择主题")]
	public OperationRoundResult ChooseTheme()
	{
		string text = _needVideoThemes[_currentIndex - 1];
		OperationResult operationResult = _services.ClickTheme(base.ZContext, base.LastScreenshot, text);
		if (operationResult.IsSuccess)
		{
			return RoundSuccess(operationResult.Status, null, TimeSpan.FromSeconds(1L));
		}
		if (string.Equals(operationResult.Status, "点击失败 " + text, StringComparison.Ordinal))
		{
			return RoundRetry(operationResult.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		_services.ScrollThemeList(base.ZContext);
		return RoundRetry("未找到" + text, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 上架录像带。
	/// </summary>
	[NodeFrom("选择主题")]
	[OperationNode("上架")]
	public OperationRoundResult ChooseOnShelf()
	{
		if (_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "下架"))
		{
			return RoundSuccess();
		}
		if (!_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "上架"))
		{
			return RoundWaitForScreenshotRound(TimeSpan.FromSeconds(1L), "未找到 上架");
		}
		OperationResult operationResult = _services.ClickArea(base.ZContext, "影像店营业", "上架");
		OperationResult operationResult2 = _services.ClickArea(base.ZContext, "影像店营业", "上架", TimeSpan.FromMilliseconds(500L));
		return (operationResult.IsSuccess && operationResult2.IsSuccess) ? RoundWait("上架", null, TimeSpan.FromSeconds(1L)) : RoundRetry(operationResult.Status, null, GetRemainingScreenshotRoundDelay(TimeSpan.FromSeconds(1L)));
	}

	/// <summary>
	/// 返回经营页面。
	/// </summary>
	[NodeFrom("识别推荐上架", Status = "推荐上架")]
	[NodeFrom("上架筛选", Status = "已选择全部录像带")]
	[OperationNode("返回")]
	public OperationRoundResult Back()
	{
		if (_services.IsAreaVisible(base.ZContext, base.LastScreenshot, "影像店营业", "经营状况"))
		{
			return RoundSuccess();
		}
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "返回");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 开始营业。
	/// </summary>
	[NodeFrom("返回")]
	[OperationNode("开始营业")]
	public OperationRoundResult Start()
	{
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "开始营业");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 确认开始营业。
	/// </summary>
	[NodeFrom("开始营业")]
	[OperationNode("确认营业")]
	public OperationRoundResult ConfirmBusiness()
	{
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "开始营业-确认");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 营业后确认。
	/// </summary>
	[NodeFrom("确认营业")]
	[OperationNode("营业后确认")]
	public OperationRoundResult Confirm()
	{
		OperationResult operationResult = _services.FindAndClickArea(base.ZContext, base.LastScreenshot, "影像店营业", "营业后确认");
		return operationResult.IsSuccess ? RoundSuccess(operationResult.Status) : RoundRetry(operationResult.Status, null, TimeSpan.FromSeconds(1L));
	}

	/// <summary>
	/// 返回大世界。
	/// </summary>
	[NodeFrom("营业后确认")]
	[NodeFrom("关闭经营页面")]
	[OperationNodeNotify(OperationNodeNotifyTiming.PreviousDone)]
	[OperationNode("返回大世界")]
	public async Task<OperationRoundResult> BackToWorld()
	{
		return RoundByOperationResult(await _services.BackToWorldAsync(base.ZContext).ConfigureAwait(continueOnCapturedContext: false));
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.ScreenArea;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 在出战画面选择预备编队。
/// </summary>
public sealed class ChoosePredefinedTeam : ZOperation
{
	private const int TeamScrollStep = 4;

	private readonly IReadOnlyList<int> _targetTeamIndexList;

	private readonly int _maxScrollPageCount;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private readonly TimeSpan _teamClickDelay;

	private readonly TimeSpan _confirmClickDelay;

	private readonly TimeSpan _confirmAfterMouseDelay;

	private int _scrollPageCount;

	/// <summary>
	/// 初始化预备编队选择操作。
	/// </summary>
	public ChoosePredefinedTeam(ZContext context, IReadOnlyList<int> targetTeamIndexList, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null, TimeSpan? teamClickDelay = null, TimeSpan? confirmClickDelay = null, TimeSpan? confirmAfterMouseDelay = null)
		: base(context, "选择预备编队 " + string.Join(",", targetTeamIndexList))
	{
		_targetTeamIndexList = targetTeamIndexList;
		_maxScrollPageCount = (from index in targetTeamIndexList
			where index >= 0
			select index / 4).DefaultIfEmpty(0).Max();
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
		_teamClickDelay = teamClickDelay ?? TimeSpan.FromMilliseconds(500L);
		_confirmClickDelay = confirmClickDelay ?? TimeSpan.FromMilliseconds(500L);
		_confirmAfterMouseDelay = confirmAfterMouseDelay ?? TimeSpan.FromMilliseconds(500L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_scrollPageCount = 0;
		return Task.CompletedTask;
	}

	[OperationNode("画面识别", IsStartNode = true, NodeMaxRetryTimes = 10)]
	private OperationRoundResult CheckScreen()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = _retryDelay;
		OperationRoundResult operationRoundResult = RoundByFindArea(lastScreenshot, "实战模拟室", "预备编队", null, retryDelay);
		return operationRoundResult.IsSuccess ? RoundSuccess(operationRoundResult.Status) : RoundRetry(operationRoundResult.Status, null, _retryDelay);
	}

	[NodeFrom("画面识别", Status = "预备编队")]
	[OperationNode("点击预备编队")]
	private OperationRoundResult ClickTeam()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "实战模拟室", "预备编队", _preClickDelay, _retryDelay, _retryDelay);
	}

	[NodeFrom("点击预备编队")]
	[NodeFrom("尝试查找编队")]
	[OperationNode("选择编队")]
	private OperationRoundResult ChooseTeam()
	{
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("实战模拟室", "预备出战");
		Mat? lastScreenshot = base.LastScreenshot;
		IReadOnlyList<IReadOnlyList<int>> colorRange = new IReadOnlyList<int>[2]
		{
			new int[3] { 240, 240, 240 },
			new int[3] { 255, 255, 255 }
		};
		OperationRoundResult operationRoundResult = RoundByOcr(lastScreenshot, "预备出战", area, 0.5, null, null, colorRange);
		if (operationRoundResult.IsSuccess)
		{
			return RoundSuccess(operationRoundResult.Status);
		}
		foreach (int targetTeamIndex in _targetTeamIndexList)
		{
			if (targetTeamIndex >= base.ZContext.TeamConfig.TeamList.Count || targetTeamIndex < -base.ZContext.TeamConfig.TeamList.Count)
			{
				return RoundFail($"选择的预备编队下标错误 {targetTeamIndex}");
			}
			int index = ((targetTeamIndex < 0) ? (base.ZContext.TeamConfig.TeamList.Count + targetTeamIndex) : targetTeamIndex);
			string name = base.ZContext.TeamConfig.TeamList[index].Name;
			IReadOnlyDictionary<string, MatchResultList> readOnlyDictionary;
			if (base.LastScreenshot != null)
			{
				readOnlyDictionary = base.ZContext.OcrService.GetOcrResultMap(base.LastScreenshot);
			}
			else
			{
				IReadOnlyDictionary<string, MatchResultList> readOnlyDictionary2 = new Dictionary<string, MatchResultList>(StringComparer.Ordinal);
				readOnlyDictionary = readOnlyDictionary2;
			}
			IReadOnlyDictionary<string, MatchResultList> readOnlyDictionary3 = readOnlyDictionary;
			string[] array = readOnlyDictionary3.Keys.ToArray();
			int? num = StringUtils.FindBestMatchByDifflib(name, array);
			if (!num.HasValue || readOnlyDictionary3[array[num.Value]].Max == null || base.ZContext.Controller == null)
			{
				return RoundFail("当前页未找到编队 " + name);
			}
			OneDragon.Core.Abstractions.Geometry.Point value = readOnlyDictionary3[array[num.Value]].Max.Center + new OneDragon.Core.Abstractions.Geometry.Point(200, 0);
			base.ZContext.Controller.Click(value);
			if (_teamClickDelay > TimeSpan.Zero)
			{
				Thread.Sleep(_teamClickDelay);
			}
		}
		return RoundWait(null, null, _retryDelay);
	}

	[NodeFrom("选择编队", Success = false)]
	[OperationNode("尝试查找编队")]
	private OperationRoundResult TryFindTeam()
	{
		_scrollPageCount++;
		if (_scrollPageCount > _maxScrollPageCount)
		{
			return RoundFail("选择配队失败");
		}
		if (base.ZContext.Controller == null)
		{
			return RoundFail("选择配队失败");
		}
		OneDragon.Core.Abstractions.Geometry.Point centerPoint = base.ZContext.Controller.CenterPoint;
		OneDragon.Core.Abstractions.Geometry.Point end = centerPoint + new OneDragon.Core.Abstractions.Geometry.Point(0, -500);
		base.ZContext.Controller.DragTo(end, centerPoint);
		return RoundSuccess(null, null, _retryDelay);
	}

	[NodeFrom("选择编队")]
	[OperationNode("选择编队确认")]
	private OperationRoundResult ClickConfirm()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? preDelay = _preClickDelay;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "实战模拟室", "预备出战", preDelay, null, null);
		if (!operationRoundResult.IsSuccess)
		{
			return RoundRetry(operationRoundResult.Status, null, _retryDelay);
		}
		if (_confirmClickDelay > TimeSpan.Zero)
		{
			Thread.Sleep(_confirmClickDelay);
		}
		base.ZContext.Controller?.MouseMove(ScreenNormalWorldEnum.Uid.Center);
		return RoundSuccess(operationRoundResult.Status, null, _confirmAfterMouseDelay);
	}
}

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
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Operations;

/// <summary>
/// 在地图界面执行区域和传送点选择。
/// </summary>
public sealed class MapTransport : ZOperation
{
	private readonly string _areaName;

	private readonly string _tpName;

	private readonly TimeSpan _retryDelay;

	private readonly TimeSpan _preClickDelay;

	private readonly TimeSpan _successDelay;

	private int _reselectAreaTimes;

	/// <summary>
	/// 初始化地图传送操作。
	/// </summary>
	public MapTransport(ZContext context, string areaName, string tpName, TimeSpan? retryDelay = null, TimeSpan? preClickDelay = null, TimeSpan? successDelay = null)
		: base(context, "地图传送 " + areaName + " " + tpName)
	{
		_areaName = areaName;
		_tpName = tpName;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(1L);
		_preClickDelay = preClickDelay ?? TimeSpan.FromMilliseconds(300L);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(1L);
	}

	/// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_reselectAreaTimes = 0;
		return Task.CompletedTask;
	}

	[NodeFrom("选择传送点", Success = false)]
	[OperationNode("选择区域", IsStartNode = true)]
	private OperationRoundResult ChooseArea()
	{
		_reselectAreaTimes++;
		if (_reselectAreaTimes > 3)
		{
			return RoundFail(base.PreviousNode.Status);
		}
		if (!base.ZContext.MapService.AreaNameMap.TryGetValue(_areaName, out MapArea value))
		{
			return RoundFail("地图区域未配置 " + _areaName);
		}
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图", null, _retryDelay);
		}
		IReadOnlyList<MapArea> areaList = base.ZContext.MapService.AreaList;
		string[] targetWords = areaList.Select((MapArea area) => area.AreaName).ToArray();
		int num = areaList.ToList().IndexOf(value);
		IReadOnlyDictionary<string, MatchResultList> ocrResultMap = base.ZContext.OcrService.GetOcrResultMap(base.LastScreenshot);
		int num2 = -1;
		foreach (KeyValuePair<string, MatchResultList> item in ocrResultMap)
		{
			item.Deconstruct(out var key, out var value2);
			string word = key;
			MatchResultList matchResultList = value2;
			int? num3 = StringUtils.FindBestMatchByDifflib(word, targetWords);
			if (!num3.HasValue)
			{
				continue;
			}
			int value3 = num3.Value;
			if (value3 == num)
			{
				if (matchResultList.Max == null || base.ZContext.Controller == null)
				{
					return RoundRetry("点击失败 " + _areaName, null, _retryDelay);
				}
				base.ZContext.Controller.Click(matchResultList.Max.Center);
				return RoundSuccess(_areaName, null, _successDelay);
			}
			if (value3 > num2)
			{
				num2 = value3;
			}
		}
		if (num2 < 0)
		{
			return RoundRetry("未识别到地图区域", null, TimeSpan.FromMilliseconds(500L));
		}
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("点击失败 " + _areaName, null, _retryDelay);
		}
		Point centerPoint = base.ZContext.Controller.CenterPoint;
		Point end = ((num2 > num) ? (centerPoint + new Point(500, 0)) : (centerPoint - new Point(500, 0)));
		base.ZContext.Controller.DragTo(end, centerPoint);
		return RoundRetry(null, null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("选择区域")]
	[OperationNode("选择传送点", NodeMaxRetryTimes = 10)]
	private OperationRoundResult ChooseTransportPoint()
	{
		if (!base.ZContext.MapService.AreaNameMap.TryGetValue(_areaName, out MapArea value))
		{
			return RoundFail("地图区域未配置 " + _areaName);
		}
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图", null, _retryDelay);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("地图", "传送点名称");
		if (area == null)
		{
			return RoundFail("区域未配置 传送点名称");
		}
		IReadOnlyDictionary<string, MatchResultList> ocrResultMap = base.ZContext.OcrService.GetOcrResultMap(base.LastScreenshot);
		if (ocrResultMap.Count == 0)
		{
			// 对应 Python map_transport.py:66 的 wait_round_time=1（补足制，非固定延时）。
			return RoundRetry("未识别到传送点", null, null, _retryDelay);
		}
		string text = null;
		List<string> list = new List<string>();
		foreach (string key in ocrResultMap.Keys)
		{
			int? num = StringUtils.FindBestMatchByDifflib(key, value.TpList);
			if (num.HasValue)
			{
				string text2 = value.TpList[num.Value];
				list.Add(text2);
				if (string.Equals(_tpName, text2, StringComparison.Ordinal))
				{
					text = key;
				}
			}
		}
		if (text != null)
		{
			MatchResult max = ocrResultMap[text].Max;
			if (max == null || base.ZContext.Controller == null)
			{
				return RoundRetry("点击失败 " + _tpName, null, _retryDelay);
			}
			base.ZContext.Controller.Click(max.Center);
			return RoundSuccess(_tpName, null, _successDelay);
		}
		if (base.ZContext.Controller == null)
		{
			return RoundRetry("点击失败 " + _tpName, null, _retryDelay);
		}
		int num2 = 0;
		foreach (string tp in value.TpList)
		{
			if (string.Equals(tp, _tpName, StringComparison.Ordinal))
			{
				break;
			}
			if (list.Contains<string>(tp, StringComparer.Ordinal))
			{
				num2++;
			}
		}
		Point point = area.Center + new Point(-20, -20);
		Point end = ((num2 > 0) ? (point + new Point(-800, 0)) : (point + new Point(750, 0)));
		base.ZContext.Controller.DragTo(end, point);
		return RoundRetry(null, num2, _retryDelay);
	}

	[NodeFrom("选择传送点")]
	[OperationNode("点击传送")]
	private OperationRoundResult ClickTransport()
	{
		return RoundByFindAndClickArea(base.LastScreenshot, "地图", "确认", _preClickDelay, _successDelay, _retryDelay);
	}
}

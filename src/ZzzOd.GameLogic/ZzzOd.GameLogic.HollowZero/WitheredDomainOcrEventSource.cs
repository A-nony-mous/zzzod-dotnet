using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.HollowZero;

/// <summary>
/// 按 BaselineParity 空洞流程的区域和 OCR 顺序识别已具备操作的特殊事件。
/// 普通事件需要各自的 OCR 选项操作，未配置时保持未处理，不能以点击空白代替完成。
/// </summary>
public sealed class WitheredDomainOcrEventSource : IHollowEventSource
{
	private static readonly string[] BottomEvents = new string[10]
	{
		HollowZeroSpecialEvent.ResoniumChoose.EventName,
		HollowZeroSpecialEvent.ResoniumConfirm1.EventName,
		HollowZeroSpecialEvent.ResoniumConfirm2.EventName,
		HollowZeroSpecialEvent.ResoniumUpgrade.EventName,
		HollowZeroSpecialEvent.ResoniumDrop.EventName,
		HollowZeroSpecialEvent.ResoniumDrop2.EventName,
		HollowZeroSpecialEvent.ResoniumSwitch.EventName,
		HollowZeroSpecialEvent.SwiftSupplyLife.EventName,
		HollowZeroSpecialEvent.SwiftSupplyCoin.EventName,
		HollowZeroSpecialEvent.SwiftSupplyPress.EventName
	};

	private static readonly string[] RightEvents = new string[6]
	{
		HollowZeroSpecialEvent.CallForSupport.EventName,
		HollowZeroSpecialEvent.ResoniumStore0.EventName,
		HollowZeroSpecialEvent.ResoniumStore1.EventName,
		HollowZeroSpecialEvent.ResoniumStore2.EventName,
		HollowZeroSpecialEvent.ResoniumStore3.EventName,
		HollowZeroSpecialEvent.ResoniumStore4.EventName
	};

	private static readonly string[] EntryOptionEvents = new string[4]
	{
		HollowZeroSpecialEvent.ResoniumStore5.EventName,
		HollowZeroSpecialEvent.CriticalStageEntry.EventName,
		HollowZeroSpecialEvent.CriticalStageEntry2.EventName,
		HollowZeroSpecialEvent.DoorBattleEntry.EventName
	};

	private readonly ZContext _context;

	private readonly WitheredDomainEventDataService _eventData;

	/// <summary>
	/// 初始化 OCR 事件源。
	/// </summary>
	public WitheredDomainOcrEventSource(ZContext context)
	{
		_context = context ?? throw new ArgumentNullException("context");
		_eventData = new WitheredDomainEventDataService(context.Environment);
	}

	/// <inheritdoc />
	public Task<HollowEventDetection?> DetectAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var (captureTimeUtc, mat) = _context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null);
		if (mat == null)
		{
			return Task.FromResult<HollowEventDetection>(null);
		}
		try
		{
			string text = DetectEventName(_context, mat, _eventData);
			if (text == null)
			{
				mat.Dispose();
				return Task.FromResult<HollowEventDetection>(null);
			}
			return Task.FromResult(new HollowEventDetection(text, 1.0, captureTimeUtc, (double)captureTimeUtc.ToUnixTimeMilliseconds() / 1000.0, mat));
		}
		catch
		{
			mat.Dispose();
			throw;
		}
	}

	internal static string? DetectEventName(ZContext context, Mat screen)
	{
		return DetectEventName(context, screen, new WitheredDomainEventDataService(context.Environment));
	}

	private static string? DetectEventName(ZContext context, Mat screen, WitheredDomainEventDataService eventData)
	{
		string text = FindOcrEvent(context, screen, "零号空洞-事件", "底部-选择列表", BottomEvents, restrictToTopLine: false);
		if (text != null)
		{
			return text;
		}
		string[] candidates = eventData.NormalEvents.Select((HollowZeroEvent item) => item.EventName).Concat(RightEvents).Distinct<string>(StringComparer.Ordinal)
			.ToArray();
		string text2 = FindOcrEvent(context, screen, "零号空洞-事件", "事件文本", candidates, restrictToTopLine: true);
		if (text2 != null)
		{
			return text2;
		}
		string text3 = FindOcrEvent(context, screen, "零号空洞-事件", "格子入口选项", EntryOptionEvents, restrictToTopLine: true);
		if (text3 != null)
		{
			return text3;
		}
		string text4 = FindOcrEvent(context, screen, "零号空洞-事件", "底部-清除列表", new string[] { HollowZeroSpecialEvent.CorruptionRemove.EventName }, restrictToTopLine: false);
		if (text4 != null)
		{
			return text4;
		}
		if (FindArea(context, screen, "战斗画面", "按键-普通攻击"))
		{
			return HollowZeroSpecialEvent.InBattle.EventName;
		}
		if (FindArea(context, screen, "零号空洞-事件", "通关-完成"))
		{
			return HollowZeroSpecialEvent.MissionComplete.EventName;
		}
		if (FindArea(context, screen, "零号空洞-事件", "背包已满"))
		{
			return HollowZeroSpecialEvent.FullInBag.EventName;
		}
		if (FindArea(context, screen, "零号空洞-事件", "背包"))
		{
			return HollowZeroSpecialEvent.HollowInside.EventName;
		}
		return FindArea(context, screen, "零号空洞-事件", "旧都失物-返回") ? HollowZeroSpecialEvent.OldCapital.EventName : null;
	}

	private static bool FindArea(ZContext context, Mat screen, string screenName, string areaName)
	{
		return ScreenUtils.FindArea(context, screen, screenName, areaName) == FindAreaResultEnum.True;
	}

	private static string? FindOcrEvent(ZContext context, Mat screen, string screenName, string areaName, IReadOnlyList<string> candidates, bool restrictToTopLine)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea(screenName, areaName);
		if (area == null)
		{
			return null;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, null, area.Rect);
		if (ocrResultList.Count == 0)
		{
			return null;
		}
		int num = ocrResultList.Min((OcrMatchResult result) => result.Y);
		foreach (OcrMatchResult item in ocrResultList)
		{
			if (!restrictToTopLine || item.Y - num < 20)
			{
				int? num2 = StringUtils.FindBestMatchByDifflib(item.Text, candidates);
				if (num2.HasValue)
				{
					return candidates[num2.Value];
				}
			}
		}
		return null;
	}
}

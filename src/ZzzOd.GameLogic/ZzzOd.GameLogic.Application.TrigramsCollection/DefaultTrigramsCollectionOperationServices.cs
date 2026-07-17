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
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.TrigramsCollection;

/// <summary>
/// 默认卦象集录流程服务。
/// </summary>
public sealed class DefaultTrigramsCollectionOperationServices : ITrigramsCollectionOperationServices
{
	/// <inheritdoc />
	public Task<OperationResult> TransportAsync(ZContext context)
	{
		return new Transport(context, "澄辉坪", "阿朔").ExecuteAsync();
	}

	/// <inheritdoc />
	public OperationResult Interact(ZContext context)
	{
		if (!(context.Controller is ZPcController zPcController))
		{
			return new OperationResult(IsSuccess: false, "控制器不支持前台键鼠交互");
		}
		Thread.Sleep(TimeSpan.FromSeconds(1L));
		zPcController.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		return new OperationResult(IsSuccess: true);
	}

	/// <inheritdoc />
	public Task<TrigramOcrMatch?> ReadPriorityTextAsync(ZContext context, Mat? screen, IReadOnlyList<string> priorityWords)
	{
		if (screen == null || context.Controller == null)
		{
			return Task.FromResult<TrigramOcrMatch>(null);
		}
		OneDragon.Core.Abstractions.Geometry.Rect value = new OneDragon.Core.Abstractions.Geometry.Rect(0, context.Controller.StandardHeight / 2, context.Controller.StandardWidth, context.Controller.StandardHeight);
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, null, value);
		string[] targetWords = priorityWords.Select(context.GameTextResolver).ToArray();
		Dictionary<string, OcrMatchResult> dictionary = new Dictionary<string, OcrMatchResult>(StringComparer.Ordinal);
		foreach (OcrMatchResult item in ocrResultList)
		{
			int? num = StringUtils.FindBestMatchByDifflib(item.Text, targetWords);
			if (num.HasValue)
			{
				string key = priorityWords[num.Value];
				if (!dictionary.TryGetValue(key, out var value2) || item.Confidence > value2.Confidence)
				{
					dictionary[key] = item;
				}
			}
		}
		foreach (string priorityWord in priorityWords)
		{
			if (dictionary.TryGetValue(priorityWord, out var value3))
			{
				return Task.FromResult(new TrigramOcrMatch(priorityWord, value3.Center));
			}
		}
		return Task.FromResult<TrigramOcrMatch>(null);
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickGetTrigramAsync(ZContext context)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("卦象集录", "区域-获取卦象");
		if (area == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "区域未配置 区域-获取卦象"));
		}
		ControllerBase? controller = context.Controller;
		int num;
		if (controller == null)
		{
			num = 0;
		}
		else
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			num = (controller.Click(position, null, pcAlt, gamepadKey) ? 1 : 0);
		}
		bool flag = (byte)num != 0;
		return Task.FromResult(new OperationResult(flag, flag ? "区域-获取卦象" : "点击失败 区域-获取卦象"));
	}

	/// <inheritdoc />
	public void DragForTrigram(ZContext context)
	{
		if (context.Controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(context.Controller.StandardWidth - 100, 100);
			OneDragon.Core.Abstractions.Geometry.Point point2 = new OneDragon.Core.Abstractions.Geometry.Point(100, context.Controller.StandardHeight - 100);
			context.Controller.DragTo(point, point2, TimeSpan.FromSeconds(1L));
			context.Controller.DragTo(point2, point, TimeSpan.FromSeconds(1L));
		}
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickConfirmAsync(ZContext context, OneDragon.Core.Abstractions.Geometry.Point? center)
	{
		if (!center.HasValue)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "找不到 确认"));
		}
		bool flag = context.Controller?.Click(center.Value) ?? false;
		return Task.FromResult(new OperationResult(flag, flag ? "确认" : "点击失败 确认"));
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}
}

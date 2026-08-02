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

namespace ZzzOd.GameLogic.Application.HouHouBakery;

/// <summary>
/// 默认吼吼饼铺流程服务。
/// </summary>
public sealed class DefaultHouHouBakeryOperationServices : IHouHouBakeryOperationServices
{
	/// <inheritdoc />
	public Task<OperationResult> TransportAsync(ZContext context)
	{
		return new Transport(context, "布亚斯特城区", "吼吼饼铺").ExecuteAsync();
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
		return new OperationResult(IsSuccess: true, "交互");
	}

	/// <inheritdoc />
	public Task<bool> RecognizeTextAsync(ZContext context, Mat? screen, string targetText)
	{
		if (screen == null)
		{
			return Task.FromResult(result: false);
		}
		string gameTargetText = context.GameTextResolver(targetText);
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen);
		return Task.FromResult(ocrResultList.Any((OcrMatchResult item) => StringUtils.FindByLcs(gameTargetText, item.Text, 0.5)));
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickTextAsync(ZContext context, Mat? screen, string targetText)
	{
		if (screen == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "未获取截图"));
		}
		if (context.Controller == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "点击失败 " + targetText));
		}
		string gameTargetText = context.GameTextResolver(targetText);
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(gameTargetText, item.Text, 0.5));
		if (ocrMatchResult == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "找不到 " + targetText));
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(300L));
		bool flag = context.Controller.Click(ocrMatchResult.Center);
		return Task.FromResult(new OperationResult(flag, flag ? targetText : ("点击失败 " + targetText)));
	}

	/// <inheritdoc />
	public OperationResult ClickCenter(ZContext context)
	{
		if (context.Controller == null)
		{
			return new OperationResult(IsSuccess: false, "控制器不可用");
		}
		bool flag = context.Controller.Click(new OneDragon.Core.Abstractions.Geometry.Point(context.Controller.StandardWidth / 2, context.Controller.StandardHeight / 2));
		return new OperationResult(flag, flag ? "点击盲盒" : "点击失败 盲盒");
	}

	/// <inheritdoc />
	public Task<OperationResult> ClickBlindBoxAsync(ZContext context)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("吼吼饼铺", "盲盒");
		if (area == null)
		{
			return Task.FromResult(new OperationResult(IsSuccess: false, "区域未配置 盲盒"));
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(300L));
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
		return Task.FromResult(new OperationResult(flag, flag ? "盲盒" : "点击失败 盲盒"));
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context)
	{
		return new BackToNormalWorld(context).ExecuteAsync();
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OneDragon.Core.Windows.Controller;
using OpenCvSharp;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.AutoBattle;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.HollowMap;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.CommissionAssistant;

/// <summary>
/// 默认委托助手业务服务。
/// </summary>
public sealed class DefaultCommissionAssistantOperationServices : ICommissionAssistantOperationServices
{
	private sealed record FishingCommandMatch(string CanonicalCommand, string DisplayCommand);

	/// <summary>跳过剧情后需要使用新截图识别确认框。</summary>
	public const string StatusNeedSkipConfirm = "需要重截图确认";

	private const string StatusFishingDone = "钓鱼结束";

	private static readonly IReadOnlyList<IReadOnlyList<int>> WhiteTextColorRange = new IReadOnlyList<int>[2]
	{
		new int[3] { 240, 240, 240 },
		new int[3] { 255, 255, 255 }
	};

	private static readonly string[] FishingCommands = new string[5] { "点击按键抛竿", "等待上鱼", "正确时机点击按键上鱼", "连点", "长按" };

	private const double DialogOptionLcsPercent = 0.5;

	private static readonly TimeSpan ChosenOptionHoldTime = TimeSpan.FromMilliseconds(500L);

	private static readonly TimeSpan ChosenOptionMaxTime = TimeSpan.FromSeconds(2L);

	private readonly DefaultAutoBattleAppServices _autoBattleServices = new DefaultAutoBattleAppServices();

	private readonly Func<ZContext, OperationResult> _waitNormalWorldOnce;

	private bool _witheredDomainInited;

	/// <summary>
	/// 初始化默认委托助手业务服务。
	/// </summary>
	public DefaultCommissionAssistantOperationServices(Func<ZContext, OperationResult>? waitNormalWorldOnce = null)
	{
		_waitNormalWorldOnce = waitNormalWorldOnce ?? new Func<ZContext, OperationResult>(DefaultWaitNormalWorldOnce);
	}

	/// <inheritdoc />
	public bool NeedPauseInBackground(ZContext context, CommissionAssistantConfig config)
	{
		return config.PauseInBackground && context.Controller is WindowsGameController windowsGameController && !windowsGameController.IsGameWindowActive;
	}

	/// <inheritdoc />
	public OperationResult ClickDialogConfirm(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		return ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "委托助手", "对话框确认"), "对话框确认");
	}

	/// <inheritdoc />
	public bool IsInteractVisible(ZContext context, Mat? screen)
	{
		return screen != null && ScreenUtils.FindArea(context, screen, "战斗画面", "按键-交互") == FindAreaResultEnum.True;
	}

	/// <inheritdoc />
	public string? CheckCurrentWorldScreen(ZContext context, Mat? screen)
	{
		return (screen == null) ? null : ScreenUtils.GetMatchScreenName(context, screen, new string[2] { "大世界-普通", "大世界-勘域" });
	}

	/// <inheritdoc />
	public bool IsSecondaryMenuVisible(ZContext context, Mat? screen)
	{
		return screen != null && ScreenUtils.FindArea(context, screen, "委托助手", "左上角返回") == FindAreaResultEnum.True;
	}

	/// <inheritdoc />
	public OperationResult HandleHollow(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		switch (ScreenUtils.FindArea(context, screen, "零号空洞-事件", "背包"))
		{
		case FindAreaResultEnum.AreaNoConfig:
			return new OperationResult(IsSuccess: false, "区域未配置 背包");
		default:
			return new OperationResult(IsSuccess: false, "未在空洞中");
		case FindAreaResultEnum.True:
		{
			if (!_witheredDomainInited)
			{
				try
				{
					int valueOrDefault = context.RunContext.CurrentInstanceIndex.GetValueOrDefault();
					WitheredDomainConfig witheredDomainConfig = WitheredDomainConfig.Load(context.Environment, valueOrDefault, "default");
					context.WitheredDomain.InitBeforeRun(witheredDomainConfig.ChallengeConfig);
				}
				catch (Exception ex) when (((ex is InvalidOperationException || ex is FileNotFoundException || ex is InvalidDataException) ? 1 : 0) != 0)
				{
					return new OperationResult(IsSuccess: false, "空洞上下文初始化失败 " + ex.Message);
				}
				_witheredDomainInited = true;
			}
			DateTimeOffset screenshotTimeUtc2 = screenshotTimeUtc ?? DateTimeOffset.UtcNow;
			HollowZeroMap hollowZeroMap;
			try
			{
				hollowZeroMap = HollowYoloMapService.CalculateCurrentMap(context, screen, screenshotTimeUtc2);
			}
			catch (Exception ex2) when (((ex2 is InvalidOperationException || ex2 is FileNotFoundException || ex2 is InvalidDataException) ? 1 : 0) != 0)
			{
				return new OperationResult(IsSuccess: false, "空洞地图识别失败 " + ex2.Message);
			}
			if (hollowZeroMap == null || hollowZeroMap.ContainsEntry("当前"))
			{
				return new OperationResult(IsSuccess: true, "空洞走格子中");
			}
			return HandleHollowEventText(context, screen);
		}
		}
	}

	/// <inheritdoc />
	public OperationResult ClickHollowFinished(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		return ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "零号空洞-事件", "通关-完成"), "通关-完成");
	}

	private static OperationResult HandleHollowEventText(ZContext context, Mat screen)
	{
		if (context.Controller == null)
		{
			return new OperationResult(IsSuccess: false, "游戏控制器未就绪");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 事件文本");
		}
		using Mat mat = Crop(screen, area);
		if (mat.Empty())
		{
			return new OperationResult(IsSuccess: false, "事件文本区域为空");
		}
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		using Mat mat4 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
		using Mat mat5 = new Mat();
		Cv2.InRange(mat, new Scalar(230.0, 230.0, 230.0), new Scalar(255.0, 255.0, 255.0), mat2);
		Cv2.Dilate(mat2, mat3, mat4, null, 5);
		Cv2.BitwiseAnd(mat, mat, mat5, mat3);
		OcrMatchResult ocrMatchResult = (from result in context.OcrService.GetOcrResultListForCrop(
			mat5,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1)
			where result.Text.Trim().Length > 1
			orderby result.Center.Y descending
			select result).FirstOrDefault();
		if (ocrMatchResult != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point value = new OneDragon.Core.Abstractions.Geometry.Point(ocrMatchResult.Center.X + area.Rect.X1, ocrMatchResult.Center.Y + area.Rect.Y1);
			context.Controller.Click(value);
			Thread.Sleep(TimeSpan.FromMilliseconds(200L));
		}
		context.Controller.Click(new OneDragon.Core.Abstractions.Geometry.Point(area.Rect.X1, area.Rect.Y1));
		return new OperationResult(IsSuccess: false, "未匹配合适的处理方法", TimeSpan.FromSeconds(1L));
	}

	/// <inheritdoc />
	public AutoBattleOperator LoadAutoOp(ZContext context, string subDir, string opName)
	{
		return _autoBattleServices.LoadAutoOp(context, subDir, opName);
	}

	/// <inheritdoc />
	public void DispatchOpLoaded(ZContext context, AutoBattleOperator autoOp)
	{
		_autoBattleServices.DispatchOpLoaded(context, autoOp);
	}

	/// <inheritdoc />
	public void StartAutoBattle(ZContext context)
	{
		_autoBattleServices.StartAutoBattle(context);
	}

	/// <inheritdoc />
	public void ResumeAutoBattle(ZContext context)
	{
		_autoBattleServices.ResumeAutoBattle(context);
	}

	/// <inheritdoc />
	public void StopAutoBattle(ZContext context)
	{
		_autoBattleServices.StopAutoBattle(context);
	}

	/// <inheritdoc />
	public void CheckBattleState(ZContext context, Mat? screen, DateTimeOffset? screenshotTimeUtc)
	{
		context.AutoBattleContext.CheckBattleState(screen, screenshotTimeUtc);
	}

	/// <inheritdoc />
	public OperationResult HandleStoryMode(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", "文本-剧情右上角");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 文本-剧情右上角");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		if (string.Equals(config.StoryMode, CommissionAssistantStoryMode.Auto.Value, StringComparison.Ordinal))
		{
			OperationResult operationResult = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "委托助手", "中间选项区域", cropFirst: true, centerX: true), "中间选项区域");
			if (operationResult.IsSuccess)
			{
				return new OperationResult(IsSuccess: true, "点击中间选项", ResolveOptionClickInterval(config));
			}
			if (ContainsOcr(context, ocrResultList, "自动", 0.6))
			{
				return new OperationResult(IsSuccess: true, "剧情自动播放中", TimeSpan.FromSeconds(1L));
			}
			if (TryClickOcrText(context, area, ocrResultList, "菜单", 0.6))
			{
				return new OperationResult(IsSuccess: true, "尝试展开剧情菜单", TimeSpan.FromSeconds(1L));
			}
			OperationResult operationResult2 = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "委托助手", "按钮-自动"), "按钮-自动");
			return operationResult2.IsSuccess ? new OperationResult(IsSuccess: true, "尝试切换到自动模式", TimeSpan.FromMilliseconds(100L)) : new OperationResult(IsSuccess: false, "未匹配剧情按钮");
		}
		if (string.Equals(config.StoryMode, CommissionAssistantStoryMode.Skip.Value, StringComparison.Ordinal))
		{
			IReadOnlyList<OcrMatchResult> ocrResultList2 = context.OcrService.GetOcrResultList(screen, WhiteTextColorRange, area.Rect);
			if (TryClickOcrText(context, area, ocrResultList2, "跳过", 0.6))
			{
				return new OperationResult(IsSuccess: false, "需要重截图确认");
			}
			OcrMatchResult ocrMatchResult = FindFirstOcr(context, ocrResultList2, new string[2] { "菜单", "自动" }, 0.5);
			if (ocrMatchResult != null)
			{
				ControllerBase? controller = context.Controller;
				if (controller != null)
				{
					OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
					bool pcAlt = area.PcAlt;
					string gamepadKey = area.GamepadKey;
					controller.Click(position, null, pcAlt, gamepadKey);
				}
				state.MainStoryClickTime = DateTimeOffset.UtcNow;
				state.ChosenOptionLastTime = default(DateTimeOffset);
				return new OperationResult(IsSuccess: true, "点击剧情按钮 " + ocrMatchResult.Text, TimeSpan.FromMilliseconds(100L));
			}
			return new OperationResult(IsSuccess: false, "需要重截图确认");
		}
		return new OperationResult(IsSuccess: false, "未匹配剧情按钮");
	}

	/// <inheritdoc />
	public OperationResult HandleSkipStoryConfirm(ZContext context, CommissionAssistantRuntimeState state, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OperationResult operationResult = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "委托助手", "对话框确认"), "对话框确认");
		if (operationResult.IsSuccess)
		{
			state.ChosenOptionLastTime = default(DateTimeOffset);
			state.MainStoryClickTime = default(DateTimeOffset);
			return new OperationResult(IsSuccess: true, "跳过剧情", TimeSpan.FromMilliseconds(100L));
		}
		return (state.MainStoryClickTime != default(DateTimeOffset) && DateTimeOffset.UtcNow - state.MainStoryClickTime <= TimeSpan.FromSeconds(5L)) ? new OperationResult(IsSuccess: true, "等待跳过键和确认框", TimeSpan.FromMilliseconds(100L)) : new OperationResult(IsSuccess: false, "未匹配剧情按钮");
	}

	/// <inheritdoc />
	public OperationResult WaitSecondaryMenu(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		FindAreaResultEnum findAreaResultEnum = ScreenUtils.FindArea(context, screen, "委托助手", "左上角返回");
		if (1 == 0)
		{
		}
		OperationResult result = findAreaResultEnum switch
		{
			FindAreaResultEnum.True => new OperationResult(IsSuccess: true, "处于二级界面, 等待用户操作"), 
			FindAreaResultEnum.AreaNoConfig => new OperationResult(IsSuccess: false, "区域未配置 左上角返回"), 
			_ => new OperationResult(IsSuccess: false, "未处于二级界面"), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	/// <inheritdoc />
	public OperationResult CheckExploreDomainMenu(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (ScreenUtils.FindArea(context, screen, "大世界-勘域", "勘域-菜单") == FindAreaResultEnum.True)
		{
			return new OperationResult(IsSuccess: true, "在勘域中, 不自动点击鼠标");
		}
		return new OperationResult(IsSuccess: false, "未处于勘域菜单");
	}

	/// <inheritdoc />
	public OperationResult CheckBattleMenu(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (ScreenUtils.FindArea(context, screen, "委托助手", "战斗-菜单") == FindAreaResultEnum.True)
		{
			return new OperationResult(IsSuccess: true, "在空洞自由行动场景中, 不自动点击鼠标");
		}
		return new OperationResult(IsSuccess: false, "未处于战斗菜单");
	}

	/// <inheritdoc />
	public OperationResult CheckGameTutorial(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", "玩法引导");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 玩法引导");
		}
		OcrMatchResult ocrMatchResult = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect).FirstOrDefault(delegate(OcrMatchResult result)
		{
			string text = result.Text;
			return (text == "战斗引导" || text == "玩法引导") ? true : false;
		});
		return (ocrMatchResult == null) ? new OperationResult(IsSuccess: false, "未处于玩法引导") : new OperationResult(IsSuccess: true, ocrMatchResult.Text);
	}

	/// <inheritdoc />
	public OperationResult HandleKnockKnock(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		switch (ScreenUtils.FindArea(context, screen, "委托助手", "标题-短信"))
		{
		case FindAreaResultEnum.AreaNoConfig:
			return new OperationResult(IsSuccess: false, "区域未配置 标题-短信");
		default:
			return new OperationResult(IsSuccess: false, "未处于短信");
		case FindAreaResultEnum.True:
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", "区域-短信-文本框");
			if (area == null)
			{
				return new OperationResult(IsSuccess: false, "区域未配置 区域-短信-文本框");
			}
			OcrMatchResult ocrMatchResult = (from result in context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect)
				orderby result.Center.Y descending
				select result).FirstOrDefault();
			if (ocrMatchResult == null)
			{
				return new OperationResult(IsSuccess: false, "短信文本未识别");
			}
			if (ocrMatchResult.Text.Contains("以上为最新", StringComparison.Ordinal))
			{
				return ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "委托助手", "按钮-短信-关闭"), "按钮-短信-关闭");
			}
			ControllerBase? controller = context.Controller;
			if (controller != null)
			{
				OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
				bool pcAlt = area.PcAlt;
				string gamepadKey = area.GamepadKey;
				controller.Click(position, null, pcAlt, gamepadKey);
			}
			return new OperationResult(IsSuccess: true, ocrMatchResult.Text);
		}
		}
	}

	/// <inheritdoc />
	public OperationResult CheckFishing(ZContext context, Mat? screen, CommissionAssistantRuntimeState state)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		switch (ScreenUtils.FindArea(context, screen, "钓鱼", "按键-返回"))
		{
		case FindAreaResultEnum.AreaNoConfig:
			return new OperationResult(IsSuccess: false, "区域未配置 按键-返回");
		default:
			return new OperationResult(IsSuccess: false, "未处于钓鱼");
		case FindAreaResultEnum.True:
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("钓鱼", "指令文本区域");
			if (area == null)
			{
				return new OperationResult(IsSuccess: false, "区域未配置 指令文本区域");
			}
			IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
			if (!StringUtils.FindBestMatchByDifflib(context.GameTextResolver("点击按键抛竿"), ocrResultList.Select((OcrMatchResult result) => result.Text).ToArray()).HasValue)
			{
				return new OperationResult(IsSuccess: false, "未处于钓鱼");
			}
			state.FishingDone = false;
			context.Controller?.MouseMove(area.LeftTop);
			return new OperationResult(IsSuccess: true, "钓鱼");
		}
		}
	}

	/// <inheritdoc />
	public OperationResult DoDialogClick(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat? screen, bool checkCenterWords)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (ClickDialogOptions(context, config, state, screen, "右侧选项区域", "点击右方选项", out OperationResult result))
		{
			return result;
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", "中间选项区域");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 中间选项区域");
		}
		OperationResult result2;
		if (!checkCenterWords)
		{
			if (IsNonColorful(screen, area))
			{
				ControllerBase? controller = context.Controller;
				if (controller != null)
				{
					TimeSpan? pressTime = TimeSpan.FromMilliseconds(1L);
					controller.Click(null, pressTime);
				}
				return new OperationResult(IsSuccess: true, "黑屏点击", TimeSpan.FromSeconds(config.DialogClickInterval));
			}
		}
		else if (ClickDialogOptions(context, config, state, screen, "中间选项区域", "点击中间选项", out result2))
		{
			return result2;
		}
		bool flag = CheckDialog(context, screen);
		bool flag2 = IsMainStory(context, screen);
		if (flag || (flag2 && !string.Equals(config.StoryMode, CommissionAssistantStoryMode.Skip.Value, StringComparison.Ordinal)))
		{
			ControllerBase? controller2 = context.Controller;
			if (controller2 != null)
			{
				TimeSpan? pressTime = TimeSpan.FromMilliseconds(1L);
				controller2.Click(null, pressTime);
			}
			state.DialogClicked = true;
			return new OperationResult(IsSuccess: true, "对话中点击", TimeSpan.FromSeconds(config.DialogClickInterval));
		}
		if (state.DialogClicked)
		{
			ControllerBase? controller3 = context.Controller;
			if (controller3 != null)
			{
				TimeSpan? pressTime = TimeSpan.FromMilliseconds(1L);
				controller3.Click(null, pressTime);
			}
			return new OperationResult(IsSuccess: false, "点击未知画面 (对话后)");
		}
		return new OperationResult(IsSuccess: false, "未知画面");
	}

	/// <inheritdoc />
	public OperationResult HandleFishing(ZContext context, Mat? screen, CommissionAssistantRuntimeState state)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("钓鱼", "指令文本区域");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域未配置 指令文本区域");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		FishingCommandMatch fishingCommandMatch = FindFishingCommand(context, ocrResultList.Select((OcrMatchResult ocrMatchResult) => ocrMatchResult.Text).ToArray());
		string text = fishingCommandMatch?.CanonicalCommand;
		string status = fishingCommandMatch?.DisplayCommand;
		if (text != null)
		{
			state.FishingDone = false;
		}
		if (state.FishingButtonPressed != null && text != "长按")
		{
			if (!(context.Controller is IZzzControllerActions actions))
			{
				return new OperationResult(IsSuccess: false, "控制器不支持钓鱼按键");
			}
			ReleaseFishingButton(actions, state);
		}
		if (1 == 0)
		{
		}
		OperationResult result = text switch
		{
			"点击按键抛竿" => InteractForFishing(context, status), 
			"等待上鱼" => new OperationResult(IsSuccess: true, status), 
			"正确时机点击按键上鱼" => HandleFishingTiming(context, screen, status), 
			"连点" => HandleFishingRepeatedClick(context, screen, status), 
			"长按" => HandleFishingHold(context, screen, state, status), 
			_ => HandleFishingIdle(context, screen, state), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static bool ContainsOcr(ZContext context, IReadOnlyList<OcrMatchResult> results, string target, double lcsPercent)
	{
		return results.Any((OcrMatchResult result) => StringUtils.FindByLcs(context.GameTextResolver(target), result.Text, lcsPercent));
	}

	private static OcrMatchResult? FindFirstOcr(ZContext context, IReadOnlyList<OcrMatchResult> results, IReadOnlyList<string> targets, double lcsPercent)
	{
		foreach (string target in targets)
		{
			string gameTarget = context.GameTextResolver(target);
			OcrMatchResult ocrMatchResult = results.FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(gameTarget, item.Text, lcsPercent));
			if (ocrMatchResult != null)
			{
				return ocrMatchResult;
			}
		}
		return null;
	}

	private static bool TryClickOcrText(ZContext context, OneDragon.Core.Screen.ScreenArea area, IReadOnlyList<OcrMatchResult> results, string target, double lcsPercent)
	{
		OcrMatchResult ocrMatchResult = FindFirstOcr(context, results, new string[] { target }, lcsPercent);
		int result;
		if (ocrMatchResult != null)
		{
			ControllerBase? controller = context.Controller;
			if (controller == null)
			{
				result = 0;
			}
			else
			{
				OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
				bool pcAlt = area.PcAlt;
				string gamepadKey = area.GamepadKey;
				result = (controller.Click(position, null, pcAlt, gamepadKey) ? 1 : 0);
			}
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static FishingCommandMatch? FindFishingCommand(ZContext context, IReadOnlyList<string> ocrTexts)
	{
		string[] array = FishingCommands.Select(context.GameTextResolver).ToArray();
		for (int i = 0; i < FishingCommands.Length; i++)
		{
			int? num = StringUtils.FindBestMatchByDifflib(array[i], ocrTexts);
			if (num.HasValue && StringUtils.FindBestMatchByDifflib(ocrTexts[num.Value], array) == i)
			{
				return new FishingCommandMatch(FishingCommands[i], array[i]);
			}
		}
		return null;
	}

	private static void ReleaseFishingButton(IZzzControllerActions actions, CommissionAssistantRuntimeState state)
	{
		if (state.FishingButtonPressed == "a")
		{
			actions.MoveA(press: false, null, release: true);
		}
		else if (state.FishingButtonPressed == "d")
		{
			actions.MoveD(press: false, null, release: true);
		}
		state.FishingButtonPressed = null;
	}

	private static OperationResult InteractForFishing(ZContext context, string status)
	{
		if (!(context.Controller is IZzzControllerActions zzzControllerActions))
		{
			return new OperationResult(IsSuccess: false, "控制器不支持钓鱼按键");
		}
		zzzControllerActions.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
		return new OperationResult(IsSuccess: true, status);
	}

	private static OperationResult HandleFishingTiming(ZContext context, Mat screen, string status)
	{
		if (ScreenUtils.FindArea(context, screen, "钓鱼", "按键-时机上鱼") != FindAreaResultEnum.True)
		{
				// 把本轮总时长补足到 0.1 秒，以便尽快按键。
			return new OperationResult(IsSuccess: true, status, new FishingRoundPacing(TimeSpan.FromMilliseconds(100L)));
		}
		return InteractForFishing(context, status);
	}

	private static OperationResult HandleFishingRepeatedClick(ZContext context, Mat screen, string status)
	{
		if (!(context.Controller is IZzzControllerActions zzzControllerActions))
		{
			return new OperationResult(IsSuccess: false, "控制器不支持钓鱼按键");
		}
		string areaName;
		if (ScreenUtils.FindArea(context, screen, "钓鱼", "按键-左") == FindAreaResultEnum.True)
		{
			zzzControllerActions.MoveA(press: true, TimeSpan.FromMilliseconds(50L), release: true);
			areaName = "按键-强力-左";
		}
		else
		{
			zzzControllerActions.MoveD(press: true, TimeSpan.FromMilliseconds(50L), release: true);
			areaName = "按键-强力-右";
		}
		PressFishingPowerKeyIfVisible(context, screen, areaName);
			// 把本轮总时长补足到 0.1 秒，以便尽快按键。
		return new OperationResult(IsSuccess: true, status, new FishingRoundPacing(TimeSpan.FromMilliseconds(100L)));
	}

	private static OperationResult HandleFishingHold(ZContext context, Mat screen, CommissionAssistantRuntimeState state, string status)
	{
		if (!(context.Controller is IZzzControllerActions zzzControllerActions))
		{
			return new OperationResult(IsSuccess: false, "控制器不支持钓鱼按键");
		}
		if (state.FishingButtonPressed != null)
		{
			return new OperationResult(IsSuccess: true, status, new FishingRoundPacing(TimeSpan.FromMilliseconds(100L)));
		}
		string text = null;
		if (ScreenUtils.FindArea(context, screen, "钓鱼", "按键-左") == FindAreaResultEnum.True)
		{
			state.FishingButtonPressed = "a";
			zzzControllerActions.MoveA(press: true);
			text = "按键-强力-左";
		}
		if (ScreenUtils.FindArea(context, screen, "钓鱼", "按键-右") == FindAreaResultEnum.True)
		{
			state.FishingButtonPressed = "d";
			zzzControllerActions.MoveD(press: true);
			text = "按键-强力-右";
		}
		if (text != null && ScreenUtils.FindArea(context, screen, "钓鱼", text) == FindAreaResultEnum.True)
		{
			Thread.Sleep(TimeSpan.FromMilliseconds(50L));
			PressFishingPowerKey(context);
		}
			// 把本轮总时长补足到 0.1 秒。
		return new OperationResult(IsSuccess: true, status, new FishingRoundPacing(TimeSpan.FromMilliseconds(100L)));
	}

	private OperationResult HandleFishingIdle(ZContext context, Mat screen, CommissionAssistantRuntimeState state)
	{
		OperationResult operationResult = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "钓鱼", "按钮-点击空白处关闭"), "按钮-点击空白处关闭");
		if (operationResult.IsSuccess)
		{
			return operationResult;
		}
		if (ScreenUtils.FindArea(context, screen, "钓鱼", "标题-挑战结果") == FindAreaResultEnum.True)
		{
			OperationResult operationResult2 = ConvertClickResult(ScreenUtils.FindAndClickArea(context, screen, "钓鱼", "按钮-确定"), "按钮-确定");
			if (operationResult2.IsSuccess)
			{
				state.FishingDone = true;
				return operationResult2;
			}
		}
		if (state.FishingDone)
		{
			return new OperationResult(IsSuccess: true, "钓鱼结束");
		}
		OperationResult operationResult3 = _waitNormalWorldOnce(context);
		return operationResult3.IsSuccess ? new OperationResult(IsSuccess: true, "钓鱼结束") : new OperationResult(IsSuccess: false, "未识别到指令");
	}

	private static bool ClickDialogOptions(ZContext context, CommissionAssistantConfig config, CommissionAssistantRuntimeState state, Mat screen, string areaName, string status, out OperationResult result)
	{
		result = new OperationResult(IsSuccess: false, "未找到 " + areaName);
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", areaName);
		if (area == null)
		{
			result = new OperationResult(IsSuccess: false, "区域未配置 " + areaName);
			return false;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, WhiteTextColorRange, area.Rect);
		if (ocrResultList.Count == 0)
		{
			return false;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (state.ChosenOptionLastTime != default(DateTimeOffset) && utcNow < state.ChosenOptionLastTime + ChosenOptionHoldTime)
		{
			ControllerBase? controller = context.Controller;
			if (controller != null)
			{
				TimeSpan? pressTime = TimeSpan.FromMilliseconds(1L);
				controller.Click(null, pressTime);
			}
			result = new OperationResult(IsSuccess: true, status, ResolveOptionClickInterval(config));
			return true;
		}
		OcrMatchResult ocrMatchResult = null;
		foreach (OcrMatchResult item in ocrResultList)
		{
			if (state.ChosenOptionLastTime != default(DateTimeOffset) && utcNow - state.ChosenOptionLastTime > ChosenOptionMaxTime + ResolveOptionClickInterval(config) && string.Equals(item.Text, state.ChosenOption, StringComparison.Ordinal) && CheckSameOptions(state, ocrResultList.Select((OcrMatchResult item) => item.Text).ToHashSet<string>(StringComparer.Ordinal)))
			{
				continue;
			}
			if (string.Equals(config.DialogOption, CommissionAssistantDialogOption.Last.Value, StringComparison.Ordinal))
			{
				if (ocrMatchResult == null || item.Center.Y > ocrMatchResult.Center.Y)
				{
					ocrMatchResult = item;
				}
			}
			else if (ocrMatchResult == null || item.Center.Y < ocrMatchResult.Center.Y)
			{
				ocrMatchResult = item;
			}
		}
		if (ocrMatchResult == null)
		{
			return false;
		}
		if (state.ChosenOptionLastTime == default(DateTimeOffset))
		{
			state.ChosenOptionLastTime = utcNow;
		}
		state.ChosenOption = ocrMatchResult.Text;
		ControllerBase? controller2 = context.Controller;
		if (controller2 != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point? position = ocrMatchResult.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			controller2.Click(position, null, pcAlt, gamepadKey);
		}
		result = new OperationResult(IsSuccess: true, status, ResolveOptionClickInterval(config));
		return true;
	}

	private static bool CheckSameOptions(CommissionAssistantRuntimeState state, HashSet<string> currentOptions)
	{
		bool flag = state.LastDialogOptions.SetEquals(currentOptions);
		if (!flag)
		{
			state.LastDialogOptions.Clear();
			foreach (string currentOption in currentOptions)
			{
				state.LastDialogOptions.Add(currentOption);
			}
		}
		return flag;
	}

	private static bool CheckDialog(ZContext context, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", "对话框内容");
		if (area == null || !IsGrayRegion(screen, area))
		{
			return false;
		}
		return context.OcrService.GetOcrResultList(screen, null, area.Rect).Any((OcrMatchResult result) => StringUtils.WithChinese(result.Text));
	}

	private static bool IsMainStory(ZContext context, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("委托助手", "文本-剧情右上角");
		if (area == null)
		{
			return false;
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		return FindFirstOcr(context, ocrResultList, new string[3] { "菜单", "跳过", "自动" }, 0.5) != null;
	}

	private static bool IsNonColorful(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		using Mat mat = Crop(screen, area);
		if (mat.Empty())
		{
			return false;
		}
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.BGR2HSV);
		Mat[] array = Cv2.Split(mat2);
		try
		{
			using Mat mat3 = new Mat();
			Cv2.Threshold(array[1], mat3, 1.0, 255.0, ThresholdTypes.Binary);
			double num = (double)Cv2.CountNonZero(mat3) / (double)Math.Max(1, mat.Rows * mat.Cols);
			return num < 0.01;
		}
		finally
		{
			Mat[] array2 = array;
			foreach (Mat mat4 in array2)
			{
				mat4.Dispose();
			}
		}
	}

	private static bool IsGrayRegion(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		using Mat mat = Crop(screen, area);
		if (mat.Empty())
		{
			return false;
		}
		int num = 0;
		for (int i = 0; i < mat.Rows; i++)
		{
			for (int j = 0; j < mat.Cols; j++)
			{
				Vec3b vec3b = mat.At<Vec3b>(i, j);
				byte b = Math.Min(vec3b.Item0, Math.Min(vec3b.Item1, vec3b.Item2));
				byte b2 = Math.Max(vec3b.Item0, Math.Max(vec3b.Item1, vec3b.Item2));
				bool flag = b2 < 55 || b > 200;
				bool flag2 = b2 - b < 20;
				if (flag || flag2)
				{
					num++;
				}
			}
		}
		return (double)num / (double)(mat.Rows * mat.Cols) > 0.9;
	}

	private static Mat Crop(Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		int num = Math.Clamp(area.X1, 0, screen.Width);
		int num2 = Math.Clamp(area.Y1, 0, screen.Height);
		int num3 = Math.Clamp(area.X2, num, screen.Width) - num;
		int num4 = Math.Clamp(area.Y2, num2, screen.Height) - num2;
		return (num3 <= 0 || num4 <= 0) ? new Mat() : new Mat(screen, new OpenCvSharp.Rect(num, num2, num3, num4)).Clone();
	}

	private static TimeSpan ResolveOptionClickInterval(CommissionAssistantConfig config)
	{
		return TimeSpan.FromSeconds(Math.Max(0.1, config.DialogClickInterval));
	}

	private static void PressFishingPowerKeyIfVisible(ZContext context, Mat screen, string areaName)
	{
		if (ScreenUtils.FindArea(context, screen, "钓鱼", areaName) == FindAreaResultEnum.True)
		{
			PressFishingPowerKey(context);
		}
	}

	private static void PressFishingPowerKey(ZContext context)
	{
		if (context.Controller is WindowsGameController windowsGameController)
		{
			windowsGameController.PressButton("space", TimeSpan.FromMilliseconds(50L));
		}
	}

	private static OperationResult DefaultWaitNormalWorldOnce(ZContext context)
	{
		return new WaitNormalWorld(context, checkOnce: true).ExecuteAsync().GetAwaiter().GetResult();
	}

	private static OperationResult ConvertClickResult(OcrClickResultEnum result, string targetName)
	{
		if (1 == 0)
		{
		}
		OperationResult result2 = result switch
		{
			OcrClickResultEnum.OcrClickSuccess => new OperationResult(IsSuccess: true, targetName), 
			OcrClickResultEnum.AreaNoConfig => new OperationResult(IsSuccess: false, "区域未配置 " + targetName), 
			OcrClickResultEnum.OcrClickFail => new OperationResult(IsSuccess: false, "点击失败 " + targetName), 
			_ => new OperationResult(IsSuccess: false, "未找到 " + targetName), 
		};
		if (1 == 0)
		{
		}
		return result2;
	}
}

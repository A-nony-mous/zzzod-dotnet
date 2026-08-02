using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;
using ZzzOd.GameLogic.Operations.Compendium;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

internal sealed class LostVoidAppOperation : ZOperation
{
	private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1L);

	private readonly LostVoidConfig _config;

	private readonly LostVoidRunRecord _runRecord;

	private readonly ILostVoidRunner _runner;

	private readonly Func<ZContext, LostVoidModelPreparationResult> _prepareModel;

	private bool _usePriorityAgent;

	private List<Agent> _priorityAgentList = new List<Agent>();

	private string _nextRegionType = "入口";

	private CancellationToken _cancellationToken;

	public LostVoidAppOperation(
		ZContext context,
		LostVoidConfig config,
		LostVoidRunRecord runRecord,
		ILostVoidRunner runner,
		Func<ZContext, LostVoidModelPreparationResult>? prepareModel = null)
		: base(context, "迷失之地")
	{
		_config = config;
		_runRecord = runRecord;
		_runner = runner;
		_prepareModel = prepareModel ?? (ctx => ctx.LostVoid.PrepareLostVoidDetectorModel());
	}

	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
		_cancellationToken = cancellationToken;
		return Task.CompletedTask;
	}

	[OperationNode("初始化加载", IsStartNode = true)]
	private OperationRoundResult Initialize()
	{
		_usePriorityAgent = false;
		_priorityAgentList = new List<Agent>();
		_nextRegionType = "入口";
		if (_runRecord.IsFinishedByDay())
		{
			return RoundSuccess("完成通关次数");
		}
		try
		{
			base.ZContext.LostVoid.InitBeforeRun(_config.ChallengeConfig);
			LostVoidModelPreparationResult modelPreparation = _prepareModel(base.ZContext);
			if (!modelPreparation.IsSuccess)
			{
				return RoundFail(modelPreparation.ToFailureStatus());
			}
			return RoundSuccess("继续挑战");
		}
		catch (Exception ex)
		{
			base.ZContext.Logger.Error(ex, "迷失之地初始化失败: Error={Error}", ex.Message);
			return RoundFail("初始化失败: " + ex.Message);
		}
	}

	[NodeFrom("初始化加载", Status = "继续挑战")]
	[OperationNode("识别初始画面")]
	private OperationRoundResult CheckInitialScreen()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = OneSecond;
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "迷失之地-大世界", "按钮-挑战-确认", null, null, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			_nextRegionType = "挑战-限时";
			return RoundWait(operationRoundResult.Status, null, OneSecond);
		}
		string text = "迷失之地-" + _config.MissionName;
		string text2 = CheckAndUpdateCurrentScreen(base.LastScreenshot);
		bool canGoMission = string.Equals(text2, text, StringComparison.Ordinal) || (text2 != null && (base.ZContext.ScreenContext.GetScreenRoute(text2, text)?.CanGo ?? false));
		bool canGoCompendium = text2 != null && (base.ZContext.ScreenContext.GetScreenRoute(text2, "快捷手册-作战")?.CanGo ?? false);
		string text3 = ResolveInitialScreenStatus(text2, text, canGoMission, canGoCompendium);
		return string.Equals(text3, "未识别初始画面", StringComparison.Ordinal) ? RoundSuccess(text3, null, OneSecond) : RoundSuccess(text3);
	}

	internal static string ResolveInitialScreenStatus(string? currentScreen, string missionScreen, bool canGoMission, bool canGoCompendium)
	{
		if (string.Equals(currentScreen, "迷失之地-大世界", StringComparison.Ordinal))
		{
			return "迷失之地-大世界";
		}
		if (canGoMission || string.Equals(currentScreen, missionScreen, StringComparison.Ordinal))
		{
			return "可前往副本画面";
		}
		return canGoCompendium ? "可前往快捷手册" : "未识别初始画面";
	}

	[NodeFrom("识别初始画面", Status = "可前往快捷手册")]
	[NodeFrom("识别初始画面", Status = "未能识别当前画面")]
	[NodeFrom("识别初始画面", Status = "未识别初始画面")]
	[OperationNode("前往迷失之地-入口")]
	private async Task<OperationRoundResult> TransportToLostVoid()
	{
		return RoundByOperationResult(await new TransportByCompendium(base.ZContext, "作战", "周期征讨", "迷失之地").ExecuteAsync(_cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("前往迷失之地-入口")]
	[OperationNode("选择迷失之地-入口", NodeMaxRetryTimes = 20)]
	private OperationRoundResult ChooseLostVoidEntry()
	{
		OperationRoundResult result = RoundByOcrAndClick(base.LastScreenshot, "迷失之地", null, 0.5, null, OneSecond, OneSecond);
		if (result.IsSuccess)
		{
			return RoundRetry("尝试进入迷失之地-入口", null, OneSecond);
		}
		if (string.Equals(result.Status, "找不到 迷失之地", StringComparison.Ordinal))
		{
			return WaitForLostVoidEntry();
		}
		return RoundRetry(result.Status, null, OneSecond);
	}

	[NodeFrom("识别初始画面", Status = "可前往副本画面")]
	[NodeFrom("选择迷失之地-入口")]
	[OperationNode("开始前等待入口加载")]
	private OperationRoundResult WaitLostVoidEntry()
	{
		return WaitForLostVoidEntry();
	}

	private OperationRoundResult WaitForLostVoidEntry()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "迷失之地-入口", "按钮-更新弹窗-关闭", null, null, retryDelay);
		if (operationRoundResult.IsSuccess)
		{
			return RoundRetry(operationRoundResult.Status, null, TimeSpan.FromMilliseconds(500L));
		}
		string[] entryScreenNames = new string[] { "迷失之地-入口-周期", "迷失之地-入口-常规" };
		string? currentScreen = CheckAndUpdateCurrentScreen(base.LastScreenshot, entryScreenNames);
		return entryScreenNames.Contains(currentScreen, StringComparer.Ordinal) ? RoundSuccess(currentScreen) : RoundWait("等待画面加载", null, OneSecond);
	}

	[NodeFrom("开始前等待入口加载")]
	[NodeFrom("通关后处理")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone, SendImage = false, Detail = true)]
	[OperationNode("识别悬赏委托完成进度")]
	private OperationRoundResult CheckBountyCommission()
	{
		if (!_config.IsBountyCommissionMode)
		{
			if (_runRecord.IsFinishedByDay())
			{
				return RoundSuccess("完成通关次数");
			}
			return string.Equals(_config.MissionName, "矩阵行动", StringComparison.Ordinal) ? RoundSuccess("继续挑战-矩阵行动") : RoundSuccess("继续挑战");
		}
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
		}
		if (!RoundByFindArea(base.LastScreenshot, "迷失之地-入口", "按钮-悬赏委托").IsSuccess)
		{
			return RoundRetry("未识别到悬赏委托", null, TimeSpan.FromMilliseconds(500L));
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-入口", "区域-悬赏委托-进度");
		if (area == null)
		{
			return RoundFail("区域未配置 区域-悬赏委托-进度");
		}
		int num = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area.ColorRange, area.Rect).Sum((OcrMatchResult ocrMatchResult) => CountOccurrences(ocrMatchResult.Text, "8000"));
		int num2 = num;
		if (1 == 0)
		{
		}
		OperationRoundResult result = num2 switch
		{
			1 => (!string.Equals(_config.MissionName, "矩阵行动", StringComparison.Ordinal)) ? RoundSuccess("继续挑战") : RoundSuccess("继续挑战-矩阵行动"), 
			2 => MarkBountyComplete(), 
			_ => RoundRetry("悬赏委托进度未识别", null, TimeSpan.FromMilliseconds(500L)), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	[NodeFrom("识别悬赏委托完成进度", Status = "继续挑战-矩阵行动")]
	[OperationNode("矩阵行动-前往入口")]
	private OperationRoundResult MatrixGotoEntry()
	{
		TimeSpan? retryDelay = OneSecond;
		return RoundByGotoScreen(null, "迷失之地-矩阵行动-编队选择", null, null, retryDelay);
	}

	[NodeFrom("矩阵行动-前往入口")]
	[OperationNode("矩阵行动-点击预备编队")]
	private OperationRoundResult MatrixClickPresetTeam()
	{
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (challengeConfig != null && challengeConfig.ManuallyChooseAgent)
		{
			return RoundSuccess("手动选取角色");
		}
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "预备编队");
		if (area == null)
		{
			return RoundFail("矩阵行动预备编队区域未配置");
		}
		using Mat image = CvImageUtils.Crop(base.LastScreenshot, area.Rect);
		if (IsColorful(image))
		{
			base.ZContext.Logger.Information("迷失之地矩阵行动预备编队已加载");
			return RoundSuccess("预备编队已加载", null, OneSecond);
		}
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		OperationRoundResult operationRoundResult = RoundByFindAndClickArea(lastScreenshot, "迷失之地-矩阵行动-编队选择", "预备编队", null, successDelay, retryDelay);
		return operationRoundResult.IsSuccess ? RoundWait("预备编队加载中", null, TimeSpan.FromMilliseconds(500L)) : RoundRetry("点击预备编队失败", null, TimeSpan.FromMilliseconds(500L));
	}

	[NodeFrom("矩阵行动-点击预备编队")]
	[OperationNode("矩阵行动-选择预备编队", NodeMaxRetryTimes = 7)]
	private OperationRoundResult MatrixSelectTeam()
	{
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (challengeConfig == null)
		{
			return RoundFail("挑战配置未加载");
		}
		int num = ((challengeConfig.PredefinedTeamIdx != -1) ? challengeConfig.PredefinedTeamIdx : 0);
		if (num < 0 || num >= base.ZContext.TeamConfig.TeamList.Count)
		{
			return RoundFail($"选择的预备编队下标错误 {num}");
		}
		if (base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return RoundRetry("预备编队界面未就绪", null, TimeSpan.FromMilliseconds(500L));
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "编队列表");
		OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "主战编队槽");
		if (area == null || area2 == null)
		{
			return RoundFail("矩阵行动编队区域未配置");
		}
		base.ZContext.LostVoid.PredefinedTeamIdx = num;
		string name = base.ZContext.TeamConfig.TeamList[num].Name;
		double lcsPercent = ((base.NodeRetryTimes < 5) ? 0.7 : 0.5);
		string word = StringUtils.RemoveWhitespace(name);
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot);
		IReadOnlyList<string> ocrTextList = ocrResultList.Select((OcrMatchResult result) => StringUtils.RemoveWhitespace(result.Text)).ToArray();
		// 两阶段匹配：先用 difflib 固定阈值 0.6 从全部 OCR 候选里粗选唯一最优，
		// 再用 LCS 按当前重试阶段阈值（0.7/0.5）校验该候选，避免宽松阈值下误判为不相关文本。
		int? num2 = StringUtils.FindBestMatchByDifflib(word, ocrTextList, 0.6);
		OcrMatchResult ocrMatchResult = ((!num2.HasValue) || !StringUtils.FindByLcs(word, ocrTextList[num2.Value], lcsPercent)) ? null : ocrResultList[num2.Value];
		if (ocrMatchResult == null)
		{
			ScreenUtils.ScrollArea(base.ZContext, area);
			return RoundRetry("未找到" + name + ", 尝试向下滚动", null, TimeSpan.FromMilliseconds(300L));
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(300L));
		if (!base.ZContext.Controller.Click(ocrMatchResult.Center))
		{
			return RoundRetry("点击预备编队失败", null, TimeSpan.FromMilliseconds(500L));
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(500L));
		Mat mat = Screenshot();
		if (mat == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
		}
		if (!base.ZContext.OcrService.GetOcrResultList(mat, area2.ColorRange, area2.Rect).Any((OcrMatchResult result) => result.Text.Contains("主战", StringComparison.Ordinal)))
		{
			return RoundRetry("未找到主战", null, TimeSpan.FromMilliseconds(500L));
		}
		string status = ((base.NodeRetryTimes >= 5) ? "已选择配队(随机)" : "已选择配队");
		return RoundSuccess(status, null, OneSecond);
	}

	[NodeFrom("矩阵行动-点击预备编队", Status = "手动选取角色")]
	[OperationNode("矩阵行动-选择代理人")]
	private OperationRoundResult MatrixSelectAgent()
	{
		if (base.LastScreenshot != null && base.ZContext.Controller != null)
		{
			LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
			if (challengeConfig != null)
			{
				OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "代理人列表");
				OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "主战编队槽");
				if (area == null || area2 == null)
				{
					return RoundFail("手动选人区域未配置");
				}
				IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, area2.ColorRange, area2.Rect);
				if (ocrResultList.Count > 0)
				{
					base.ZContext.Controller.Click(ocrResultList[0].Center);
					Thread.Sleep(TimeSpan.FromMilliseconds(500L));
				}
				foreach (OcrMatchResult item3 in ocrResultList.Where((OcrMatchResult result) => result.Text.Contains("主战", StringComparison.Ordinal)))
				{
					base.ZContext.Controller.Click(item3.Center);
					Thread.Sleep(TimeSpan.FromMilliseconds(500L));
				}
				Dictionary<string, (int, OneDragon.Core.Abstractions.Geometry.Point)> dictionary = new Dictionary<string, (int, OneDragon.Core.Abstractions.Geometry.Point)>(StringComparer.Ordinal);
				int lastScannedPage = 0;
				for (int num = 0; num < 5; num++)
				{
					if (dictionary.Count >= challengeConfig.TeamInfo.Count)
					{
						break;
					}
					lastScannedPage = num;
					Mat mat = ((num == 0) ? base.LastScreenshot : Screenshot());
					if (mat == null)
					{
						return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
					}
					foreach (MatchResult item4 in AgentTemplateMatcher.MatchTeamAgentTemplate(base.ZContext, mat, area.Rect, challengeConfig.TeamInfo))
					{
						if (item4.Data is Agent agent && !dictionary.ContainsKey(agent.AgentId))
						{
							dictionary[agent.AgentId] = (num, item4.Center);
						}
					}
					if (dictionary.Count < challengeConfig.TeamInfo.Count)
					{
						ScreenUtils.ScrollArea(base.ZContext, area, "down", 0.75, 0.25);
						Thread.Sleep(OneSecond);
					}
				}
				if (dictionary.Count < challengeConfig.TeamInfo.Count)
				{
					for (int num2 = 0; num2 < GetMatrixAgentReturnToTopSwipeCount(lastScannedPage, foundAll: false); num2++)
					{
						ScreenUtils.ScrollArea(base.ZContext, area, "up", 0.75, 0.25);
						Thread.Sleep(TimeSpan.FromMilliseconds(200L));
					}
					return RoundRetry("未找齐代理人");
				}
				for (int num3 = 0; num3 < GetMatrixAgentReturnToTopSwipeCount(lastScannedPage, foundAll: true); num3++)
				{
					ScreenUtils.ScrollArea(base.ZContext, area, "up", 0.75, 0.25);
					Thread.Sleep(TimeSpan.FromMilliseconds(100L));
				}
				foreach (string item5 in challengeConfig.TeamInfo)
				{
					(int, OneDragon.Core.Abstractions.Geometry.Point) tuple = dictionary[item5];
					int item = tuple.Item1;
					OneDragon.Core.Abstractions.Geometry.Point item2 = tuple.Item2;
					for (int num4 = 0; num4 < item; num4++)
					{
						ScreenUtils.ScrollArea(base.ZContext, area, "down", 0.75, 0.25);
						Thread.Sleep(TimeSpan.FromMilliseconds(100L));
					}
					if (!base.ZContext.Controller.Click(item2))
					{
						return RoundRetry("点击代理人失败", null, TimeSpan.FromMilliseconds(500L));
					}
					Thread.Sleep(TimeSpan.FromMilliseconds(500L));
					for (int num5 = 0; num5 < item; num5++)
					{
						ScreenUtils.ScrollArea(base.ZContext, area, "up", 0.75, 0.25);
						Thread.Sleep(TimeSpan.FromMilliseconds(100L));
					}
				}
				return RoundSuccess();
			}
		}
		return RoundRetry("手动选人界面未就绪", null, TimeSpan.FromMilliseconds(500L));
	}

	internal static int GetMatrixAgentReturnToTopSwipeCount(int lastScannedPage, bool foundAll)
	{
		return foundAll ? Math.Clamp(lastScannedPage, 0, 5) : 5;
	}

	[NodeFrom("矩阵行动-选择预备编队")]
	[NodeFrom("矩阵行动-选择代理人")]
	[OperationNode("矩阵行动-点击协战代理人")]
	private OperationRoundResult MatrixClickSupportAgent()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = OneSecond;
		return RoundByFindAndClickArea(lastScreenshot, "迷失之地-矩阵行动-编队选择", "协战代理人", null, successDelay, retryDelay);
	}

	[NodeFrom("矩阵行动-点击协战代理人")]
	[OperationNode("矩阵行动-等待代理人列表", NodeMaxRetryTimes = 300)]
	private OperationRoundResult MatrixWaitSupportPanel()
	{
		return (base.LastScreenshot != null && IsSupportPanelVisible(
			base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot).Select((OcrMatchResult result) => result.Text),
			base.ZContext.GameTextResolver("代理人")))
			? RoundSuccess("已出现代理人列表")
			: RoundRetry("等待代理人列表", null, TimeSpan.FromMilliseconds(100L));
	}

	internal static bool IsSupportPanelVisible(IEnumerable<string> ocrTexts, string target)
	{
		string[] texts = ocrTexts.ToArray();
		return StringUtils.FindBestMatchByDifflib(target, texts, 0.5).HasValue
			|| texts.Any((string text) => StringUtils.FindByLcs(target, text, 0.6));
	}

	[NodeFrom("矩阵行动-等待代理人列表")]
	[OperationNode("矩阵行动-选择协战代理人")]
	private OperationRoundResult MatrixSelectSupportAgent()
	{
		if (base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "代理人列表");
		OneDragon.Core.Screen.ScreenArea area2 = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "协战编队槽");
		OneDragon.Core.Screen.ScreenArea area3 = base.ZContext.ScreenContext.GetArea("迷失之地-矩阵行动-编队选择", "协战代理人属性");
		if (area == null || area2 == null || area3 == null)
		{
			return RoundFail("协战代理人区域未配置");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot, null, area.Rect);
		OcrMatchResult ocrMatchResult = ocrResultList.FirstOrDefault((OcrMatchResult result) => string.Equals(result.Text, "up", StringComparison.OrdinalIgnoreCase)) ?? ocrResultList.FirstOrDefault((OcrMatchResult result) => string.Equals(result.Text, "60", StringComparison.OrdinalIgnoreCase));
		if (ocrMatchResult == null || !base.ZContext.Controller.Click(ocrMatchResult.Center))
		{
			return RoundRetry("未找到协战代理人", null, TimeSpan.FromMilliseconds(500L));
		}
		Thread.Sleep(TimeSpan.FromMilliseconds(500L));
		Mat mat = Screenshot();
		if (mat == null)
		{
			return RoundRetry("未获取截图", null, TimeSpan.FromMilliseconds(500L));
		}
		if (!base.ZContext.OcrService.GetOcrResultList(mat, null, area2.Rect).Any((OcrMatchResult result) => result.Text.Contains("协战", StringComparison.Ordinal)))
		{
			return RoundRetry("未找到协战", null, TimeSpan.FromMilliseconds(500L));
		}
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (challengeConfig != null)
		{
			challengeConfig.ClearArtifactPriorityInBattle();
			foreach (OcrMatchResult ocrResult in base.ZContext.OcrService.GetOcrResultList(mat, null, area3.Rect))
			{
				string text = ocrResult.Text.Replace('【', '[');
				string text2 = ((text.StartsWith("[", StringComparison.Ordinal) && text.Length >= 3) ? text.Substring(1, 2) : text);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					challengeConfig.ArtifactPriorityInBattle.Add(text2);
					base.ZContext.Logger.Information("添加协战代理人属性武备至第一优先级: [{Priority}]", text2);
				}
			}
		}
		return RoundSuccess("已选择协战代理人");
	}

	[NodeFrom("矩阵行动-选择协战代理人")]
	[OperationNode("矩阵行动-开始挑战")]
	private OperationRoundResult MatrixStartChallenge()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = OneSecond;
		return RoundByFindAndClickArea(lastScreenshot, "迷失之地-矩阵行动-编队选择", "按钮-开始挑战", null, successDelay, retryDelay);
	}

	[NodeFrom("识别悬赏委托完成进度", Status = "继续挑战")]
	[OperationNode("前往副本画面")]
	private OperationRoundResult GotoMissionScreen()
	{
		string screenName = "迷失之地-" + _config.MissionName;
		TimeSpan? retryDelay = OneSecond;
		return RoundByGotoScreen(null, screenName, null, null, retryDelay);
	}

	[NodeFrom("前往副本画面")]
	[OperationNode("副本画面识别")]
	private OperationRoundResult CheckForMission()
	{
		if (!string.Equals(_config.MissionName, "特遣调查", StringComparison.Ordinal))
		{
			return RoundSuccess();
		}
		if (base.LastScreenshot == null)
		{
			return RoundRetry("未获取截图", null, OneSecond);
		}
		OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-特遣调查", "区域-代理人头像");
		if (area == null)
		{
			return RoundFail("区域未配置 区域-代理人头像");
		}
		_priorityAgentList = (from match in AgentTemplateMatcher.MatchTeamAgentTemplate(base.ZContext, base.LastScreenshot, area.Rect)
			orderby match.LeftTop.X
			select match.Data).OfType<Agent>().ToList();
		base.ZContext.Logger.Information("迷失之地特遣调查识别UP代理人: {Agents}", (_priorityAgentList.Count == 0) ? "无" : string.Join(", ", _priorityAgentList.Select((Agent agent) => agent.AgentName)));
		return (_priorityAgentList.Count > 0) ? RoundSuccess() : RoundRetry("未识别UP代理人", null, OneSecond);
	}

	[NodeFrom("副本画面识别")]
	[OperationNode("打开调查战略列表")]
	private OperationRoundResult OpenStrategyList()
	{
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = OneSecond;
		return RoundByClickArea("迷失之地-战线肃清", "按钮-调查战略", clickLeftTop: false, null, successDelay, retryDelay);
	}

	[NodeFrom("打开调查战略列表")]
	[OperationNode("选择调查战略")]
	private OperationRoundResult ChooseStrategy()
	{
		if (base.LastScreenshot == null || base.ZContext.Controller == null)
		{
			return RoundRetry("未获取截图", null, OneSecond);
		}
		string text = CheckAndUpdateCurrentScreen(base.LastScreenshot, new string[2] { "迷失之地-战线肃清", "迷失之地-特遣调查" });
		if (text != null)
		{
			return RoundSuccess(text);
		}
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (challengeConfig == null)
		{
			return RoundFail("挑战配置未加载");
		}
		base.ZContext.Logger.Information("迷失之地调查战略决策: ChaseNew={ChaseNew}, Config={Config}, Strategy={Strategy}", challengeConfig.ChaseNewMode, base.ZContext.LostVoid.ChallengeConfigName, challengeConfig.InvestigationStrategy);
		if (challengeConfig.ChaseNewMode)
		{
			OneDragon.Core.Screen.ScreenArea area = base.ZContext.ScreenContext.GetArea("迷失之地-调查战略选择", "战略等级");
			if (area == null)
			{
				return RoundFail("区域未配置 战略等级");
			}
			OneDragon.Core.Abstractions.Geometry.Point? point = FindChaseNewNoLevelTarget(base.LastScreenshot, area.Rect);
			if (point.HasValue)
			{
				return ClickStrategyAndConfirm(point.Value);
			}
			for (int i = 0; i < 3; i++)
			{
				IReadOnlyList<OpenCvSharp.Point[]> readOnlyList = FindChaseNewLevelFrames(base.LastScreenshot, area.Rect);
				if (readOnlyList.Count > 0)
				{
					IReadOnlyList<OpenCvSharp.Point[]> digitContours = FindChaseNewLevelDigits(base.LastScreenshot, area.Rect);
					OneDragon.Core.Abstractions.Geometry.Point? point2 = FindChaseNewFrameWithoutDigitTarget(readOnlyList, digitContours, area.Rect);
					if (point2.HasValue)
					{
						return ClickStrategyAndConfirm(point2.Value);
					}
					SwipeStrategyList();
					Screenshot();
				}
				else
				{
					SwipeStrategyList();
				}
			}
			IReadOnlyList<OpenCvSharp.Point[]> readOnlyList2 = FindChaseNewLevelFrames(base.LastScreenshot, area.Rect);
			if (readOnlyList2.Count > 0 && TryGetContourCenter(readOnlyList2[0], area.Rect, out var target))
			{
				return ClickStrategyAndConfirm(target);
			}
			return RoundFail("追新模式失败：未找到任何可选择的调查战略");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot);
		string word = base.ZContext.GameTextResolver(challengeConfig.InvestigationStrategy);
		int? num = StringUtils.FindBestMatchByDifflib(word, ocrResultList.Select((OcrMatchResult result) => result.Text).ToArray());
		if (num.HasValue)
		{
			return ClickStrategyAndConfirm(ocrResultList[num.Value].Center);
		}
		bool flag = IsStrategyAfterOcr(challengeConfig.InvestigationStrategy, ocrResultList.Select((OcrMatchResult result) => result.Text));
		OneDragon.Core.Abstractions.Geometry.Point point3 = new OneDragon.Core.Abstractions.Geometry.Point(base.ZContext.Controller.StandardWidth / 2, base.ZContext.Controller.StandardHeight / 2);
		OneDragon.Core.Abstractions.Geometry.Point end = point3 + new OneDragon.Core.Abstractions.Geometry.Point(flag ? (-800) : 800, 0);
		base.ZContext.Controller.DragTo(end, point3);
		return RoundRetry("未识别到目标调查战略", null, OneSecond);
	}

	[NodeFrom("选择调查战略")]
	[OperationNode("选择周期增益")]
	private OperationRoundResult ChooseBuff()
	{
		OperationRoundResult result;
		if (!string.Equals(_config.MissionName, "特遣调查", StringComparison.Ordinal))
		{
			string areaName = "周期增益-" + base.ZContext.LostVoid.ChallengeConfig?.PeriodBuffNo;
			TimeSpan? successDelay = OneSecond;
			TimeSpan? retryDelay = OneSecond;
			result = RoundByClickArea("迷失之地-战线肃清", areaName, clickLeftTop: false, null, successDelay, retryDelay);
		}
		else
		{
			result = RoundSuccess("无需选择");
		}
		return result;
	}

	[NodeFrom("选择周期增益")]
	[OperationNode("下一步")]
	private OperationRoundResult ClickNext()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = OneSecond;
		IReadOnlyList<(string, string)> untilFindAll = new (string, string)[] { ("通用-出战", "按钮-出战") };
		return RoundByFindAndClickArea(lastScreenshot, "通用-出战", "按钮-下一步", null, successDelay, retryDelay, cropFirst: true, centerX: false, untilFindAll);
	}

	[NodeFrom("下一步")]
	[OperationNode("检查预备编队")]
	private OperationRoundResult CheckPredefinedTeam()
	{
		_usePriorityAgent = false;
		LostVoidChallengeConfig challengeConfig = base.ZContext.LostVoid.ChallengeConfig;
		if (challengeConfig == null)
		{
			return RoundFail("挑战配置未加载");
		}
		if (string.Equals(_config.MissionName, "特遣调查", StringComparison.Ordinal) && challengeConfig.ChooseTeamByPriority && !_runRecord.CompleteTaskForceWithUp)
		{
			int num = challengeConfig.PredefinedTeamIdx;
			int num2 = 0;
			int index;
			for (index = 0; index < base.ZContext.TeamConfig.TeamList.Count; index++)
			{
				int num3 = _priorityAgentList.Count((Agent agent) => base.ZContext.TeamConfig.TeamList[index].AgentIdList.Contains<string>(agent.AgentId, StringComparer.Ordinal));
				if (num3 > num2)
				{
					num2 = num3;
					num = index;
				}
			}
			if (num != -1)
			{
				base.ZContext.LostVoid.PredefinedTeamIdx = num;
				_usePriorityAgent = true;
				return RoundSuccess("需选择预备编队");
			}
		}
		if (challengeConfig.PredefinedTeamIdx != -1)
		{
			base.ZContext.LostVoid.PredefinedTeamIdx = challengeConfig.PredefinedTeamIdx;
			return RoundSuccess("需选择预备编队");
		}
		return RoundSuccess("无需选择预备编队");
	}

	[NodeFrom("检查预备编队", Status = "需选择预备编队")]
	[OperationNode("选择预备编队")]
	private async Task<OperationRoundResult> ChoosePredefinedTeam()
	{
		return RoundByOperationResult(await new ChoosePredefinedTeam(base.ZContext, new int[] { base.ZContext.LostVoid.PredefinedTeamIdx }).ExecuteAsync(_cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("检查预备编队", Status = "无需选择预备编队")]
	[NodeFrom("选择预备编队")]
	[OperationNode("出战")]
	private async Task<OperationRoundResult> Deploy()
	{
		_nextRegionType = "入口";
		return RoundByOperationResult(await new Deploy(base.ZContext).ExecuteAsync(_cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	[NodeFrom("识别初始画面", Status = "迷失之地-大世界")]
	[NodeFrom("出战")]
	[NodeFrom("矩阵行动-开始挑战")]
	[OperationNode("加载自动战斗配置")]
	private OperationRoundResult LoadAutoOp()
	{
		try
		{
			base.ZContext.AutoBattleContext.InitAutoOp(base.ZContext.LostVoid.GetAutoOpName());
			return RoundSuccess();
		}
		catch (Exception)
		{
			return RoundFail("加载自动战斗配置失败");
		}
	}

	[NodeFrom("加载自动战斗配置")]
	[NodeFrom("层间移动")]
	[OperationNode("层间移动")]
	private async Task<OperationRoundResult> RunLevel()
	{
		base.ZContext.Logger.Information("迷失之地推测楼层类型: {RegionType}", _nextRegionType);
		base.ZContext.DebugDataPublisher.PublishBusinessState(
			"迷失之地-下一层",
			_nextRegionType,
			nameof(LostVoidAppOperation),
			60d);
		OperationResult result = await _runner.RunLevelAsync(base.ZContext, _config, _runRecord, _nextRegionType, _cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess && string.Equals(result.Status, "进入下层", StringComparison.Ordinal))
		{
			_nextRegionType = (result.Data as string) ?? "入口";
			base.ZContext.LostVoid.HadInteractedOpheliaOnCurrentLevel = false;
		}
		else if (result.IsSuccess && string.Equals(result.Status, "通关", StringComparison.Ordinal))
		{
			_nextRegionType = "入口";
			base.ZContext.LostVoid.HadInteractedOpheliaOnCurrentLevel = false;
		}
		return RoundByOperationResult(result);
	}

	[NodeFrom("层间移动", Status = "通关")]
	[OperationNode("通关后处理")]
	private OperationRoundResult AfterComplete()
	{
		OperationRoundResult operationRoundResult = WaitForLostVoidEntry();
		if (!operationRoundResult.IsSuccess)
		{
			return operationRoundResult;
		}
		_runRecord.AddCompleteTimes();
		if (_usePriorityAgent)
		{
			_runRecord.CompleteTaskForceWithUp = true;
		}
		return RoundSuccess();
	}

	[NodeFrom("识别悬赏委托完成进度", Status = "完成通关次数")]
	[OperationNode("打开悬赏委托")]
	private OperationRoundResult OpenRewardList()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = OneSecond;
		IReadOnlyList<(string, string)> untilNotFindAll = new (string, string)[] { ("迷失之地-入口", "按钮-悬赏委托") };
		return RoundByFindAndClickArea(lastScreenshot, "迷失之地-入口", "按钮-悬赏委托", null, successDelay, retryDelay, cropFirst: true, centerX: false, null, untilNotFindAll);
	}

	[NodeFrom("打开悬赏委托")]
	[OperationNodeNotify(OperationNodeNotifyTiming.CurrentDone)]
	[OperationNode("全部领取", NodeMaxRetryTimes = 2)]
	private OperationRoundResult ClaimAll()
	{
		Mat? lastScreenshot = base.LastScreenshot;
		TimeSpan? successDelay = OneSecond;
		TimeSpan? retryDelay = TimeSpan.FromMilliseconds(500L);
		return RoundByFindAndClickArea(lastScreenshot, "迷失之地-入口", "按钮-悬赏委托-全部领取", null, successDelay, retryDelay);
	}

	[NodeFrom("全部领取")]
	[NodeFrom("全部领取", Success = false)]
	[OperationNode("完成后返回")]
	private async Task<OperationRoundResult> BackAtLast()
	{
		return RoundByOperationResult(await new BackToNormalWorld(base.ZContext).ExecuteAsync(_cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}

	private OperationRoundResult MarkBountyComplete()
	{
		_runRecord.BountyCommissionComplete = true;
		return RoundSuccess("完成通关次数");
	}

	private OperationRoundResult ClickStrategyAndConfirm(OneDragon.Core.Abstractions.Geometry.Point target)
	{
		base.ZContext.Controller.Click(target);
		Thread.Sleep(OneSecond);
		IReadOnlyList<OcrMatchResult> ocrResultList = base.ZContext.OcrService.GetOcrResultList(base.LastScreenshot);
		int? num = StringUtils.FindBestMatchByDifflib(base.ZContext.GameTextResolver("确定"), ocrResultList.Select((OcrMatchResult result) => result.Text).ToArray());
		if (!num.HasValue)
		{
			return RoundRetry("未识别到确定按钮", null, OneSecond);
		}
		base.ZContext.Controller.Click(ocrResultList[num.Value].Center);
		Thread.Sleep(OneSecond);
		return RoundWait("确定", null, TimeSpan.Zero);
	}

	private void SwipeStrategyList()
	{
		OneDragon.Core.Abstractions.Geometry.Point point = new OneDragon.Core.Abstractions.Geometry.Point(base.ZContext.Controller.StandardWidth / 2, (int)((double)base.ZContext.Controller.StandardHeight / 2.5));
		base.ZContext.Controller.DragTo(point + new OneDragon.Core.Abstractions.Geometry.Point(-800, 0), point);
		Thread.Sleep(OneSecond);
	}

	internal static OneDragon.Core.Abstractions.Geometry.Point? FindChaseNewNoLevelTarget(Mat screen, OneDragon.Core.Abstractions.Geometry.Rect area)
	{
		IReadOnlyList<OpenCvSharp.Point[]> readOnlyList = FindChaseNewContours(screen, area, new int[3] { 179, 59, 63 }, new int[3] { 15, 51, 63 }, 3, 1, 3, 1).Where(delegate(OpenCvSharp.Point[] contour)
		{
			double num = Cv2.ContourArea(contour);
			return num >= 3000.0 && num <= 4000.0;
		}).ToArray();
		OneDragon.Core.Abstractions.Geometry.Point target;
		return (readOnlyList.Count > 0 && TryGetContourCenter(readOnlyList[0], area, out target)) ? new OneDragon.Core.Abstractions.Geometry.Point?(target) : ((OneDragon.Core.Abstractions.Geometry.Point?)null);
	}

	internal static OneDragon.Core.Abstractions.Geometry.Point? FindChaseNewFrameWithoutDigitTarget(IReadOnlyList<OpenCvSharp.Point[]> frameContours, IReadOnlyList<OpenCvSharp.Point[]> digitContours, OneDragon.Core.Abstractions.Geometry.Rect area)
	{
		foreach (OpenCvSharp.Point[] frameContour in frameContours)
		{
			OpenCvSharp.Rect frameRect = Cv2.BoundingRect(frameContour);
			if (!digitContours.Any(delegate(OpenCvSharp.Point[] digitContour)
			{
				Moments moments = Cv2.Moments(digitContour);
				if (Math.Abs(moments.M00) < double.Epsilon)
				{
					return false;
				}
				int num = (int)(moments.M10 / moments.M00);
				int num2 = (int)(moments.M01 / moments.M00);
				return frameRect.X < num && num < frameRect.X + frameRect.Width && frameRect.Y < num2 && num2 < frameRect.Y + frameRect.Height;
			}) && TryGetContourCenter(frameContour, area, out var target))
			{
				return target;
			}
		}
		return null;
	}

	internal static IReadOnlyList<OpenCvSharp.Point[]> FindChaseNewLevelFrames(Mat screen, OneDragon.Core.Abstractions.Geometry.Rect area)
	{
		return FindChaseNewContours(screen, area, new int[3] { 75, 5, 150 }, new int[3] { 75, 5, 255 }, 3, 1, 5, 1).Where(delegate(OpenCvSharp.Point[] contour)
		{
			OpenCvSharp.Rect rect = Cv2.BoundingRect(contour);
			if (rect.Height == 0)
			{
				return false;
			}
			double num = (double)rect.Width / (double)rect.Height;
			double num2 = Cv2.ContourArea(contour);
			return num >= 0.9 && num <= 1.1 && num2 >= 2000.0 && num2 <= 20000.0;
		}).ToArray();
	}

	internal static IReadOnlyList<OpenCvSharp.Point[]> FindChaseNewLevelDigits(Mat screen, OneDragon.Core.Abstractions.Geometry.Rect area)
	{
		return FindChaseNewContours(screen, area, new int[3] { 0, 0, 200 }, new int[3] { 0, 0, 50 }, 4, 1, 5, 3).Where(delegate(OpenCvSharp.Point[] contour)
		{
			double num = Cv2.ContourArea(contour);
			return num >= 900.0 && num <= 10000.0;
		}).ToArray();
	}

	private static IReadOnlyList<OpenCvSharp.Point[]> FindChaseNewContours(Mat screen, OneDragon.Core.Abstractions.Geometry.Rect area, IReadOnlyList<int> hsvColor, IReadOnlyList<int> hsvDiff, int erodeKernelSize, int erodeIterations, int dilateKernelSize, int dilateIterations)
	{
		if (screen.Empty() || area.Width <= 0 || area.Height <= 0)
		{
			return Array.Empty<OpenCvSharp.Point[]>();
		}
		int num = Math.Clamp(area.X1, 0, screen.Width);
		int num2 = Math.Clamp(area.Y1, 0, screen.Height);
		int num3 = Math.Clamp(area.X2, 0, screen.Width);
		int num4 = Math.Clamp(area.Y2, 0, screen.Height);
		if (num3 <= num || num4 <= num2)
		{
			return Array.Empty<OpenCvSharp.Point[]>();
		}
		using Mat mat = new Mat(screen, new OpenCvSharp.Rect(num, num2, num3 - num, num4 - num2));
		using Mat mat2 = new Mat();
		Cv2.CvtColor(mat, mat2, ColorConversionCodes.BGR2HSV);
		using Mat mat3 = FilterByHsvWithCircularHue(mat2, hsvColor, hsvDiff);
		using Mat mat4 = new Mat();
		using Mat mat5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(erodeKernelSize, erodeKernelSize));
		InputArray src = mat3;
		OutputArray dst = mat4;
		InputArray element = mat5;
		int iterations = erodeIterations;
		Cv2.Erode(src, dst, element, null, iterations);
		using Mat mat6 = new Mat();
		using Mat mat7 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilateKernelSize, dilateKernelSize));
		InputArray src2 = mat4;
		OutputArray dst2 = mat6;
		InputArray element2 = mat7;
		iterations = dilateIterations;
		Cv2.Dilate(src2, dst2, element2, null, iterations);
		Cv2.FindContours(mat6, out OpenCvSharp.Point[][] contours, out HierarchyIndex[] _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
		return contours;
	}

	private static Mat FilterByHsvWithCircularHue(Mat hsv, IReadOnlyList<int> hsvColor, IReadOnlyList<int> hsvDiff)
	{
		int num = Math.Clamp(hsvColor[1] - hsvDiff[1], 0, 255);
		int num2 = Math.Clamp(hsvColor[1] + hsvDiff[1], 0, 255);
		int num3 = Math.Clamp(hsvColor[2] - hsvDiff[2], 0, 255);
		int num4 = Math.Clamp(hsvColor[2] + hsvDiff[2], 0, 255);
		int num5 = hsvColor[0] - hsvDiff[0];
		int num6 = hsvColor[0] + hsvDiff[0];
		if (num5 < 0)
		{
			using (Mat mat = new Mat())
			{
				using Mat mat2 = new Mat();
				Cv2.InRange(hsv, new Scalar(num5 + 180, num, num3), new Scalar(179.0, num2, num4), mat);
				Cv2.InRange(hsv, new Scalar(0.0, num, num3), new Scalar(num6, num2, num4), mat2);
				Mat mat3 = new Mat();
				Cv2.BitwiseOr(mat, mat2, mat3);
				return mat3;
			}
		}
		if (num6 > 179)
		{
			using (Mat mat4 = new Mat())
			{
				using Mat mat5 = new Mat();
				Cv2.InRange(hsv, new Scalar(num5, num, num3), new Scalar(179.0, num2, num4), mat4);
				Cv2.InRange(hsv, new Scalar(0.0, num, num3), new Scalar(num6 - 180, num2, num4), mat5);
				Mat mat6 = new Mat();
				Cv2.BitwiseOr(mat4, mat5, mat6);
				return mat6;
			}
		}
		Mat mat7 = new Mat();
		Cv2.InRange(hsv, new Scalar(num5, num, num3), new Scalar(num6, num2, num4), mat7);
		return mat7;
	}

	private static bool TryGetContourCenter(OpenCvSharp.Point[] contour, OneDragon.Core.Abstractions.Geometry.Rect area, out OneDragon.Core.Abstractions.Geometry.Point target)
	{
		Moments moments = Cv2.Moments(contour);
		if (Math.Abs(moments.M00) < double.Epsilon)
		{
			target = default(OneDragon.Core.Abstractions.Geometry.Point);
			return false;
		}
		target = new OneDragon.Core.Abstractions.Geometry.Point((int)(moments.M10 / moments.M00) + area.X1, (int)(moments.M01 / moments.M00) + area.Y1);
		return true;
	}

	private static int CountOccurrences(string value, string target)
	{
		int num = 0;
		int startIndex = 0;
		while ((startIndex = value.IndexOf(target, startIndex, StringComparison.Ordinal)) >= 0)
		{
			num++;
			startIndex += target.Length;
		}
		return num;
	}

	private static bool IsColorful(Mat image, double saturationThreshold = 30.0, double colorRatioThreshold = 0.1)
	{
		if (image.Empty() || image.Channels() != 3)
		{
			return false;
		}
		using Mat mat = new Mat();
		Cv2.CvtColor(image, mat, ColorConversionCodes.BGR2HSV);
		Mat[] array = Cv2.Split(mat);
		try
		{
			using Mat mat2 = new Mat();
			Cv2.Threshold(array[1], mat2, saturationThreshold, 255.0, ThresholdTypes.Binary);
			double val = Cv2.Mean(array[1]).Val0;
			double num = ((mat2.Total() == 0L) ? 0.0 : ((double)Cv2.CountNonZero(mat2) / (double)mat2.Total()));
			return val > saturationThreshold && num > colorRatioThreshold;
		}
		finally
		{
			Mat[] array2 = array;
			foreach (Mat mat3 in array2)
			{
				mat3.Dispose();
			}
		}
	}

	private bool IsStrategyAfterOcr(string target, IEnumerable<string> ocrTexts)
	{
		return IsStrategyAfterOcr(
			target,
			base.ZContext.LostVoid.InvestigationStrategyList.Select((LostVoidInvestigationStrategy item) => item.StrategyName),
			ocrTexts,
			base.ZContext.GameTextResolver);
	}

	internal static bool IsStrategyAfterOcr(string target, IEnumerable<string> orderedStrategies, IEnumerable<string> ocrTexts, Func<string, string> gameTextResolver)
	{
		List<string> list = orderedStrategies.ToList();
		string[] texts = ocrTexts.ToArray();
		int num = list.FindIndex((string item) => string.Equals(item, target, StringComparison.Ordinal));
		if (num < 0)
		{
			return false;
		}
		bool result = false;
		foreach (string item in list)
		{
			if (string.Equals(item, target, StringComparison.Ordinal))
			{
				return result;
			}
			if (StringUtils.FindBestMatchByDifflib(gameTextResolver(item), texts, 0.6).HasValue)
			{
				result = true;
			}
		}
		return false;
	}
}

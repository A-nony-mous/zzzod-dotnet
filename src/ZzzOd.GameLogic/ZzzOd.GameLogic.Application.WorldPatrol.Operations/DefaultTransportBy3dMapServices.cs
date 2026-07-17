using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Controller;
using OneDragon.Core.Matcher;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.E2E;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Application.WorldPatrol.Operations;

/// <summary>
/// 默认 3D 地图传送流程服务。
/// </summary>
public sealed class DefaultTransportBy3dMapServices : ITransportBy3dMapServices
{
	private sealed class TransportPointSearchIconEvidence
	{
		public int ClickX { get; set; }

		public int ClickY { get; set; }

		public double MatchConfidence { get; set; }

		public bool GoButtonFound { get; set; }

		public string? RecognizedName { get; set; }

		public string? MatchedName { get; set; }

		public string? FailureReason { get; set; }
	}

	private readonly TimeSpan _iconClickDelay;

	private readonly TimeSpan _clickPreDelay;

	private readonly Func<int, int> _nextRandomIndex;

	private readonly Action<TimeSpan> _sleep;

	private WorldPatrolLargeMap? _largeMap;

	private WorldPatrolLargeMapIcon? _targetIcon;

	private IReadOnlyList<string> _iconWordList = Array.Empty<string>();

	private OneDragon.Core.Screen.ScreenArea? _mapArea;

	/// <summary>
	/// 初始化默认服务。
	/// </summary>
	public DefaultTransportBy3dMapServices(TimeSpan? iconClickDelay = null, Func<int, int>? nextRandomIndex = null, TimeSpan? clickPreDelay = null, Action<TimeSpan>? sleep = null)
	{
		_iconClickDelay = iconClickDelay ?? TimeSpan.FromSeconds(1L);
		_clickPreDelay = clickPreDelay ?? TimeSpan.FromMilliseconds(300L);
		_nextRandomIndex = nextRandomIndex ?? new Func<int, int>(Random.Shared.Next);
		_sleep = sleep ?? new Action<TimeSpan>(Thread.Sleep);
	}

	/// <inheritdoc />
	public string? CheckCurrentScreen(ZContext context, Mat? screen, IReadOnlyList<string> screenNameList)
	{
		if (screen == null)
		{
			return null;
		}
		string matchScreenName = ScreenUtils.GetMatchScreenName(context, screen, screenNameList);
		if (matchScreenName != null)
		{
			context.ScreenContext.UpdateCurrentScreenName(matchScreenName);
		}
		return matchScreenName;
	}

	/// <inheritdoc />
	public Task<OperationResult> BackToNormalWorldAsync(ZContext context, CancellationToken cancellationToken)
	{
		return new BackToNormalWorld(context).ExecuteAsync(cancellationToken);
	}

	/// <inheritdoc />
	public bool Open3dMap(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return false;
		}
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId() + "-transport-open-3d-map");
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", screen) : null);
		string beforeScreenName = (isEnabled ? ScreenUtils.GetMatchScreenName(context, screen) : null);
		WorldPatrolMiniMapSnapshot worldPatrolMiniMapSnapshot = context.WorldPatrolService.CutMiniMap(context, screen);
		if (!worldPatrolMiniMapSnapshot.PlayMaskFound)
		{
			context.Logger.Warning("未发现地图");
			if (isEnabled)
			{
				WriteOpen3dMapEvidence(context, fileStem, beforeScreenshotPath, beforeScreenName, worldPatrolMiniMapSnapshot.PlayMaskFound, null, null, actionClicked: false, "mini_map_player_mask_missing");
			}
			return false;
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("大世界", "小地图");
		bool flag = false;
		if (area != null && context.Controller != null)
		{
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			flag = controller.Click(position, null, pcAlt, gamepadKey);
		}
		if (flag)
		{
			context.Logger.Information("点击打开3D地图");
		}
		if (isEnabled)
		{
			using Mat image = context.Controller?.Screenshot().Screen;
			string afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", image);
			WriteOpen3dMapEvidence(context, fileStem, beforeScreenshotPath, beforeScreenName, worldPatrolMiniMapSnapshot.PlayMaskFound, CreateAreaClickTargetSummary(area), afterScreenshotPath, flag, flag ? null : "click_area_failed");
		}
		return worldPatrolMiniMapSnapshot.PlayMaskFound;
	}

	/// <inheritdoc />
	public OperationResult ChooseArea(ZContext context, Mat? screen, string areaName, WorldPatrolArea targetArea)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "区域-区域列表");
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域配置不存在 区域-区域列表");
		}
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		string targetText = GameTextTranslator.Translate(context.Environment, context.GameAccountConfig.GameLanguage, areaName);
		OcrMatchResult ocrMatchResult = FindBestOcrMatch(targetText, ocrResultList, 0.8);
		if (ocrMatchResult != null)
		{
			bool flag = false;
			if (context.Controller != null)
			{
				flag = context.Controller.Click(ocrMatchResult.Center);
			}
			context.Logger.Information("选择3D地图区域 {AreaName}", areaName);
			return new OperationResult(IsSuccess: true, areaName);
		}
		if (context.Controller == null)
		{
			return new OperationResult(IsSuccess: false, "找不到 " + areaName);
		}
		bool flag2 = IsTargetAfterOcrList(areaName, context.WorldPatrolService.AreaList.Select((WorldPatrolArea item) => item.AreaName).ToArray(), ocrResultList.Select((OcrMatchResult item) => item.Text).ToArray(), (string item) => GameTextTranslator.Translate(context.Environment, context.GameAccountConfig.GameLanguage, item), 0.8);
		OneDragon.Core.Abstractions.Geometry.Point center = area.Center;
		OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(0, 400 * ((!flag2) ? 1 : (-1)));
		context.Controller.DragTo(end, center);
		context.Logger.Information("3D地图区域列表未找到 {AreaName}，向{Direction}滚动", areaName, flag2 ? "下" : "上");
		return new OperationResult(IsSuccess: false, "找不到 " + areaName);
	}

	/// <inheritdoc />
	public OperationResult ExpandSubArea(ZContext context, Mat? screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "按钮-当前子区域");
		bool flag = false;
		if (area != null && context.Controller != null)
		{
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			flag = controller.Click(position, null, pcAlt, gamepadKey);
		}
		return new OperationResult(flag, flag ? "展开子区域列表" : "点击失败 按钮-当前子区域");
	}

	/// <inheritdoc />
	public OperationResult ChooseSubArea(ZContext context, Mat? screen, string areaName)
	{
		return ClickTextInArea(context, screen, areaName, context.ScreenContext.GetArea("3D地图", "区域-子区域列表"), 0.5);
	}

	/// <inheritdoc />
	public OperationResult OpenFilter(ZContext context, Mat? screen)
	{
		if (screen != null && ScreenUtils.FindArea(context, screen, "3D地图", "标题-标识点筛选") == FindAreaResultEnum.True)
		{
			return new OperationResult(IsSuccess: true, "标题-标识点筛选");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "按钮-筛选");
		bool flag = false;
		if (area != null && context.Controller != null)
		{
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			flag = controller.Click(position, null, pcAlt, gamepadKey);
		}
		return new OperationResult(flag, flag ? "按钮-筛选" : "点击失败 按钮-筛选");
	}

	/// <inheritdoc />
	public OperationResult ChooseFilter(ZContext context, Mat? screen, string targetWord)
	{
		return ClickTextInArea(context, screen, targetWord, context.ScreenContext.GetArea("3D地图", "区域-筛选选项"), 0.5);
	}

	/// <inheritdoc />
	public OperationResult CloseFilter(ZContext context, Mat? screen)
	{
		if (screen != null && ScreenUtils.GetMatchScreenName(context, screen, new string[] { "3D地图" }) == "3D地图")
		{
			context.ScreenContext.UpdateCurrentScreenName("3D地图");
			return new OperationResult(IsSuccess: true, "3D地图");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "按钮-关闭筛选");
		bool flag = false;
		if (area != null && context.Controller != null)
		{
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			flag = controller.Click(position, null, pcAlt, gamepadKey);
		}
		return new OperationResult(flag, flag ? "关闭筛选" : "点击失败 按钮-关闭筛选");
	}

	/// <inheritdoc />
	public OperationResult ClickMiniScale(ZContext context)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "按钮-最小缩放");
		if (area == null || context.Controller == null)
		{
			return new OperationResult(IsSuccess: false, "点击失败 按钮-最小缩放");
		}
		context.Controller.DragTo(area.Center + new OneDragon.Core.Abstractions.Geometry.Point(-300, 0), area.Center);
		context.Logger.Information("3D地图执行最小缩放");
		return new OperationResult(IsSuccess: true, "最小缩放");
	}

	/// <inheritdoc />
	public OperationResult InitTransportPointSearch(ZContext context, WorldPatrolArea targetArea, string targetTransportName)
	{
		_largeMap = context.WorldPatrolService.GetLargeMapByAreaFullId(targetArea.FullId);
		_targetIcon = _largeMap?.IconList.FirstOrDefault((WorldPatrolLargeMapIcon icon) => string.Equals(icon.IconName, targetTransportName, StringComparison.Ordinal));
		_iconWordList = _largeMap?.IconList.Select((WorldPatrolLargeMapIcon icon) => icon.IconName).ToArray() ?? Array.Empty<string>();
		_mapArea = context.ScreenContext.GetArea("3D地图", "区域-地图");
		if (_targetIcon == null)
		{
			context.Logger.Error("未找到目标传送点配置 {TargetTransportName}", targetTransportName);
			return new OperationResult(IsSuccess: false, "未找到目标传送点配置 " + targetTransportName);
		}
		context.Logger.Information("初始化传送点搜索完成，目标 {TargetTransportName}", targetTransportName);
		return new OperationResult(IsSuccess: true, targetTransportName);
	}

	/// <inheritdoc />
	public OperationResult SearchTransportPoint(ZContext context, Mat? screen, string targetTransportName, CancellationToken cancellationToken)
	{
		if (_targetIcon == null || _largeMap == null)
		{
			return new OperationResult(IsSuccess: false, "未找到目标传送点配置 " + targetTransportName);
		}
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (_mapArea == null)
		{
			return new OperationResult(IsSuccess: false, "区域配置不存在 区域-地图");
		}
		bool isEnabled = ActionLevelDebugEvidenceWriter.IsEnabled;
		string fileStem = ActionLevelDebugEvidenceWriter.CreateFileStem(ActionLevelDebugEvidenceWriter.GetApplicationId() + "-transport-search-3d-map-tp");
		string beforeScreenshotPath = (isEnabled ? ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "before", screen) : null);
		List<TransportPointSearchIconEvidence> list = new List<TransportPointSearchIconEvidence>();
		MatchResultList matchResultList = context.TemplateMatcher.CropAndMatchTemplate(screen, _mapArea.Rect, "map", "3d_map_tp_icon_1", 0.5, onlyBest: false);
		if (matchResultList.Count == 0)
		{
			context.Logger.Debug("画面内无传送点图标，执行随机拖动");
			PerformRandomDrag(context, _mapArea);
			if (isEnabled)
			{
				WriteSearchTransportPointEvidence(context, fileStem, beforeScreenshotPath, targetTransportName, matchResultList.Count, list, "random_drag_no_visible_icon", "画面内无传送点图标");
			}
			return new OperationResult(IsSuccess: false, "画面内无传送点图标");
		}
		WorldPatrolLargeMapIcon worldPatrolLargeMapIcon = null;
		context.Logger.Debug("画面内发现 {IconCount} 个传送点图标，开始逐个检查", matchResultList.Count);
		foreach (MatchResult item in matchResultList.Items)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TransportPointSearchIconEvidence transportPointSearchIconEvidence = new TransportPointSearchIconEvidence
			{
				ClickX = item.Center.X,
				ClickY = item.Center.Y,
				MatchConfidence = item.Confidence
			};
			list.Add(transportPointSearchIconEvidence);
			ControllerBase? controller = context.Controller;
			if (controller == null || !controller.Click(item.Center))
			{
				transportPointSearchIconEvidence.FailureReason = "click_reported_failure";
			}
			using Mat mat = CaptureAfterIconClick(context, cancellationToken);
			if (mat == null)
			{
				context.Logger.Warning("点击传送点图标后未获取到截图");
				transportPointSearchIconEvidence.FailureReason = "screenshot_after_icon_click_missing";
				continue;
			}
			Mat screen2 = mat;
			if (ScreenUtils.FindArea(context, screen2, "3D地图", "按钮-前往") != FindAreaResultEnum.True)
			{
				transportPointSearchIconEvidence.GoButtonFound = false;
				transportPointSearchIconEvidence.FailureReason = "go_button_not_found";
				continue;
			}
			transportPointSearchIconEvidence.GoButtonFound = true;
			string text = RecognizeSelectedTransportPoint(context, screen2);
			context.Logger.Debug("OCR识别到传送点名称 {RecognizedName}", text ?? string.Empty);
			transportPointSearchIconEvidence.RecognizedName = text;
			if (string.IsNullOrWhiteSpace(text))
			{
				transportPointSearchIconEvidence.FailureReason = "transport_name_ocr_empty";
				continue;
			}
			string[] targetWords = _iconWordList.Select((string name) => GameTextTranslator.Translate(context.Environment, context.GameAccountConfig.GameLanguage, name)).ToArray();
			int? num = FindBestStringMatchIndex(text, targetWords, 0.6);
			object obj;
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				obj = _largeMap.IconList[valueOrDefault];
			}
			else
			{
				obj = null;
			}
			WorldPatrolLargeMapIcon worldPatrolLargeMapIcon2 = (WorldPatrolLargeMapIcon)obj;
			transportPointSearchIconEvidence.MatchedName = worldPatrolLargeMapIcon2?.IconName;
			if (worldPatrolLargeMapIcon2 == null)
			{
				context.Logger.Warning("无法匹配传送点名称 {RecognizedName}", text);
				transportPointSearchIconEvidence.FailureReason = "transport_name_match_failed";
				continue;
			}
			if (string.Equals(worldPatrolLargeMapIcon2.IconName, targetTransportName, StringComparison.Ordinal))
			{
				context.Logger.Information("找到目标传送点 {TargetTransportName}", targetTransportName);
				if (isEnabled)
				{
					WriteSearchTransportPointEvidence(context, fileStem, beforeScreenshotPath, targetTransportName, matchResultList.Count, list, "found_target_transport_point", null);
				}
				return new OperationResult(IsSuccess: true, targetTransportName);
			}
			if (worldPatrolLargeMapIcon == null)
			{
				worldPatrolLargeMapIcon = worldPatrolLargeMapIcon2;
			}
			context.Logger.Debug("记录导航参考点 {IconName}({X}, {Y})", worldPatrolLargeMapIcon2.IconName, worldPatrolLargeMapIcon2.LargeMapPosition.X, worldPatrolLargeMapIcon2.LargeMapPosition.Y);
			transportPointSearchIconEvidence.FailureReason = "not_target_transport_point";
		}
		string transitionResult;
		if (worldPatrolLargeMapIcon != null)
		{
			DragTowardTarget(context, _mapArea, worldPatrolLargeMapIcon, _targetIcon);
			transitionResult = "drag_toward_target_from_" + worldPatrolLargeMapIcon.IconName;
		}
		else
		{
			context.Logger.Debug("所有图标识别失败，执行随机拖动");
			PerformRandomDrag(context, _mapArea);
			transitionResult = "random_drag_no_navigation_reference";
		}
		if (isEnabled)
		{
			WriteSearchTransportPointEvidence(context, fileStem, beforeScreenshotPath, targetTransportName, matchResultList.Count, list, transitionResult, "未找到目标传送点");
		}
		return new OperationResult(IsSuccess: false, "未找到目标传送点");
	}

	/// <inheritdoc />
	public void CloseAreaInfoPopup(ZContext context, Mat? screen)
	{
		if (screen != null && ScreenUtils.FindArea(context, screen, "3D地图", "按钮-区域信息-关闭") == FindAreaResultEnum.True)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "按钮-区域信息-关闭");
			if (area != null && context.Controller != null)
			{
				WaitBeforeClick();
				ControllerBase? controller = context.Controller;
				OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
				bool pcAlt = area.PcAlt;
				string gamepadKey = area.GamepadKey;
				controller.Click(position, null, pcAlt, gamepadKey);
			}
		}
	}

	/// <inheritdoc />
	public OperationResult ClickGo(ZContext context, Mat? screen)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (ScreenUtils.FindArea(context, screen, "3D地图", "按钮-前往") != FindAreaResultEnum.True)
		{
			return new OperationResult(IsSuccess: false, "找不到 按钮-前往");
		}
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "按钮-前往");
		bool flag = false;
		if (area != null && context.Controller != null)
		{
			WaitBeforeClick();
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = area.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			flag = controller.Click(position, null, pcAlt, gamepadKey);
		}
		return new OperationResult(flag, flag ? "按钮-前往" : "点击失败 按钮-前往");
	}

	/// <inheritdoc />
	public async Task<OperationResult> WaitNormalWorldAfterTransportAsync(ZContext context, WorldPatrolArea targetArea, CancellationToken cancellationToken)
	{
		OperationResult result = await new BackToNormalWorld(context, ensureNormalWorld: false, allowBattle: true).ExecuteAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsSuccess && (string.Equals(result.Status, "发现地图", StringComparison.Ordinal) || string.Equals(result.Status, "大世界-战斗", StringComparison.Ordinal)))
		{
			context.ScreenContext.UpdateCurrentScreenName(targetArea.IsHollow ? "大世界-勘域" : "大世界-普通");
		}
		return result;
	}

	private Mat? CaptureAfterIconClick(ZContext context, CancellationToken cancellationToken)
	{
		if (_iconClickDelay > TimeSpan.Zero)
		{
			Task.Delay(_iconClickDelay, cancellationToken).GetAwaiter().GetResult();
		}
		return context.Controller?.Screenshot().Screen;
	}

	private static string? RecognizeSelectedTransportPoint(ZContext context, Mat screen)
	{
		OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("3D地图", "标题-当前选择传送点");
		if (area == null)
		{
			return null;
		}
		return context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect).FirstOrDefault()?.Text;
	}

	private void DragTowardTarget(ZContext context, OneDragon.Core.Screen.ScreenArea mapArea, WorldPatrolLargeMapIcon navigationReference, WorldPatrolLargeMapIcon targetIcon)
	{
		if (context.Controller != null)
		{
			int num = targetIcon.LargeMapPosition.X - navigationReference.LargeMapPosition.X;
			int num2 = targetIcon.LargeMapPosition.Y - navigationReference.LargeMapPosition.Y;
			int num3;
			int num4;
			if (Math.Abs(num) > Math.Abs(num2))
			{
				num3 = ((num > 0) ? (-300) : 300);
				num4 = ((num != 0) ? (-(int)(300.0 * ((double)num2 / (double)Math.Abs(num)))) : 0);
			}
			else
			{
				num4 = ((num2 > 0) ? (-300) : 300);
				num3 = ((num2 != 0) ? (-(int)(300.0 * ((double)num / (double)Math.Abs(num2)))) : 0);
			}
			OneDragon.Core.Abstractions.Geometry.Point center = mapArea.Center;
			OneDragon.Core.Abstractions.Geometry.Point end = center + new OneDragon.Core.Abstractions.Geometry.Point(num3, num4);
			context.Logger.Debug("执行精确拖动 从 {ReferenceName}({ReferenceX}, {ReferenceY}) 向 {TargetName}({TargetX}, {TargetY}) 坐标差({DeltaX}, {DeltaY}) 拖动方向({DragX}, {DragY})", navigationReference.IconName, navigationReference.LargeMapPosition.X, navigationReference.LargeMapPosition.Y, targetIcon.IconName, targetIcon.LargeMapPosition.X, targetIcon.LargeMapPosition.Y, num, num2, num3, num4);
			context.Controller.DragTo(end, center);
		}
	}

	private OperationResult ClickTextInArea(ZContext context, Mat? screen, string targetText, OneDragon.Core.Screen.ScreenArea? area, double lcsPercent)
	{
		if (screen == null)
		{
			return new OperationResult(IsSuccess: false, "未获取截图");
		}
		if (area == null)
		{
			return new OperationResult(IsSuccess: false, "区域配置不存在 " + targetText);
		}
		if (context.Controller == null)
		{
			return new OperationResult(IsSuccess: false, "点击失败 " + targetText);
		}
		string text = GameTextTranslator.Translate(context.Environment, context.GameAccountConfig.GameLanguage, targetText);
		IReadOnlyList<OcrMatchResult> ocrResultList = context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect);
		OcrMatchResult ocrMatchResult = FindBestOcrMatch(text, ocrResultList, 0.6);
		if (ocrMatchResult == null || !StringUtils.FindByLcs(text, ocrMatchResult.Text, lcsPercent))
		{
			return new OperationResult(IsSuccess: false, "找不到 " + targetText);
		}
		WaitBeforeClick();
		bool flag = context.Controller.Click(ocrMatchResult.Center);
		return new OperationResult(flag, flag ? targetText : ("点击失败 " + targetText));
	}

	private void WaitBeforeClick()
	{
		if (_clickPreDelay > TimeSpan.Zero)
		{
			_sleep(_clickPreDelay);
		}
	}

	private void PerformRandomDrag(ZContext context, OneDragon.Core.Screen.ScreenArea mapArea)
	{
		if (context.Controller != null)
		{
			OneDragon.Core.Abstractions.Geometry.Point[] array = new OneDragon.Core.Abstractions.Geometry.Point[8]
			{
				new OneDragon.Core.Abstractions.Geometry.Point(300, 0),
				new OneDragon.Core.Abstractions.Geometry.Point(-300, 0),
				new OneDragon.Core.Abstractions.Geometry.Point(0, 300),
				new OneDragon.Core.Abstractions.Geometry.Point(0, -300),
				new OneDragon.Core.Abstractions.Geometry.Point(300, 300),
				new OneDragon.Core.Abstractions.Geometry.Point(-300, -300),
				new OneDragon.Core.Abstractions.Geometry.Point(300, -300),
				new OneDragon.Core.Abstractions.Geometry.Point(-300, 300)
			};
			int num = _nextRandomIndex(array.Length);
			int num2 = (num % array.Length + array.Length) % array.Length;
			OneDragon.Core.Abstractions.Geometry.Point center = mapArea.Center;
			context.Logger.Debug("执行随机拖动 {Direction}", array[num2]);
			context.Controller.DragTo(center + array[num2], center);
		}
	}

	private static OcrMatchResult? FindBestOcrMatch(string targetText, IReadOnlyList<OcrMatchResult> results, double threshold)
	{
		OcrMatchResult ocrMatchResult = results.FirstOrDefault((OcrMatchResult item) => string.Equals(item.Text, targetText, StringComparison.Ordinal));
		if (ocrMatchResult != null)
		{
			return ocrMatchResult;
		}
		string[] targetWords = results.Select((OcrMatchResult result) => result.Text).ToArray();
		int? num = StringUtils.FindBestMatchByDifflib(targetText, targetWords, threshold);
		return (!num.HasValue) ? null : results[num.Value];
	}

	private static int? FindBestStringMatchIndex(string word, IReadOnlyList<string> targetWords, double threshold)
	{
		return StringUtils.FindBestMatchByDifflib(word, targetWords, threshold);
	}

	private static bool IsTargetAfterOcrList(string targetName, IReadOnlyList<string> orderedNames, IReadOnlyList<string> ocrWords, Func<string, string> translateGameText, double threshold)
	{
		bool flag = false;
		bool flag2 = false;
		foreach (string orderedName in orderedNames)
		{
			if (string.Equals(targetName, orderedName, StringComparison.Ordinal))
			{
				flag = true;
				break;
			}
			if (StringUtils.FindBestMatchByDifflib(translateGameText(orderedName), ocrWords, threshold).HasValue)
			{
				flag2 = true;
			}
		}
		return flag && flag2;
	}

	private static AreaClickTargetSummary? CreateAreaClickTargetSummary(OneDragon.Core.Screen.ScreenArea? area)
	{
		if (area == null)
		{
			return new AreaClickTargetSummary
			{
				ScreenName = "大世界",
				AreaName = "小地图",
				FailureReason = "area not configured"
			};
		}
		return new AreaClickTargetSummary
		{
			ScreenName = "大世界",
			AreaName = "小地图",
			AreaKind = "static",
			ClickX = area.Center.X,
			ClickY = area.Center.Y,
			PcAlt = area.PcAlt,
			GamepadAction = area.GamepadKey
		};
	}

	private static void WriteOpen3dMapEvidence(ZContext context, string fileStem, string? beforeScreenshotPath, string? beforeScreenName, bool miniMapPlayerMaskFound, AreaClickTargetSummary? actionTarget, string? afterScreenshotPath, bool actionClicked, string? failureReason)
	{
		string text = null;
		if (!string.IsNullOrWhiteSpace(afterScreenshotPath))
		{
			using Mat mat = Cv2.ImRead(afterScreenshotPath);
			if (!mat.Empty())
			{
				text = ScreenUtils.GetMatchScreenName(context, mat, new string[] { "3D地图" });
			}
		}
		ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
		{
			FileStem = fileStem,
			AppId = ActionLevelDebugEvidenceWriter.GetApplicationId(),
			OperationName = "传送",
			NodeName = "打开3D地图",
			DotNetMethod = "ZzzOd.GameLogic.Application.WorldPatrol.Operations.DefaultTransportBy3dMapServices.Open3dMap()",
			BaselineParityRequirement = "TransportBy3dMap.open_map confirms the mini-map player mask, then clicks area 大世界/小地图.",
			BeforeScreenshotPath = beforeScreenshotPath,
			BeforeRecognitionSummary = new
			{
				ActiveScreenName = beforeScreenName,
				MiniMapPlayerMaskFound = miniMapPlayerMaskFound
			},
			ActionKind = "click_area",
			ActionTarget = "大世界/小地图",
			ActionTargetDetails = actionTarget,
			ExpectedNextState = "3D地图",
			AfterScreenshotPath = afterScreenshotPath,
			AfterRecognitionSummary = new
			{
				ActiveScreenName = text,
				Is3dMapScreen = string.Equals(text, "3D地图", StringComparison.Ordinal)
			},
			TransitionResult = (string.Equals(text, "3D地图", StringComparison.Ordinal) ? "entered_3d_map" : (actionClicked ? "not_entered_3d_map_yet" : "action_failed")),
			FailureReason = failureReason,
			RetryStoppedBecauseOfSuspectedLoop = false
		});
	}

	private static void WriteSearchTransportPointEvidence(ZContext context, string fileStem, string? beforeScreenshotPath, string targetTransportName, int iconMatchCount, IReadOnlyList<TransportPointSearchIconEvidence> iconEvidence, string transitionResult, string? failureReason)
	{
		string afterScreenshotPath = null;
		string activeScreenName = null;
		using Mat mat = context.Controller?.Screenshot().Screen;
		if (mat != null)
		{
			afterScreenshotPath = ActionLevelDebugEvidenceWriter.WriteScreenshot(fileStem, "after", mat);
			activeScreenName = ScreenUtils.GetMatchScreenName(context, mat, new string[] { "3D地图" });
		}
		ActionLevelDebugEvidenceWriter.Write(new ActionLevelDebugEvidence
		{
			FileStem = fileStem,
			AppId = ActionLevelDebugEvidenceWriter.GetApplicationId(),
			OperationName = "传送",
			NodeName = "搜索传送点循环",
			DotNetMethod = "ZzzOd.GameLogic.Application.WorldPatrol.Operations.DefaultTransportBy3dMapServices.SearchTransportPoint()",
			BaselineParityRequirement = "TransportBy3dMap.search_tp_icon_loop clicks visible 3D map transport icons, checks 前往, OCRs 标题-当前选择传送点, and compares the recognized name with the configured target.",
			BeforeScreenshotPath = beforeScreenshotPath,
			BeforeRecognitionSummary = new
			{
				ActiveScreenName = "3D地图",
				TargetTransportName = targetTransportName,
				IconMatchCount = iconMatchCount
			},
			ActionKind = "search_transport_point",
			ActionTarget = targetTransportName,
			ActionTargetDetails = new
			{
				IconMatchCount = iconMatchCount,
				Icons = iconEvidence
			},
			ExpectedNextState = "target transport point selected and 按钮-前往 visible",
			AfterScreenshotPath = afterScreenshotPath,
			AfterRecognitionSummary = new
			{
				ActiveScreenName = activeScreenName
			},
			TransitionResult = transitionResult,
			FailureReason = failureReason,
			RetryStoppedBecauseOfSuspectedLoop = false
		});
	}
}

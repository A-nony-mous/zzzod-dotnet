using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Controller;
using OneDragon.Core.Ocr;
using OneDragon.Core.Screen;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.GameData;
using ZzzOd.GameLogic.HollowZero;
using ZzzOd.GameLogic.HollowZero.GameData;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// BaselineParity <c>normal_event_handler.py</c> 的普通事件 OCR 选择。
/// </summary>
internal static class WitheredDomainEventOperations
{
	private const string EventScreen = "零号空洞-事件";

	private const string EventTextArea = "事件文本";

	/// <summary>
	/// 清除侵蚀症状（remove_corruption.py）使用的白色 OCR 过滤范围。
	/// </summary>
	internal static readonly IReadOnlyList<IReadOnlyList<int>> WhiteColorRange = new IReadOnlyList<int>[2]
	{
		new int[3] { 240, 240, 240 },
		new int[3] { 255, 255, 255 }
	};

	internal static async Task<HollowEventHandleResult> HandleNormalEventAsync(ZContext context, HollowZeroEvent normalEvent, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null)
		{
			return Failed(normalEvent.EventName, "未获取事件截图");
		}
		using (screen)
		{
			if (context.Controller == null)
			{
				return Failed(normalEvent.EventName, "控制器未就绪");
			}
			OneDragon.Core.Screen.ScreenArea textArea = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
			if (textArea == null)
			{
				return Failed(normalEvent.EventName, "区域未配置 事件文本");
			}
			IReadOnlyList<OcrMatchResult> results = ReadEventTextResults(context, screen, textArea);
			(HollowZeroNormalEventOption Option, OcrMatchResult Result)? selection = FindReferenceEquivalentOption(results, normalEvent.Options);
			bool pcAlt;
			string gamepadKey;
			if (selection.HasValue)
			{
				HollowZeroNormalEventOption selected = selection.Value.Option;
				ControllerBase? controller = context.Controller;
				OneDragon.Core.Abstractions.Geometry.Point? position = selection.Value.Result.Center;
				pcAlt = textArea.PcAlt;
				gamepadKey = textArea.GamepadKey;
				if (!controller.Click(position, null, pcAlt, gamepadKey))
				{
					return Failed(normalEvent.EventName, "点击事件选项失败 " + selected.OptionName);
				}
				await Task.Delay(TimeSpan.FromMilliseconds(100L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				ControllerBase? controller2 = context.Controller;
				OneDragon.Core.Abstractions.Geometry.Point? position2 = textArea.LeftTop;
				pcAlt = textArea.PcAlt;
				gamepadKey = textArea.GamepadKey;
				if (!controller2.Click(position2, null, pcAlt, gamepadKey))
				{
					return Failed(normalEvent.EventName, "点击事件文本失败");
				}
				await Task.Delay(TimeSpan.FromSeconds(selected.Wait), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return new HollowEventHandleResult(normalEvent.EventName, HollowEventOutcomeKind.Interacted, Success: true, selected.OptionName);
			}
			if (!ContainsEventMark(results, normalEvent.EventName, normalEvent.LcsPercent))
			{
				return Failed(normalEvent.EventName, "未识别事件标题或配置选项");
			}
			OcrMatchResult bottom = (from item in results
				where item.Text.Trim().Length > 1
				orderby item.Center.Y descending
				select item).FirstOrDefault();
			int num;
			if (bottom != null)
			{
				ControllerBase? controller3 = context.Controller;
				OneDragon.Core.Abstractions.Geometry.Point? position3 = bottom.Center;
				pcAlt = textArea.PcAlt;
				gamepadKey = textArea.GamepadKey;
				num = ((!controller3.Click(position3, null, pcAlt, gamepadKey)) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			if (num != 0)
			{
				return Failed(normalEvent.EventName, "点击事件底部文本失败");
			}
			await Task.Delay(TimeSpan.FromMilliseconds(200L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller4 = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position4 = textArea.LeftTop;
			pcAlt = textArea.PcAlt;
			gamepadKey = textArea.GamepadKey;
			return controller4.Click(position4, null, pcAlt, gamepadKey) ? new HollowEventHandleResult(normalEvent.EventName, HollowEventOutcomeKind.Interacted, Success: true, "事件无选项") : Failed(normalEvent.EventName, "点击事件文本失败");
		}
	}

	internal static async Task<HollowEventHandleResult> ClickEventTextAsync(ZContext context, string eventName, string targetText, double lcsPercent, TimeSpan wait, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null || context.Controller == null)
		{
			screen?.Dispose();
			return Failed(eventName, "事件控制器未就绪");
		}
		using (screen)
		{
			OneDragon.Core.Screen.ScreenArea textArea = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
			if (textArea == null)
			{
				return Failed(eventName, "区域未配置 事件文本");
			}
			OcrMatchResult target = ReadEventTextResults(context, screen, textArea).FirstOrDefault((OcrMatchResult item) => StringUtils.FindByLcs(targetText, item.Text, lcsPercent));
			if (target == null)
			{
				return Failed(eventName, "未识别事件文本 " + targetText);
			}
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = target.Center;
			bool pcAlt = textArea.PcAlt;
			string gamepadKey = textArea.GamepadKey;
			if (!controller.Click(position, null, pcAlt, gamepadKey))
			{
				return Failed(eventName, "点击事件文本失败 " + targetText);
			}
			await Task.Delay(TimeSpan.FromMilliseconds(100L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller2 = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position2 = textArea.LeftTop;
			pcAlt = textArea.PcAlt;
			gamepadKey = textArea.GamepadKey;
			if (!controller2.Click(position2, null, pcAlt, gamepadKey))
			{
				return Failed(eventName, "点击事件文本失败");
			}
		}
		await Task.Delay(wait, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true, targetText);
	}

	internal static async Task<HollowEventHandleResult> HandleCallForSupportAsync(ZContext context, WitheredDomainEventDataService eventData, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null || context.Controller == null)
		{
			screen?.Dispose();
			return Failed("呼叫增援！", "事件控制器未就绪");
		}
		bool replacing;
		Agent candidate;
		int? position;
		using (screen)
		{
			OneDragon.Core.Screen.ScreenArea textArea = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
			OneDragon.Core.Screen.ScreenArea supportAgentArea = context.ScreenContext.GetArea("零号空洞-事件", "呼叫增援-角色行");
			if (textArea == null || supportAgentArea == null)
			{
				return Failed("呼叫增援！", "呼叫增援区域未配置");
			}
			IReadOnlyList<OcrMatchResult> texts = context.OcrService.GetOcrResultList(screen, textArea.ColorRange, textArea.Rect);
			OcrMatchResult accept = FindText(texts, "接应支援代理人", 0.6);
			replacing = false;
			if (accept == null)
			{
				accept = FindText(texts, "接替小队成员", 0.8) ?? FindText(texts, "接替小组成员", 0.8);
				replacing = accept != null;
			}
			if (accept == null)
			{
				return await RejectSupportAsync(context, screen, textArea, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			IReadOnlyList<Agent?> team = context.WitheredDomain.CheckAgentList(screen);
			candidate = MatchSupportAgent(context, screen, supportAgentArea);
			if (team == null || candidate == null)
			{
				return Failed("呼叫增援！", (team == null) ? "无法识别当前队伍" : "无法识别增援代理人");
			}
			position = GetSupportPosition(context.WitheredDomain.GetTargetAgents(), team, candidate);
			if (!position.HasValue)
			{
				return await RejectSupportAsync(context, screen, textArea, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position2 = accept.Center;
			bool pcAlt = textArea.PcAlt;
			string gamepadKey = textArea.GamepadKey;
			if (!controller.Click(position2, null, pcAlt, gamepadKey))
			{
				return Failed("呼叫增援！", "点击接应选项失败");
			}
		}
		await Task.Delay(TimeSpan.FromSeconds(2L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		Mat positionScreen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (positionScreen == null || context.Controller == null)
		{
			positionScreen?.Dispose();
			return Failed("呼叫增援！", "未获取位置选择画面");
		}
		using (positionScreen)
		{
			OneDragon.Core.Screen.ScreenArea textArea2 = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
			if (textArea2 == null)
			{
				return Failed("呼叫增援！", "区域未配置 事件文本");
			}
			string positionText = (replacing ? $"接替{position.Value}号队员的位置" : $"{position.Value}号位");
			OcrMatchResult positionResult = FindText(context.OcrService.GetOcrResultList(positionScreen, textArea2.ColorRange, textArea2.Rect), positionText, 1.0);
			if (positionResult == null)
			{
				return Failed("呼叫增援！", "未能选择 " + positionText);
			}
			await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller2 = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position3 = positionResult.Center;
			bool pcAlt = textArea2.PcAlt;
			string gamepadKey = textArea2.GamepadKey;
			if (!controller2.Click(position3, null, pcAlt, gamepadKey))
			{
				return Failed("呼叫增援！", "未能选择 " + positionText);
			}
			await Task.Delay(TimeSpan.FromSeconds(2L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			context.WitheredDomain.UpdateAgentListAfterSupport(candidate, position.Value);
			ControllerBase? controller3 = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position4 = textArea2.LeftTop;
			pcAlt = textArea2.PcAlt;
			gamepadKey = textArea2.GamepadKey;
			return controller3.Click(position4, null, pcAlt, gamepadKey) ? new HollowEventHandleResult("呼叫增援！", HollowEventOutcomeKind.Interacted, Success: true, positionText) : Failed("呼叫增援！", "确认增援后点击事件文本失败");
		}
	}

	internal static async Task<HollowEventHandleResult> HandleBambooMerchantAsync(ZContext context, WitheredDomainEventDataService eventData, string eventName, CancellationToken cancellationToken)
	{
		int unknownScreenRetries = 0;
		_ = string.Empty;
		Mat screen;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
			if (screen == null || context.Controller == null)
			{
				break;
			}
			using (screen)
			{
				OneDragon.Core.Screen.ScreenArea eventText = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
				OneDragon.Core.Screen.ScreenArea titleArea = context.ScreenContext.GetArea("零号空洞-商店", "二级标题");
				if (eventText == null || titleArea == null)
				{
					return Failed(eventName, "商店区域未配置");
				}
				IReadOnlyList<OcrMatchResult> eventTexts = context.OcrService.GetOcrResultList(screen, eventText.ColorRange, eventText.Rect);
				IReadOnlyList<OcrMatchResult> titles = context.OcrService.GetOcrResultList(screen, titleArea.ColorRange, titleArea.Rect);
				string status;
				if (ContainsText(titles, "交易", 1.0) || ContainsText(titles, "特价折扣", 1.0))
				{
					status = "购买";
					unknownScreenRetries = 0;
					goto IL_0ad2;
				}
				if (ContainsText(titles, "催化", 1.0))
				{
					status = "催化";
					unknownScreenRetries = 0;
					goto IL_0ad2;
				}
				if (ContainsText(titles, "血汗交易", 0.6))
				{
					status = "不购买";
					unknownScreenRetries = 0;
					goto IL_0ad2;
				}
				OcrMatchResult confirm = FindText(eventTexts, "确定", 0.6);
				bool pcAlt;
				string gamepadKey;
				if (confirm != null)
				{
					await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					ControllerBase? controller = context.Controller;
					OneDragon.Core.Abstractions.Geometry.Point? position = confirm.Center;
					pcAlt = eventText.PcAlt;
					gamepadKey = eventText.GamepadKey;
					if (!controller.Click(position, null, pcAlt, gamepadKey))
					{
						int num = unknownScreenRetries + 1;
						unknownScreenRetries = num;
						if (num > 3)
						{
							return Failed(eventName, "点击商店确认失败");
						}
						await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						unknownScreenRetries = 0;
						await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					continue;
				}
				OcrMatchResult enter = FindText(eventTexts, "进入商店", 0.6);
				if (enter != null)
				{
					await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					ControllerBase? controller2 = context.Controller;
					OneDragon.Core.Abstractions.Geometry.Point? position2 = enter.Center;
					pcAlt = eventText.PcAlt;
					gamepadKey = eventText.GamepadKey;
					if (!controller2.Click(position2, null, pcAlt, gamepadKey))
					{
						int num = unknownScreenRetries + 1;
						unknownScreenRetries = num;
						if (num > 3)
						{
							return Failed(eventName, "进入商店点击失败");
						}
						await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						unknownScreenRetries = 0;
						await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					continue;
				}
				if (FindText(eventTexts, "血汗交易", 0.6) != null)
				{
					status = "不购买";
					unknownScreenRetries = 0;
					goto IL_0ad2;
				}
				OcrMatchResult trade = FindText(eventTexts, "鸣徽交易", 0.6) ?? FindText(eventTexts, "特价折扣", 0.6);
				if (trade == null)
				{
					int num = unknownScreenRetries + 1;
					unknownScreenRetries = num;
					if (num > 3)
					{
						return Failed(eventName, "未识别邦布商店画面");
					}
					await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				ControllerBase? controller3 = context.Controller;
				OneDragon.Core.Abstractions.Geometry.Point? position3 = trade.Center;
				pcAlt = eventText.PcAlt;
				gamepadKey = eventText.GamepadKey;
				if (!controller3.Click(position3, null, pcAlt, gamepadKey))
				{
					int num = unknownScreenRetries + 1;
					unknownScreenRetries = num;
					if (num > 3)
					{
						return Failed(eventName, "选择商店交易失败");
					}
					await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				else
				{
					unknownScreenRetries = 0;
					await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				goto end_IL_012e;
				IL_0ad2:
				if (status == "催化" || status == "不购买")
				{
					return await BackFromMerchantAsync(context, eventName, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				HollowEventHandleResult choose = await ChooseAndBuyMerchantItemAsync(context, eventData, screen, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if ((object)choose != null)
				{
					return choose;
				}
				end_IL_012e:;
			}
		}
		screen?.Dispose();
		return Failed(eventName, "商店控制器未就绪");
	}

	internal static async Task<HollowEventHandleResult> HandleResoniumAsync(ZContext context, WitheredDomainEventDataService eventData, string eventName, CancellationToken cancellationToken)
	{
		if (1 == 0)
		{
		}
		string text = eventName switch
		{
			"选择" => "选择", 
			"催化" => "催化", 
			"丢弃" => "丢弃", 
			"抵押欠款" => "抵押欠款", 
			"交换" => "交换", 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		string action = text;
		if (action == null)
		{
			return Failed(eventName, "未配置鸣徽事件动作");
		}
		if (context.Controller == null)
		{
			return Failed(eventName, "鸣徽控制器未就绪");
		}
		for (int retry = 0; retry <= 3; retry++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Mat screen = context.Controller.Screenshot().Screen;
			if (screen == null)
			{
				return Failed(eventName, "未获取鸣徽截图");
			}
			using (screen)
			{
				List<(WitheredDomainResonium Resonium, OneDragon.Core.Screen.ScreenArea Button)> choices = new List<(WitheredDomainResonium, OneDragon.Core.Screen.ScreenArea)>();
				for (int index = 1; index <= 3; index++)
				{
					OneDragon.Core.Screen.ScreenArea nameArea = context.ScreenContext.GetArea("零号空洞-事件", $"鸣徽名称-{index}");
					OneDragon.Core.Screen.ScreenArea buttonArea = context.ScreenContext.GetArea("零号空洞-事件", $"鸣徽选择-{index}");
					if (nameArea == null || buttonArea == null)
					{
						return Failed(eventName, $"鸣徽区域未配置 {index}");
					}
					string name = string.Concat(from result in context.OcrService.GetOcrResultList(screen, nameArea.ColorRange, nameArea.Rect)
						orderby result.X
						select result.Text);
					bool available = context.OcrService.GetOcrResultList(screen, buttonArea.ColorRange, buttonArea.Rect).Any((OcrMatchResult result) => StringUtils.FindByLcs(action, result.Text, 1.0));
					WitheredDomainResonium resonium = eventData.MatchResoniumByOcrFull(name);
					if (available && resonium != null)
					{
						choices.Add((resonium, buttonArea));
					}
				}
				if (choices.Count > 0)
				{
					IReadOnlyList<int> order = OrderResoniumByBaselinePriority(choices.Select<(WitheredDomainResonium, OneDragon.Core.Screen.ScreenArea), WitheredDomainResonium>(((WitheredDomainResonium Resonium, OneDragon.Core.Screen.ScreenArea Button) item) => item.Resonium).ToArray(), context.WitheredDomain.ChallengeConfig?.ResoniumPriority ?? new List<string>(), onlyPriority: false);
					if (order.Count > 0)
					{
						text = action;
						int num;
						if ((!(text == "丢弃") && !(text == "抵押欠款")) || 1 == 0)
						{
							num = order[0];
						}
						else
						{
							num = order[order.Count - 1];
						}
						int chosenIndex = num;
						OneDragon.Core.Screen.ScreenArea button = choices[chosenIndex].Button;
						ControllerBase? controller = context.Controller;
						OneDragon.Core.Abstractions.Geometry.Point? position = button.Center;
						bool pcAlt = button.PcAlt;
						text = button.GamepadKey;
						if (!controller.Click(position, null, pcAlt, text))
						{
							return Failed(eventName, "点击鸣徽选项失败");
						}
						await Task.Delay(TimeSpan.FromMilliseconds(100L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						OneDragon.Core.Screen.ScreenArea empty = context.ScreenContext.GetArea("零号空洞-事件", "空白");
						int num2;
						if (empty != null)
						{
							ControllerBase? controller2 = context.Controller;
							OneDragon.Core.Abstractions.Geometry.Point? position2 = empty.Center;
							pcAlt = empty.PcAlt;
							text = empty.GamepadKey;
							num2 = ((!controller2.Click(position2, null, pcAlt, text)) ? 1 : 0);
						}
						else
						{
							num2 = 1;
						}
						if (num2 != 0)
						{
							return Failed(eventName, "鸣徽选择后点击空白失败");
						}
						await Task.Delay(TimeSpan.FromMilliseconds(900L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
						return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true, choices[chosenIndex].Resonium.Name);
					}
				}
			}
			if (retry < 3)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		return await HandleOcrClickAsync(colorRange: new IReadOnlyList<int>[2]
		{
			new int[3] { 240, 240, 240 },
			new int[3] { 255, 255, 255 }
		}, context: context, eventName: eventName, areaName: "底部-选择列表", targetText: action, cancellationToken: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// 对齐 BaselineParity <c>confirm_resonium.py</c> 与 <c>remove_corruption.py</c>：在底部列表区域用 OCR 找到目标文本并点击。
	/// </summary>
	/// <param name="context">运行上下文。</param>
	/// <param name="eventName">事件名称。</param>
	/// <param name="areaName">识别区域名称。</param>
	/// <param name="targets">按优先级排列的目标文本列表。</param>
	/// <param name="cropFirst">是否先裁剪区域再识别；为 false 时先全图识别再筛选落在区域内的文本，清除侵蚀症状使用该模式以兼容当前识别模型。</param>
	/// <param name="colorRange">OCR 颜色过滤范围；鸣徽确认（confirm_resonium.py）不传颜色范围，清除侵蚀症状（remove_corruption.py）传 <see cref="WhiteColorRange"/>。</param>
	/// <param name="cancellationToken">取消令牌。</param>
	internal static async Task<HollowEventHandleResult> HandleConfirmOrCorruptionAsync(ZContext context, string eventName, string areaName, IReadOnlyList<string> targets, bool cropFirst, IReadOnlyList<IReadOnlyList<int>>? colorRange, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null || context.Controller == null)
		{
			screen?.Dispose();
			return Failed(eventName, "事件控制器未就绪");
		}
		using (screen)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("零号空洞-事件", areaName);
			if (area == null)
			{
				return Failed(eventName, "区域未配置 " + areaName);
			}
			IReadOnlyList<OcrMatchResult> results = context.OcrService.GetOcrResultList(screen, colorRange, area.Rect, cropFirst);
			OcrMatchResult target = targets.Select((string text) => FindText(results, text, 0.6)).FirstOrDefault((OcrMatchResult result) => result != null);
			if (target == null)
			{
				return Failed(eventName, "未识别或无法点击 " + string.Join("/", targets));
			}
			await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = target.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			if (!controller.Click(position, null, pcAlt, gamepadKey))
			{
				return Failed(eventName, "未识别或无法点击 " + string.Join("/", targets));
			}
		}
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true, targets[0]);
	}

	internal static async Task<HollowEventHandleResult> HandleSwiftSupplyAsync(ZContext context, string eventName, CancellationToken cancellationToken)
	{
		return await HandleOcrClickAsync(context, eventName, "底部-选择列表", "降低压力值", null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal static async Task<HollowEventHandleResult> HandleFullInBagAsync(ZContext context, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null || context.Controller == null)
		{
			screen?.Dispose();
			return Failed("背包已满", "背包已满控制器未就绪");
		}
		using (screen)
		{
			OcrMatchResult drop = FindText(context.OcrService.GetOcrResultList(screen), "丢弃", 0.6);
			if (drop == null)
			{
				return Failed("背包已满", "未识别或无法点击丢弃");
			}
			await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!context.Controller.Click(drop.Center))
			{
				return Failed("背包已满", "未识别或无法点击丢弃");
			}
		}
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new HollowEventHandleResult("背包已满", HollowEventOutcomeKind.Interacted, Success: true, "丢弃");
	}

	internal static async Task<HollowEventHandleResult> HandleOldCapitalAsync(ZContext context, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null)
		{
			return Failed("旧都失物", "旧都失物未获取截图");
		}
		using (screen)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (ScreenUtils.FindAndClickArea(context, screen, "零号空洞-事件", "旧都失物-返回") != OcrClickResultEnum.OcrClickSuccess)
			{
				return Failed("旧都失物", "未识别或无法点击旧都失物返回");
			}
		}
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new HollowEventHandleResult("旧都失物", HollowEventOutcomeKind.Interacted, Success: true, "旧都失物-返回");
	}

	/// <summary>
	/// 对齐 BaselineParity <c>door_battle.py</c>：优先点击开门，并兼容侵蚀门的普通事件选项。
	/// </summary>
	internal static async Task<HollowEventHandleResult> HandleDoorBattleAsync(ZContext context, WitheredDomainEventDataService eventData, string entryName, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null || context.Controller == null)
		{
			screen?.Dispose();
			return Failed(entryName, "事件控制器未就绪");
		}
		using (screen)
		{
			OneDragon.Core.Screen.ScreenArea textArea = context.ScreenContext.GetArea("零号空洞-事件", "事件文本");
			if (textArea == null)
			{
				return Failed(entryName, "区域未配置 事件文本");
			}
			List<HollowZeroNormalEventOption> candidates = new List<HollowZeroNormalEventOption>(1)
			{
				new HollowZeroNormalEventOption(HollowZeroSpecialEvent.DoorBattleEntry.EventName, null, 3f)
			};
			candidates.AddRange(eventData.NormalEvents.Where((HollowZeroEvent item) => string.Equals(item.EntryName, "门扉禁闭-侵蚀", StringComparison.Ordinal)).SelectMany((HollowZeroEvent item) => item.Options));
			IReadOnlyList<OcrMatchResult> results = ReadEventTextResults(context, screen, textArea);
			(HollowZeroNormalEventOption Option, OcrMatchResult Result)? selected = FindReferenceEquivalentOption(results, candidates);
			if (!selected.HasValue)
			{
				return Failed(entryName, "未识别开门或兼容事件选项");
			}
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = selected.Value.Result.Center;
			bool pcAlt = textArea.PcAlt;
			string gamepadKey = textArea.GamepadKey;
			if (!controller.Click(position, null, pcAlt, gamepadKey))
			{
				return Failed(entryName, "点击事件选项失败 " + selected.Value.Option.OptionName);
			}
			await Task.Delay(TimeSpan.FromMilliseconds(100L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller2 = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position2 = textArea.LeftTop;
			pcAlt = textArea.PcAlt;
			gamepadKey = textArea.GamepadKey;
			if (!controller2.Click(position2, null, pcAlt, gamepadKey))
			{
				return Failed(entryName, "点击事件文本失败");
			}
			await Task.Delay(TimeSpan.FromSeconds(selected.Value.Option.Wait), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return new HollowEventHandleResult(entryName, HollowEventOutcomeKind.Interacted, Success: true, selected.Value.Option.OptionName);
		}
	}

	/// <summary>
	/// 对齐 BaselineParity <c>leave_random_zone.py</c>：抵达后等待特殊区域，交互提示出现时按交互键再次触发。
	/// </summary>
	internal static async Task<HollowEventHandleResult> HandleLeaveRandomZoneAsync(ZContext context, CancellationToken cancellationToken)
	{
		int retryTimes = 0;
		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
			if (screen == null)
			{
				return Failed("不宜久留", "未获取特殊区域截图");
			}
			using (screen)
			{
				if (string.Equals(WitheredDomainOcrEventSource.DetectEventName(context, screen), "特殊区域", StringComparison.Ordinal))
				{
					return new HollowEventHandleResult("不宜久留", HollowEventOutcomeKind.Interacted, Success: true, "特殊区域");
				}
				if (ScreenUtils.FindArea(context, screen, "零号空洞-事件", "交互可再次触发事件") == FindAreaResultEnum.True)
				{
					ControllerBase controller = context.Controller;
					if (!(controller is IZzzControllerActions controller2))
					{
						return Failed("不宜久留", "控制器不支持交互");
					}
					controller2.Interact(press: true, TimeSpan.FromMilliseconds(200L), release: true);
					await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
			}
			retryTimes++;
			if (retryTimes > 3)
			{
				break;
			}
			await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return Failed("不宜久留", "等待特殊区域超时");
	}

	private static IReadOnlyList<OcrMatchResult> ReadEventTextResults(ZContext context, Mat screen, OneDragon.Core.Screen.ScreenArea textArea)
	{
		return ReadMaskedOcrResults(context, screen, textArea, new Scalar(230.0, 230.0, 230.0), new Scalar(255.0, 255.0, 255.0), 5);
	}

	private static IReadOnlyList<OcrMatchResult> ReadMaskedOcrResults(ZContext context, Mat screen, OneDragon.Core.Screen.ScreenArea area, Scalar lower, Scalar upper, int dilateSize)
	{
		using Mat mat = CvImageUtils.Crop(screen, area.Rect);
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		using Mat mat4 = new Mat();
		Cv2.InRange(mat, lower, upper, mat2);
		using Mat mat5 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilateSize, dilateSize));
		Cv2.Dilate(mat2, mat3, mat5);
		Cv2.BitwiseAnd(mat, mat, mat4, mat3);
		return (from result in context.OcrService.GetOcrResultListForCrop(
			mat4,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1)
			select new OcrMatchResult(result.Confidence, result.X + area.Rect.X1, result.Y + area.Rect.Y1, result.Width, result.Height, result.Text)).ToArray();
	}

	private static (HollowZeroNormalEventOption Option, OcrMatchResult Result)? FindReferenceEquivalentOption(IReadOnlyList<OcrMatchResult> ocrResults, IReadOnlyList<HollowZeroNormalEventOption> options)
	{
		string[] array = (from item in ocrResults
			where item.Text.Trim().Length > 1
			select item.Text).ToArray();
		string[] array2 = options.Select((HollowZeroNormalEventOption item) => item.OcrWord).ToArray();
		foreach (HollowZeroNormalEventOption option in options)
		{
			int? num = StringUtils.FindBestMatchByDifflib(option.OcrWord, array);
			if (!num.HasValue)
			{
				continue;
			}
			string ocrText = array[num.Value];
			int? num2 = StringUtils.FindBestMatchByDifflib(ocrText, array2);
			if (!num2.HasValue || !string.Equals(array2[num2.Value], option.OcrWord, StringComparison.Ordinal))
			{
				continue;
			}
			return (option, ocrResults.First((OcrMatchResult item) => string.Equals(item.Text, ocrText, StringComparison.Ordinal)));
		}
		return null;
	}

	private static bool ContainsEventMark(IReadOnlyList<OcrMatchResult> results, string eventName, float lcsPercent)
	{
		return results.Any((OcrMatchResult item) => StringUtils.FindByLcs(eventName, item.Text, lcsPercent));
	}

	private static async Task<HollowEventHandleResult> RejectSupportAsync(ZContext context, Mat screen, OneDragon.Core.Screen.ScreenArea textArea, CancellationToken cancellationToken)
	{
		(string Text, double LcsPercent)[] rejectOptions = new(string, double)[26]
		{
			("下次再依靠你", 0.5),
			("这次没有研究的机会", 0.5),
			("先不劳烦青衣了", 0.5),
			("暂不需要援护", 0.5),
			("目前不需要支援", 0.5),
			("下次再雇你", 0.5),
			("市民更需要你", 0.5),
			("无需增援", 0.6),
			("无需增援over", 0.5),
			("下次指名你", 0.5),
			("辛苦了兄弟下次一起", 0.5),
			("谢谢可琳这次不用", 0.5),
			("星徽骑士再见", 0.5),
			("还不用请出白祇重工", 0.5),
			("杀以骸焉用艾莲", 0.5),
			("这次不用快回去吃饭吧", 0.5),
			("谢谢你的好意下次一定", 0.5),
			("这点小事不用专家出马", 0.5),
			("下一次一起玩", 0.5),
			("谢谢露西但我能搞定", 0.5),
			("之后有机会再一起玩吧", 0.5),
			("不打扰你工作了", 0.5),
			("还不到常胜冠军出马的时候", 0.5),
			("等遇到大问题再找你帮忙！", 0.5),
			("怎么能让你加班呢", 0.5),
			("放心去照顾嘉音吧，我没问题的", 0.5)
		};
		IReadOnlyList<OcrMatchResult> results = ReadMaskedOcrResults(context, screen, textArea, new Scalar(240.0, 240.0, 240.0), new Scalar(255.0, 255.0, 255.0), 5);
		string[] targetTexts = rejectOptions.Select(((string Text, double LcsPercent) option) => option.Text).ToArray();
		OcrMatchResult reject = null;
		foreach (OcrMatchResult result in results)
		{
			int? optionIndex = StringUtils.FindBestMatchByDifflib(result.Text, targetTexts);
			if (optionIndex.HasValue && StringUtils.FindByLcs(rejectOptions[optionIndex.Value].Text, result.Text, rejectOptions[optionIndex.Value].LcsPercent))
			{
				reject = result;
				break;
			}
		}
		int num;
		bool pcAlt;
		string gamepadKey;
		if (reject != null && context.Controller != null)
		{
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = reject.Center;
			pcAlt = textArea.PcAlt;
			gamepadKey = textArea.GamepadKey;
			num = ((!controller.Click(position, null, pcAlt, gamepadKey)) ? 1 : 0);
		}
		else
		{
			num = 1;
		}
		if (num != 0)
		{
			return Failed("呼叫增援！", "未识别可拒绝的增援选项");
		}
		await Task.Delay(TimeSpan.FromMilliseconds(100L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		ControllerBase? controller2 = context.Controller;
		OneDragon.Core.Abstractions.Geometry.Point? position2 = textArea.LeftTop;
		pcAlt = textArea.PcAlt;
		gamepadKey = textArea.GamepadKey;
		return controller2.Click(position2, null, pcAlt, gamepadKey) ? new HollowEventHandleResult("呼叫增援！", HollowEventOutcomeKind.Interacted, Success: true, "拒绝增援") : Failed("呼叫增援！", "点击拒绝后事件文本失败");
	}

	private static Agent? MatchSupportAgent(ZContext context, Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		string text = string.Concat(from result in context.OcrService.GetOcrResultList(screen, area.ColorRange, area.Rect)
			orderby result.X
			select result.Text).Replace("「", string.Empty, StringComparison.Ordinal);
		int num = new int[3]
		{
			text.IndexOf('响'),
			text.IndexOf('应'),
			text.IndexOf('了')
		}.Where((int index) => index > 0).DefaultIfEmpty(-1).Min();
		int num2 = num;
		if (1 == 0)
		{
		}
		string text2;
		if (num2 <= 1)
		{
			if (num2 > 0)
			{
				goto IL_0145;
			}
			text2 = ((text.Length <= 3) ? text : text.Substring(0, 3));
		}
		else if (text[num] != '应')
		{
			if (num2 <= 2 || text[num] != '了')
			{
				goto IL_0145;
			}
			text2 = text.Substring(0, num - 2);
		}
		else
		{
			text2 = text.Substring(0, num - 1);
		}
		goto IL_0169;
		IL_0169:
		if (1 == 0)
		{
		}
		string word = text2;
		int? num3 = StringUtils.FindBestMatchByDifflib(word, AgentEnum.Values.Select((AgentEnum item) => item.Value.AgentName).ToArray(), 0.1);
		return (!num3.HasValue) ? null : AgentEnum.Values[num3.Value].Value;
		IL_0145:
		text2 = text.Substring(0, num);
		goto IL_0169;
	}

	private static int? GetSupportPosition(IReadOnlyList<string?> targets, IReadOnlyList<Agent?> team, Agent candidate)
	{
		if (team.Count != 3 || targets.Count != 3)
		{
			return null;
		}
		int count = 0;
		for (int i = 0; i < targets.Count; i++)
		{
			if (MatchesTarget(team[0], targets[i]))
			{
				count = i;
				break;
			}
		}
		string[] array = targets.Skip(count).Concat(targets.Take(count)).ToArray();
		if (team[1] == null)
		{
			return MatchesTarget(candidate, array[1]) ? 2 : (MatchesTarget(candidate, array[2]) ? 1 : 2);
		}
		if (team[2] == null)
		{
			if (MatchesTarget(team[1], array[1]))
			{
				return 3;
			}
			if (MatchesTarget(team[1], array[2]))
			{
				return 2;
			}
			if (MatchesTarget(candidate, array[1]))
			{
				return 2;
			}
			return 3;
		}
		if (targets.All((string target) => !MatchesTarget(candidate, target)))
		{
			return null;
		}
		return Array.FindIndex(array, (string target) => MatchesTarget(candidate, target)) + 1;
	}

	private static bool MatchesTarget(Agent? agent, string? target)
	{
		return agent != null && !string.IsNullOrWhiteSpace(target) && (string.Equals(agent.AgentId, target, StringComparison.Ordinal) || string.Equals(agent.AgentTypeStr, target, StringComparison.Ordinal));
	}

	private static OcrMatchResult? FindText(IEnumerable<OcrMatchResult> results, string target, double lcsPercent)
	{
		return results.FirstOrDefault((OcrMatchResult result) => StringUtils.FindByLcs(target, result.Text, lcsPercent));
	}

	private static bool ContainsText(IEnumerable<OcrMatchResult> results, string target, double lcsPercent)
	{
		return results.Any((OcrMatchResult result) => StringUtils.FindByLcs(target, result.Text, lcsPercent));
	}

	private static async Task<HollowEventHandleResult> HandleOcrClickAsync(ZContext context, string eventName, string areaName, string targetText, IReadOnlyList<IReadOnlyList<int>>? colorRange, CancellationToken cancellationToken)
	{
		Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
		if (screen == null || context.Controller == null)
		{
			screen?.Dispose();
			return Failed(eventName, "事件控制器未就绪");
		}
		using (screen)
		{
			OneDragon.Core.Screen.ScreenArea area = context.ScreenContext.GetArea("零号空洞-事件", areaName);
			if (area == null)
			{
				return Failed(eventName, "区域未配置 " + areaName);
			}
			OcrMatchResult target = FindText(context.OcrService.GetOcrResultList(screen, colorRange ?? area.ColorRange, area.Rect), targetText, 0.6);
			if (target == null)
			{
				return Failed(eventName, "未识别或无法点击 " + targetText);
			}
			await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ControllerBase? controller = context.Controller;
			OneDragon.Core.Abstractions.Geometry.Point? position = target.Center;
			bool pcAlt = area.PcAlt;
			string gamepadKey = area.GamepadKey;
			if (!controller.Click(position, null, pcAlt, gamepadKey))
			{
				return Failed(eventName, "未识别或无法点击 " + targetText);
			}
		}
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true, targetText);
	}

	private static async Task<HollowEventHandleResult?> ChooseAndBuyMerchantItemAsync(ZContext context, WitheredDomainEventDataService eventData, Mat screen, CancellationToken cancellationToken)
	{
		OneDragon.Core.Screen.ScreenArea priceArea = context.ScreenContext.GetArea("零号空洞-商店", "商品价格区域");
		OneDragon.Core.Screen.ScreenArea descriptionArea = context.ScreenContext.GetArea("零号空洞-商店", "商品描述区域");
		if (priceArea == null || descriptionArea == null || context.Controller == null)
		{
			return Failed("邦布商人", "商品价格或描述区域未配置");
		}
		IReadOnlyList<OcrMatchResult> prices = ReadMerchantPrices(context, screen, priceArea);
		IReadOnlyList<OcrMatchResult> descriptions = context.OcrService.GetOcrResultList(screen, descriptionArea.ColorRange, descriptionArea.Rect);
		List<(WitheredDomainResonium Resonium, OcrMatchResult Position)> candidates = new List<(WitheredDomainResonium, OcrMatchResult)>();
		foreach (OcrMatchResult description in descriptions)
		{
			WitheredDomainResonium resonium = eventData.MatchResoniumByOcrFull(description.Text);
			if (resonium != null && prices.Any((OcrMatchResult price) => price.Center.Y > description.Center.Y && price.Center.Y - description.Center.Y < 150))
			{
				candidates.Add((resonium, description));
			}
		}
		OcrMatchResult target;
		if (candidates.Count > 0)
		{
			IReadOnlyList<int> order = OrderResoniumByBaselinePriority(candidates.Select<(WitheredDomainResonium, OcrMatchResult), WitheredDomainResonium>(((WitheredDomainResonium Resonium, OcrMatchResult Position) item) => item.Resonium).ToArray(), context.WitheredDomain.ChallengeConfig?.ResoniumPriority ?? new List<string>(), context.WitheredDomain.ChallengeConfig?.BuyOnlyPriority ?? true);
			if (order.Count == 0)
			{
				return await BackFromMerchantAsync(context, "邦布商人", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			target = candidates[order[0]].Position;
		}
		else
		{
			target = prices.OrderBy((OcrMatchResult item) => item.Center.Y).LastOrDefault();
		}
		if (target == null)
		{
			await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return await BackFromMerchantAsync(context, "邦布商人", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		context.Controller.Click(target.Center);
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!(await ClickAreaWithBaselineRetryAsync(context, "零号空洞-商店", "商品购买区域", cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return Failed("邦布商人", "点击购买区域失败");
		}
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return (await ClickAreaWithBaselineRetryAsync(context, "零号空洞-商店", "购买后确定", cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) ? null : Failed("邦布商人", "点击购买确认失败");
	}

	internal static IReadOnlyList<int> OrderResoniumByBaselinePriority(IReadOnlyList<WitheredDomainResonium> items, IReadOnlyList<string> priority, bool onlyPriority)
	{
		List<int> list = new List<int>();
		string[] array = new string[4]
		{
			"S",
			"A",
			"B",
			string.Empty
		};
		foreach (string text in array)
		{
			foreach (string item in priority)
			{
				int num = item.IndexOf(' ');
				string text2 = ((num < 0) ? item : item.Substring(0, num));
				string text3 = ((num < 0) ? string.Empty : item.Substring(num + 1));
				for (int j = 0; j < items.Count; j++)
				{
					WitheredDomainResonium witheredDomainResonium = items[j];
					if (!list.Contains(j) && !(witheredDomainResonium.Level != text) && !(witheredDomainResonium.Category != text2) && (text3.Length == 0 || witheredDomainResonium.Name == item))
					{
						list.Add(j);
					}
				}
			}
		}
		if (onlyPriority)
		{
			return list;
		}
		string[] array2 = new string[4]
		{
			"S",
			"A",
			"B",
			string.Empty
		};
		foreach (string text4 in array2)
		{
			for (int l = 0; l < items.Count; l++)
			{
				if (items[l].Level == text4 && !list.Contains(l))
				{
					list.Add(l);
				}
			}
		}
		return list;
	}

	private static IReadOnlyList<OcrMatchResult> ReadMerchantPrices(ZContext context, Mat screen, OneDragon.Core.Screen.ScreenArea area)
	{
		using Mat mat = CvImageUtils.Crop(screen, area.Rect);
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		Cv2.InRange(mat, new Scalar(240.0, 140.0, 0.0), new Scalar(255.0, 255.0, 50.0), mat2);
		using Mat mat4 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
		Cv2.Dilate(mat2, mat3, mat4);
		using Mat mat5 = new Mat();
		Cv2.BitwiseAnd(mat, mat, mat5, mat3);
		OcrMatchResult[] array = (from item in context.OcrService.GetOcrResultListForCrop(
			mat5,
			screen.Width,
			screen.Height,
			area.X1,
			area.Y1)
			where StringUtils.GetPositiveDigits(item.Text).HasValue
			select new OcrMatchResult(item.Confidence, item.X + area.Rect.X1, item.Y + area.Rect.Y1, item.Width, item.Height, item.Text)).ToArray();
		if (array.Length != 0)
		{
			return array;
		}
		List<OcrMatchResult> list = new List<OcrMatchResult>();
		for (int num = 2; num <= 3; num++)
		{
			for (int num2 = 1; num2 <= num; num2++)
			{
				OneDragon.Core.Screen.ScreenArea area2 = context.ScreenContext.GetArea("零号空洞-商店", $"商品价格-{num}-{num2}");
				if (area2 == null)
				{
					continue;
				}
				using Mat mat6 = CvImageUtils.Crop(screen, area2.Rect);
				using Mat mat7 = new Mat();
				using Mat mat8 = new Mat();
				Cv2.InRange(mat6, new Scalar(240.0, 140.0, 0.0), new Scalar(255.0, 255.0, 50.0), mat7);
				Cv2.Dilate(mat7, mat8, mat4);
				using Mat mat9 = new Mat();
				Cv2.CvtColor(mat8, mat9, ColorConversionCodes.GRAY2BGR);
				int? num3 = ParseMerchantFallbackPrice(context.OcrService.RunOcrSingleLineForCrop(
					mat9,
					screen.Width,
					screen.Height,
					area2.X1,
					area2.Y1));
				if (num3.HasValue)
				{
					list.Add(new OcrMatchResult(1.0, area2.Rect.X1, area2.Rect.Y1, area2.Rect.Width, area2.Rect.Height, num3.Value.ToString()));
				}
			}
		}
		return list;
	}

	internal static int? ParseMerchantFallbackPrice(string text)
	{
		return StringUtils.GetPositiveDigits(text.Replace('.', '0').Replace('。', '0').Replace('o', '0')
			.Replace('O', '0'));
	}

	private static async Task<HollowEventHandleResult> BackFromMerchantAsync(ZContext context, string eventName, CancellationToken cancellationToken)
	{
		if (!(await ClickAreaWithBaselineRetryAsync(context, "零号空洞-商店", "右上角-返回", cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return Failed(eventName, "点击商店返回失败");
		}
		await Task.Delay(TimeSpan.FromMilliseconds(1500L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!(await ClickAreaWithBaselineRetryAsync(context, "零号空洞-商店", "右上角-返回", cancellationToken).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return Failed(eventName, "点击商店返回失败");
		}
		await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Interacted, Success: true, "商店返回");
	}

	private static async Task<bool> ClickAreaWithBaselineRetryAsync(ZContext context, string screenName, string areaName, CancellationToken cancellationToken)
	{
		for (int retry = 0; retry <= 3; retry++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Mat screen = (context.Controller?.Screenshot() ?? (DateTimeOffset.UtcNow, null)).Screen;
			if (screen != null)
			{
				using (screen)
				{
					await Task.Delay(TimeSpan.FromMilliseconds(300L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (ScreenUtils.FindAndClickArea(context, screen, screenName, areaName) == OcrClickResultEnum.OcrClickSuccess)
					{
						return true;
					}
				}
			}
			if (retry < 3)
			{
				await Task.Delay(TimeSpan.FromSeconds(1L), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		return false;
	}

	private static OcrMatchResult? FindBestMutualMatch(IReadOnlyList<OcrMatchResult> results, IReadOnlyList<string> candidates)
	{
		string[] ocrTexts = (from result in results
			where result.Text.Trim().Length > 1
			select result.Text).ToArray();
		foreach (string candidate in candidates)
		{
			int? ocrIndex = StringUtils.FindBestMatchByDifflib(candidate, ocrTexts);
			if (!ocrIndex.HasValue)
			{
				continue;
			}
			int? num = StringUtils.FindBestMatchByDifflib(ocrTexts[ocrIndex.Value], candidates);
			if (!num.HasValue || !string.Equals(candidates[num.Value], candidate, StringComparison.Ordinal))
			{
				continue;
			}
			return results.First((OcrMatchResult result) => string.Equals(result.Text, ocrTexts[ocrIndex.Value], StringComparison.Ordinal));
		}
		return null;
	}

	private static HollowEventHandleResult Failed(string eventName, string message)
	{
		return new HollowEventHandleResult(eventName, HollowEventOutcomeKind.Unhandled, Success: false, message);
	}
}

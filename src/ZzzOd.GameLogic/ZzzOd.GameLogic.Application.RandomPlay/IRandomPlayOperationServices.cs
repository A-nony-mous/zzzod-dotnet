using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Operations;
using OpenCvSharp;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.RandomPlay;

/// <summary>
/// 录像店营业流程服务。
/// </summary>
public interface IRandomPlayOperationServices
{
	/// <summary>传送到营业入口。</summary>
	Task<OperationResult> TransportAsync(ZContext context, RandomPlayTransportPoint point);

	/// <summary>清空转向补偿待学习样本。</summary>
	void ClearPendingTurnSample();

	/// <summary>移动并交互。</summary>
	Task<OperationRoundResult> MoveAndInteractAsync(ZContext context, RandomPlayConfig config, Mat? screen);

	/// <summary>查找画面区域。</summary>
	bool IsAreaVisible(ZContext context, Mat? screen, string screenName, string areaName);

	/// <summary>查找并点击画面区域。</summary>
	OperationResult FindAndClickArea(ZContext context, Mat? screen, string screenName, string areaName);

	/// <summary>点击固定区域。</summary>
	OperationResult ClickArea(ZContext context, string screenName, string areaName, TimeSpan? preDelay = null);

	/// <summary>点击 OCR 文本。</summary>
	OperationResult ClickText(ZContext context, Mat? screen, string targetText, string screenName, string areaName);

	/// <summary>选择代理人。</summary>
	bool TrySelectAgent(ZContext context, Mat? screen, string agentName);

	/// <summary>滚动宣传员列表。</summary>
	void ScrollPromoterList(ZContext context);

	/// <summary>读取所需录像带主题。</summary>
	IReadOnlyList<string> ReadVideoThemes(ZContext context, Mat? screen);

	/// <summary>点击主题。</summary>
	OperationResult ClickTheme(ZContext context, Mat? screen, string theme);

	/// <summary>滚动主题列表。</summary>
	void ScrollThemeList(ZContext context);

	/// <summary>返回大世界。</summary>
	Task<OperationResult> BackToWorldAsync(ZContext context);
}

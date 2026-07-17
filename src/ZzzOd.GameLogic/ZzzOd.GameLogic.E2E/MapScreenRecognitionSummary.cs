using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 地图画面判断明细。
/// </summary>
public sealed class MapScreenRecognitionSummary
{
	/// <summary>当前识别到的画面名。</summary>
	public string? ActiveScreenName { get; set; }

	/// <summary>地图区域名匹配数量。</summary>
	public int AreaNameMatchCount { get; set; }

	/// <summary>传送点名称匹配数量。</summary>
	public int TransportPointNameMatchCount { get; set; }

	/// <summary>左上角返回区域识别结果。</summary>
	public string BackButtonResult { get; set; } = string.Empty;

	/// <summary>是否判定为地图页面。</summary>
	public bool IsMapScreen { get; set; }

	/// <summary>按位置排序后的 OCR 文本。</summary>
	public IReadOnlyList<string> OcrTexts { get; set; } = Array.Empty<string>();

	/// <summary>失败原因。</summary>
	public string? FailureReason { get; set; }
}

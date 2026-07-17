namespace ZzzOd.GameLogic.DebugData;

/// <summary>
/// ZZZ 业务调试数据类型。
/// </summary>
public enum ZzzDebugDataKind
{
	/// <summary>OCR 识别结果。</summary>
	Ocr,
	/// <summary>YOLO 检测结果。</summary>
	Yolo,
	/// <summary>路径选择结果。</summary>
	Path,
	/// <summary>动作决策结果。</summary>
	ActionDecision,
	/// <summary>性能采样。</summary>
	Performance
}

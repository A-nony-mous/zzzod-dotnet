namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 覆盖矩阵条目类型。
/// </summary>
public enum E2ECoverageArea
{
	/// <summary>业务应用。</summary>
	Application,
	/// <summary>Operation 流程。</summary>
	Operation,
	/// <summary>自动战斗。</summary>
	AutoBattle,
	/// <summary>HollowZero 和 LostVoid。</summary>
	HollowZero,
	/// <summary>输入控制。</summary>
	Input,
	/// <summary>截图捕获。</summary>
	Capture,
	/// <summary>OCR。</summary>
	Ocr,
	/// <summary>YOLO。</summary>
	Yolo,
	/// <summary>音频。</summary>
	Audio,
	/// <summary>第三方通知推送。</summary>
	NotificationPush
}

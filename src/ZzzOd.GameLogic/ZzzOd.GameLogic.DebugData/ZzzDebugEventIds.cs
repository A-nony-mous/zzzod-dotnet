namespace ZzzOd.GameLogic.DebugData;

/// <summary>
/// ZZZ 业务调试数据事件标识。
/// </summary>
public static class ZzzDebugEventIds
{
	/// <summary>全部业务调试数据。</summary>
	public const string All = "Zzz.Debug.Data";

	/// <summary>OCR 调试数据。</summary>
	public const string Ocr = "Zzz.Debug.Data.Ocr";

	/// <summary>YOLO 调试数据。</summary>
	public const string Yolo = "Zzz.Debug.Data.Yolo";

	/// <summary>路径调试数据。</summary>
	public const string Path = "Zzz.Debug.Data.Path";

	/// <summary>动作决策调试数据。</summary>
	public const string ActionDecision = "Zzz.Debug.Data.ActionDecision";

	/// <summary>性能采样调试数据。</summary>
	public const string Performance = "Zzz.Debug.Data.Performance";

	/// <summary>
	/// 根据数据类型获取对应事件标识。
	/// </summary>
	/// <param name="kind">调试数据类型。</param>
	/// <returns>事件标识。</returns>
	public static string ForKind(ZzzDebugDataKind kind)
	{
		if (1 == 0)
		{
		}
		string result = kind switch
		{
			ZzzDebugDataKind.Ocr => "Zzz.Debug.Data.Ocr", 
			ZzzDebugDataKind.Yolo => "Zzz.Debug.Data.Yolo", 
			ZzzDebugDataKind.Path => "Zzz.Debug.Data.Path", 
			ZzzDebugDataKind.ActionDecision => "Zzz.Debug.Data.ActionDecision", 
			ZzzDebugDataKind.Performance => "Zzz.Debug.Data.Performance", 
			_ => "Zzz.Debug.Data", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}

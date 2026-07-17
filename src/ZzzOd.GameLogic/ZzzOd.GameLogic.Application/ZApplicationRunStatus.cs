namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用运行记录状态，数值与 BaselineParity AppRunRecord 保持一致。
/// </summary>
public static class ZApplicationRunStatus
{
	/// <summary>等待运行。</summary>
	public const int Wait = 0;

	/// <summary>运行成功。</summary>
	public const int Success = 1;

	/// <summary>运行失败。</summary>
	public const int Fail = 2;

	/// <summary>运行中。</summary>
	public const int Running = 3;
}

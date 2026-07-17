using System.Collections.Generic;

namespace ZzzOd.GameLogic.Config;

public static class NotifyLifecycleModes
{
	public const string Off = "off";

	public const string FinishOnly = "finish_only";

	public const string StartAndFinish = "start_and_finish";

	public static IReadOnlyList<string> All { get; } = new string[3] { "off", "finish_only", "start_and_finish" };
}

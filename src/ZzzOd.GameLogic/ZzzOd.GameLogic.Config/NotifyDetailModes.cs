using System.Collections.Generic;

namespace ZzzOd.GameLogic.Config;

public static class NotifyDetailModes
{
	public const string Off = "off";

	public const string ErrorOnly = "error_only";

	public const string All = "all";

	public const string Merge = "merge";

	public static IReadOnlyList<string> Values { get; } = new string[4] { "off", "error_only", "all", "merge" };
}

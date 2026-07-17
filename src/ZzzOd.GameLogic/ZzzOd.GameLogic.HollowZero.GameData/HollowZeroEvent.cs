using System.Collections.Generic;

namespace ZzzOd.GameLogic.HollowZero.GameData;

public class HollowZeroEvent
{
	public string EventName { get; set; }

	public string? EntryName { get; set; }

	public List<HollowZeroNormalEventOption> Options { get; set; }

	public float LcsPercent { get; set; }

	public bool OnTheRight { get; set; }

	public bool IsEntryOpt { get; set; }

	public HollowZeroEvent(string eventName, string? entryName = null, List<HollowZeroNormalEventOption>? options = null, float lcsPercent = 1f, bool onTheRight = false, bool isEntryOpt = false)
	{
		EventName = eventName;
		EntryName = entryName;
		Options = options ?? new List<HollowZeroNormalEventOption>();
		LcsPercent = lcsPercent;
		OnTheRight = onTheRight;
		IsEntryOpt = isEntryOpt;
	}
}

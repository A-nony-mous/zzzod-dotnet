namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地入口。
/// </summary>
public sealed class WorldPatrolEntry
{
	/// <summary>入口名称。</summary>
	public string EntryName { get; }

	/// <summary>入口 id。</summary>
	public string EntryId { get; }

	/// <summary>
	/// 初始化入口。
	/// </summary>
	public WorldPatrolEntry(string entryName, string entryId)
	{
		EntryName = entryName;
		EntryId = entryId;
	}
}

namespace ZzzOd.GameLogic.HollowZero.GameData;

public class HollowZeroEntry
{
	public string EntryId { get; set; }

	public string EntryName { get; set; }

	public bool IsBenefit { get; set; }

	public int NeedStep { get; set; }

	public bool IsBase { get; set; }

	public bool CanGo { get; set; }

	public bool IsTp { get; set; }

	public bool MoveAfterwards { get; set; }

	public int CanVisitedTimes { get; set; }

	public HollowZeroEntry(string entryName, bool isBenefit = true, int needStep = 1, bool isBase = false, bool canGo = true, bool isTp = false, bool moveAfterwards = false, int canVisitedTimes = 2)
	{
		if (entryName.Length >= 4)
		{
			EntryId = entryName.Substring(0, 4);
			EntryName = ((entryName.Length > 5) ? entryName.Substring(5) : string.Empty);
		}
		else
		{
			EntryId = entryName;
			EntryName = string.Empty;
		}
		IsBenefit = isBenefit;
		NeedStep = needStep;
		IsBase = isBase;
		CanGo = canGo;
		IsTp = isTp;
		MoveAfterwards = moveAfterwards;
		CanVisitedTimes = canVisitedTimes;
	}
}

namespace ZzzOd.GameLogic.HollowZero;

public class HollowLevelInfo
{
	public string? MissionTypeName { get; set; }

	public string? MissionName { get; set; }

	public int Level { get; set; }

	public int Phase { get; set; }

	public HollowLevelInfo(string? missionTypeName = null, string? missionName = null, int level = -1, int phase = -1)
	{
		MissionTypeName = missionTypeName;
		MissionName = missionName;
		Level = level;
		Phase = phase;
	}

	public bool IsMissionType(string missionTypeName, int level)
	{
		return MissionTypeName != null && MissionTypeName == missionTypeName && Level == level;
	}

	public void ToNextLevel()
	{
		if (Level != -1)
		{
			Level++;
			Phase = 1;
		}
	}

	public void ToNextPhase()
	{
		if (Phase != -1)
		{
			Phase++;
		}
	}
}

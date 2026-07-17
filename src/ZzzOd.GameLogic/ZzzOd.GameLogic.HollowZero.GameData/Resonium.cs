namespace ZzzOd.GameLogic.HollowZero.GameData;

public class Resonium
{
	public string Category { get; set; }

	public string Name { get; set; }

	public string Level { get; set; }

	public Resonium(string category, string name, string level)
	{
		Category = category;
		Name = name;
		Level = level;
	}
}

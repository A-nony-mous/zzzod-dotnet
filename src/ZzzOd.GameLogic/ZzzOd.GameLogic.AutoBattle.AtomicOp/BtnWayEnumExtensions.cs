namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public static class BtnWayEnumExtensions
{
	public static BtnWayEnum FromValue(string? value)
	{
		if (1 == 0)
		{
		}
		BtnWayEnum result = value switch
		{
			"按下" => BtnWayEnum.Press, 
			"松开" => BtnWayEnum.Release, 
			"点按" => BtnWayEnum.Tap, 
			_ => BtnWayEnum.None, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}

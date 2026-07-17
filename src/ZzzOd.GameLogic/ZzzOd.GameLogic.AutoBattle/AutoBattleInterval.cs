using System;

namespace ZzzOd.GameLogic.AutoBattle;

public readonly record struct AutoBattleInterval(float Start, float End)
{
	public float NextValue()
	{
		return (Start == End) ? Start : (Start + (End - Start) * Random.Shared.NextSingle());
	}
}

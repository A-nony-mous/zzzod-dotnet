using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicTurn : AutoBattleAtomicOp
{
	public float TurnX { get; }

	public AtomicTurn(AutoBattleContext context, float turnX)
		: base(context, "转向", new OperationDef())
	{
		TurnX = turnX;
	}

	public override void Execute()
	{
		base.Context.TurnByDistance(TurnX);
	}
}

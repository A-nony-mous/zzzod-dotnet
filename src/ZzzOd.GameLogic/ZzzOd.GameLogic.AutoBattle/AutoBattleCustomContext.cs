using System.Collections.Generic;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class AutoBattleCustomContext
{
	private readonly AutoBattleContext _autoBattleContext;

	public AutoBattleCustomContext(AutoBattleContext autoBattleContext)
	{
		_autoBattleContext = autoBattleContext;
	}

	public void SetState(IReadOnlyList<string> stateNameList, double timeDiff = 0.0, double timeDiffAdd = 0.0, int? value = null, int? valueAdd = null)
	{
		_autoBattleContext.SetCustomState(stateNameList, timeDiff, timeDiffAdd, value, valueAdd);
	}

	public void ClearState(IReadOnlyList<string> stateNameList)
	{
		_autoBattleContext.ClearCustomState(stateNameList);
	}
}

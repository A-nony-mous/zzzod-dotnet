using System;
using System.Collections.Generic;
using System.Globalization;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public sealed class AtomicOpFactory
{
	private const string PressSuffix = "-按下";

	private const string ReleaseSuffix = "-松开";

	private readonly AutoBattleContext? _context;

	public AtomicOpFactory(AutoBattleContext? context = null)
	{
		_context = context;
	}

	public OneDragon.Core.Operation.AtomicOp GetAtomicOp(OperationDef operationDef)
	{
		ArgumentNullException.ThrowIfNull(operationDef, "operationDef");
		string text = operationDef.OpName ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentException("非法的指令 ", "operationDef");
		}
		bool press = text.EndsWith("-按下", StringComparison.Ordinal);
		bool release = text.EndsWith("-松开", StringComparison.Ordinal);
		double? pressTimeSeconds = ResolvePressTime(operationDef, press);
		if (text == "按键-切换角色" || text == "切换角色")
		{
			return new AtomicBtnSwitchAgent(_context, operationDef);
		}
		if (text == "按键-快速支援")
		{
			return new AtomicBtnQuickAssist(_context, operationDef);
		}
		if (text.StartsWith("按键", StringComparison.Ordinal) && !text.EndsWith("按下", StringComparison.Ordinal) && !text.EndsWith("松开", StringComparison.Ordinal))
		{
			return new AtomicBtnCommon(_context, operationDef);
		}
		if (text.StartsWith(BattleStateEnum.BtnDodge.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnDodge(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnSwitchNext.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnSwitchNext(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnSwitchPrev.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnSwitchPrev(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnSwitchBackup.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnSwitchBackup(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnSwitchNormalAttack.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnNormalAttack(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnSwitchSpecialAttack.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnSpecialAttack(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnUltimate.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnUltimate(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnChainLeft.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnChainLeft(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnChainRight.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnChainRight(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnChainCancel.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnChainCancel(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnMoveW.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnMoveW(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnMoveS.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnMoveS(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnMoveA.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnMoveA(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnMoveD.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnMoveD(_context, operationDef, press, pressTimeSeconds, release);
		}
		if (text.StartsWith(BattleStateEnum.BtnLock.GetDescription(), StringComparison.Ordinal))
		{
			return new AtomicBtnLock(_context, operationDef, press, pressTimeSeconds, release);
		}
		return text switch
		{
			"等待秒数" => new AtomicWait(operationDef), 
			"设置状态" => new AtomicSetState(_context, operationDef), 
			"清除状态" => new AtomicClearState(_context, operationDef), 
			_ => throw new ArgumentException("非法的指令 " + text, "operationDef"), 
		};
	}

	private static double? ResolvePressTime(OperationDef operationDef, bool press)
	{
		if (!press)
		{
			return null;
		}
		if (operationDef.BtnPress.HasValue)
		{
			return operationDef.BtnPress.Value;
		}
		IReadOnlyList<string> data = operationDef.Data;
		if (data != null && data.Count > 0 && double.TryParse(operationDef.Data[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}
}

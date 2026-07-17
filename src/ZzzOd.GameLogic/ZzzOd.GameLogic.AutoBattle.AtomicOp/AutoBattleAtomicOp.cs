using System;
using System.Globalization;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle.AtomicOp;

public abstract class AutoBattleAtomicOp : OneDragon.Core.Operation.AtomicOp
{
	private readonly AutoBattleContext? _context;

	public OperationDef OperationDef { get; }

	public double PreDelay { get; }

	public double PostDelay { get; }

	protected AutoBattleContext Context => _context ?? throw new InvalidOperationException("AutoBattleContext is required to execute this atomic operation.");

	protected AutoBattleAtomicOp(AutoBattleContext? context, string opName, OperationDef operationDef, bool asyncOp = false)
		: base(opName, asyncOp)
	{
		_context = context;
		OperationDef = operationDef;
		PreDelay = operationDef.PreDelay;
		PostDelay = operationDef.PostDelay;
	}

	protected static double? ParseDouble(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		double result;
		return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? new double?(result) : ((double?)null);
	}

	protected static TimeSpan? ToTimeSpan(double? seconds)
	{
		return seconds.HasValue ? new TimeSpan?(TimeSpan.FromSeconds(seconds.Value)) : ((TimeSpan?)null);
	}
}

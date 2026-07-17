using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class AutoBattleCondOpScene
{
	public Dictionary<string, object?> OriginalData { get; }

	public int? Priority { get; }

	public IReadOnlyList<string> Triggers { get; }

	public double IntervalSeconds { get; }

	public IReadOnlyList<AutoBattleCondOpStateHandler> Handlers { get; private set; }

	public HashSet<string> UsageStates
	{
		get
		{
			HashSet<string> hashSet = new HashSet<string>(Triggers, StringComparer.Ordinal);
			foreach (AutoBattleCondOpStateHandler handler in Handlers)
			{
				hashSet.UnionWith(handler.UsageStates);
			}
			return hashSet;
		}
	}

	public AutoBattleCondOpScene(IReadOnlyDictionary<string, object?> data)
	{
		OriginalData = new Dictionary<string, object>(data, StringComparer.Ordinal);
		Priority = GetNullableInt(data, "priority");
		Triggers = GetStringList(data, "triggers") ?? Array.Empty<string>();
		IntervalSeconds = GetDouble(data, "interval", 0.5);
		Handlers = (from handler in GetDictionaryList(data, "handlers")
			select new AutoBattleCondOpStateHandler(handler)).ToList();
	}

	public void SetHandlers(IReadOnlyList<AutoBattleCondOpStateHandler> handlers)
	{
		Handlers = handlers.ToList();
	}

	public void Build(Func<string, StateRecorder?> stateRecorderGetter, Func<OperationDef, OneDragon.Core.Operation.AtomicOp> atomicOpGetter)
	{
		foreach (AutoBattleCondOpStateHandler handler in Handlers)
		{
			handler.Build(stateRecorderGetter, atomicOpGetter);
		}
	}

	public ExecutionInfo? MatchExecution(double triggerTime)
	{
		foreach (AutoBattleCondOpStateHandler handler in Handlers)
		{
			ExecutionInfo executionInfo = handler.MatchExecution(triggerTime);
			if (executionInfo != null)
			{
				return executionInfo;
			}
		}
		return null;
	}

	internal static List<Dictionary<string, object?>> GetDictionaryList(IReadOnlyDictionary<string, object?> data, string key)
	{
		if (!data.TryGetValue(key, out object value) || value == null)
		{
			return new List<Dictionary<string, object>>();
		}
		if (value is IEnumerable enumerable && !(enumerable is string))
		{
			List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
			foreach (object item in enumerable)
			{
				if (item is IReadOnlyDictionary<string, object> collection)
				{
					list.Add(new Dictionary<string, object>(collection, StringComparer.Ordinal));
				}
			}
			return list;
		}
		return new List<Dictionary<string, object>>();
	}

	internal static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object?> data, string key)
	{
		if (!data.TryGetValue(key, out object value) || value == null)
		{
			return null;
		}
		if (value is string item)
		{
			return new string[] { item };
		}
		if (value is IEnumerable enumerable)
		{
			List<string> list = new List<string>();
			foreach (object item2 in enumerable)
			{
				if (item2 != null)
				{
					list.Add(Convert.ToString(item2, CultureInfo.InvariantCulture) ?? string.Empty);
				}
			}
			return list;
		}
		return new string[] { Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty };
	}

	internal static string? GetString(IReadOnlyDictionary<string, object?> data, string key)
	{
		object value;
		return (data.TryGetValue(key, out value) && value != null) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
	}

	internal static int? GetNullableInt(IReadOnlyDictionary<string, object?> data, string key)
	{
		if (!data.TryGetValue(key, out object value) || value == null)
		{
			return null;
		}
		if (value is int value2)
		{
			return value2;
		}
		if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}

	internal static double GetDouble(IReadOnlyDictionary<string, object?> data, string key, double fallback)
	{
		if (!data.TryGetValue(key, out object value) || value == null)
		{
			return fallback;
		}
		if (1 == 0)
		{
		}
		double result2;
		double result = ((value is double num) ? num : ((value is float num2) ? ((double)num2) : ((value is decimal num3) ? ((double)num3) : ((value is int num4) ? ((double)num4) : ((!(value is long num5)) ? (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result2) ? result2 : fallback) : ((double)num5))))));
		if (1 == 0)
		{
		}
		return result;
	}
}

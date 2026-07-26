using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Operation;

namespace ZzzOd.GameLogic.AutoBattle;

public sealed class AutoBattleCondOpStateHandler
{
	private static readonly HashSet<string> KnownKeys = new HashSet<string>(StringComparer.Ordinal)
	{
		"state_template",
		"debug_name",
		"states",
		"interrupt_states",
		"operations",
		"sub_handlers",
	};

	public Dictionary<string, object?> OriginalData { get; }

	public string? StateTemplate { get; }

	public string? DisplayName { get; }

	public string States { get; }

	public string? InterruptStates { get; }

	public IReadOnlyList<OperationDef> Operations { get; private set; }

	public IReadOnlyList<AutoBattleCondOpStateHandler> SubHandlers { get; private set; }

	public IReadOnlyList<OneDragon.Core.Operation.AtomicOp> OpList { get; private set; } = Array.Empty<OneDragon.Core.Operation.AtomicOp>();

	public StateCalNode? StateCalTree { get; private set; }

	public StateCalNode? InterruptStatesCalTree { get; private set; }

	public HashSet<string> UsageStates
	{
		get
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			if (StateCalTree != null)
			{
				hashSet.UnionWith(StateCalTree.UsageStates);
			}
			if (InterruptStatesCalTree != null)
			{
				hashSet.UnionWith(InterruptStatesCalTree.UsageStates);
			}
			foreach (AutoBattleCondOpStateHandler subHandler in SubHandlers)
			{
				hashSet.UnionWith(subHandler.UsageStates);
			}
			return hashSet;
		}
	}

	public AutoBattleCondOpStateHandler(IReadOnlyDictionary<string, object?> data)
	{
		AutoBattleCondOpScene.ValidateKnownKeys(data, KnownKeys);
		OriginalData = new Dictionary<string, object>(data, StringComparer.Ordinal);
		StateTemplate = AutoBattleCondOpScene.GetString(data, "state_template");
		DisplayName = AutoBattleCondOpScene.GetString(data, "debug_name");
		States = AutoBattleCondOpScene.GetString(data, "states") ?? string.Empty;
		InterruptStates = AutoBattleCondOpScene.GetString(data, "interrupt_states");
		Operations = (from operation in AutoBattleCondOpScene.GetDictionaryList(data, "operations")
			select new OperationDef(operation)).ToList();
		SubHandlers = (from handler in AutoBattleCondOpScene.GetDictionaryList(data, "sub_handlers")
			select new AutoBattleCondOpStateHandler(handler)).ToList();
	}

	public void SetSubHandlers(IReadOnlyList<AutoBattleCondOpStateHandler> subHandlers)
	{
		SubHandlers = subHandlers.ToList();
	}

	public void SetOperations(IReadOnlyList<OperationDef> operations)
	{
		Operations = operations.ToList();
	}

	public void Build(Func<string, StateRecorder?> stateRecorderGetter, Func<OperationDef, OneDragon.Core.Operation.AtomicOp> atomicOpGetter, StateCalNode? parentInterruptStatesCalTree = null)
	{
		StateCalTree = StateCalExpressionParser.Construct(States, stateRecorderGetter);
		InterruptStatesCalTree = BuildInterruptTree(stateRecorderGetter, parentInterruptStatesCalTree);
		if (SubHandlers.Count > 0)
		{
			foreach (AutoBattleCondOpStateHandler subHandler in SubHandlers)
			{
				subHandler.Build(stateRecorderGetter, atomicOpGetter, InterruptStatesCalTree);
			}
			return;
		}
		if (Operations.Count > 0)
		{
			OpList = Operations.Select(atomicOpGetter).ToList();
		}
	}

	public ExecutionInfo? MatchExecution(double triggerTime)
	{
		StateCalNode? stateCalTree = StateCalTree;
		if (stateCalTree == null || !stateCalTree.InTimeRange(triggerTime))
		{
			return null;
		}
		if (SubHandlers.Count > 0)
		{
			foreach (AutoBattleCondOpStateHandler subHandler in SubHandlers)
			{
				ExecutionInfo executionInfo = subHandler.MatchExecution(triggerTime);
				if (executionInfo != null)
				{
					executionInfo.AddState(States, DisplayName);
					return executionInfo;
				}
			}
			return null;
		}
		ExecutionInfo executionInfo2 = new ExecutionInfo(OpList.ToList(), InterruptStatesCalTree);
		executionInfo2.AddState(States, DisplayName);
		return executionInfo2;
	}

	private StateCalNode? BuildInterruptTree(Func<string, StateRecorder?> stateRecorderGetter, StateCalNode? parentTree)
	{
		StateCalNode stateCalNode = ((InterruptStates != null) ? StateCalExpressionParser.Construct(InterruptStates, stateRecorderGetter) : null);
		if (parentTree == null)
		{
			return stateCalNode;
		}
		if (stateCalNode == null)
		{
			return parentTree;
		}
		return new StateCalNode(StateCalNodeType.OP, StateCalOpType.OR, parentTree, stateCalNode);
	}
}

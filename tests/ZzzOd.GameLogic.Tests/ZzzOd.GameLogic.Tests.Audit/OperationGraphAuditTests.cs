using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OneDragon.Core.Abstractions.Operations;
using Xunit;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// 节点图结构不变量审计：同一来源节点在同一成败方向上最多只允许一条忽略状态的兜底边，
/// 否则解析结果取决于反射枚举顺序而非业务分支。
/// 连线集合按运行时建图语义计算：最派生层重新声明的连线整体替换基类声明，未重声明才回退继承。
/// </summary>
[Trait("Category", "Audit")]
public sealed class OperationGraphAuditTests
{
	private static IReadOnlyList<NodeFromAttribute> GetEffectiveEdges(MethodInfo method)
	{
		NodeFromAttribute[] declared = method.GetCustomAttributes<NodeFromAttribute>(inherit: false).ToArray();
		return declared.Length > 0 ? declared : method.GetCustomAttributes<NodeFromAttribute>(inherit: true).ToArray();
	}

	[Fact]
	public void EveryOperationDeclaresAtMostOneStatusIgnoringEdgePerSourceNode()
	{
		List<string> violations = new List<string>();
		Assembly assembly = typeof(ZOperation).Assembly;
		foreach (Type type in assembly.GetTypes())
		{
			if (type.IsAbstract || !typeof(ZOperation).IsAssignableFrom(type))
			{
				continue;
			}
			IReadOnlyList<(string NodeName, MethodInfo Method)> nodeMethods = type
				.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Select(method => (Node: method.GetCustomAttribute<OperationNodeAttribute>(), Method: method))
				.Where(pair => pair.Node != null)
				.Select(pair => (pair.Node!.Name, pair.Method))
				.ToArray();
			foreach ((string nodeName, MethodInfo method) in nodeMethods)
			{
				IEnumerable<IGrouping<(string FromName, bool Success), NodeFromAttribute>> groups = GetEffectiveEdges(method)
					.Where(edge => edge.Status == null)
					.GroupBy(edge => (edge.FromName, edge.Success));
				foreach (IGrouping<(string FromName, bool Success), NodeFromAttribute> group in groups)
				{
					if (group.Count() > 1)
					{
						violations.Add($"{type.Name}.{nodeName} 从「{group.Key.FromName}」(Success={group.Key.Success}) 声明了 {group.Count()} 条忽略状态的边");
					}
				}
			}
			// 跨方法：同一来源节点的忽略状态兜底边指向多个不同节点同样构成歧义
			IEnumerable<IGrouping<(string FromName, bool Success), string>> crossGroups = nodeMethods
				.SelectMany(pair => GetEffectiveEdges(pair.Method)
					.Where(edge => edge.Status == null)
					.Select(edge => (Edge: edge, Target: pair.NodeName)))
				.GroupBy(pair => (pair.Edge.FromName, pair.Edge.Success), pair => pair.Target);
			foreach (IGrouping<(string FromName, bool Success), string> group in crossGroups)
			{
				string[] targets = group.Distinct(StringComparer.Ordinal).ToArray();
				if (targets.Length > 1)
				{
					violations.Add($"{type.Name} 从「{group.Key.FromName}」(Success={group.Key.Success}) 的忽略状态兜底边指向多个节点：{string.Join("/", targets)}");
				}
			}
		}
		Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
	}
}

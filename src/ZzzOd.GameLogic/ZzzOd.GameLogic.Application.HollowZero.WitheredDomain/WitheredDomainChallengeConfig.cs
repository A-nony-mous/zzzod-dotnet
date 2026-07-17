using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都挑战配置。
/// </summary>
public sealed class WitheredDomainChallengeConfig
{
	[YamlMember(Alias = "auto_battle", ApplyNamingConventions = false)]
	public string AutoBattle { get; set; } = "全配队通用";

	[YamlMember(Alias = "resonium_priority", ApplyNamingConventions = false)]
	public List<string> ResoniumPriority { get; set; } = new List<string>();

	[YamlMember(Alias = "event_priority", ApplyNamingConventions = false)]
	public List<string> EventPriority { get; set; } = new List<string>();

	[YamlMember(Alias = "target_agents", ApplyNamingConventions = false)]
	public List<string?> TargetAgents { get; set; }

	[YamlMember(Alias = "path_finding", ApplyNamingConventions = false)]
	public string PathFinding { get; set; }

	[YamlMember(Alias = "go_in_1_step", ApplyNamingConventions = false)]
	public List<string> GoInOneStep { get; set; }

	[YamlMember(Alias = "waypoint", ApplyNamingConventions = false)]
	public List<string> Waypoint { get; set; }

	[YamlMember(Alias = "avoid", ApplyNamingConventions = false)]
	public List<string> Avoid { get; set; }

	[YamlMember(Alias = "buy_only_priority", ApplyNamingConventions = false)]
	public bool BuyOnlyPriority { get; set; }

	public WitheredDomainChallengeConfig Clone()
	{
		return new WitheredDomainChallengeConfig
		{
			AutoBattle = AutoBattle,
			ResoniumPriority = ResoniumPriority.ToList(),
			EventPriority = EventPriority.ToList(),
			TargetAgents = TargetAgents.ToList(),
			PathFinding = PathFinding,
			GoInOneStep = GoInOneStep.ToList(),
			Waypoint = Waypoint.ToList(),
			Avoid = Avoid.ToList(),
			BuyOnlyPriority = BuyOnlyPriority
		};
	}

	public WitheredDomainChallengeConfig()
	{
		int num = 3;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = null;
		span[1] = null;
		span[2] = null;
		TargetAgents = list;
		PathFinding = "默认";
		GoInOneStep = new List<string>();
		Waypoint = new List<string>();
		Avoid = new List<string>();
		BuyOnlyPriority = true;
	}
}

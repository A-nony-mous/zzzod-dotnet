using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;
using ZzzOd.GameLogic.GameData;

namespace ZzzOd.GameLogic.Application.ShiyuDefense;

/// <summary>
/// 式舆防卫战应用配置。
/// </summary>
public sealed class ShiyuDefenseConfig : ZApplicationConfig, IApplicationConfig
{
	private Action? _save;

	private int _criticalMaxNodeIndex = 7;

	[YamlMember(Alias = "team_list", ApplyNamingConventions = false)]
	public List<ShiyuDefenseTeamConfig> TeamList { get; set; } = new List<ShiyuDefenseTeamConfig>();

	[YamlMember(Alias = "critical_max_node_idx", ApplyNamingConventions = false)]
	public int CriticalMaxNodeIndex
	{
		get
		{
			return _criticalMaxNodeIndex;
		}
		set
		{
			if (_criticalMaxNodeIndex != value)
			{
				_criticalMaxNodeIndex = value;
				Save();
			}
		}
	}

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static ShiyuDefenseConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<ShiyuDefenseConfig> yamlConfig = new YamlConfig<ShiyuDefenseConfig>(environment, "shiyu_defense", null, instanceIndex, new string[2] { "app_config", groupId });
		ShiyuDefenseConfig current = yamlConfig.Current;
		current.ConfigureRuntime("shiyu_defense", instanceIndex, groupId);
		current.ConfigurePersistence(delegate
		{
			yamlConfig.Save();
		});
		return current;
	}

	/// <summary>
	/// 按预备编队下标获取配置，缺失时创建。
	/// </summary>
	public ShiyuDefenseTeamConfig GetConfigByTeamIndex(int teamIndex)
	{
		ShiyuDefenseTeamConfig shiyuDefenseTeamConfig = TeamList.FirstOrDefault((ShiyuDefenseTeamConfig item) => item.TeamIndex == teamIndex);
		if (shiyuDefenseTeamConfig != null)
		{
			return shiyuDefenseTeamConfig;
		}
		shiyuDefenseTeamConfig = new ShiyuDefenseTeamConfig
		{
			TeamIndex = teamIndex
		};
		TeamList.Add(shiyuDefenseTeamConfig);
		return shiyuDefenseTeamConfig;
	}

	/// <summary>
	/// 增加一个队伍弱点属性。
	/// </summary>
	public bool AddWeakness(int teamIndex, DmgTypeEnum dmgType)
	{
		ShiyuDefenseTeamConfig configByTeamIndex = GetConfigByTeamIndex(teamIndex);
		List<DmgTypeEnum> weaknessList = configByTeamIndex.WeaknessList;
		if (weaknessList.Contains(dmgType))
		{
			return false;
		}
		weaknessList.Add(dmgType);
		configByTeamIndex.WeaknessList = weaknessList;
		Save();
		return true;
	}

	/// <summary>
	/// 移除一个队伍弱点属性。
	/// </summary>
	public bool RemoveWeakness(int teamIndex, DmgTypeEnum dmgType)
	{
		ShiyuDefenseTeamConfig configByTeamIndex = GetConfigByTeamIndex(teamIndex);
		List<DmgTypeEnum> weaknessList = configByTeamIndex.WeaknessList;
		if (!weaknessList.Remove(dmgType))
		{
			return false;
		}
		configByTeamIndex.WeaknessList = weaknessList;
		Save();
		return true;
	}

	/// <summary>
	/// 修改是否参与剧变节点。
	/// </summary>
	public bool ChangeForCritical(int teamIndex, bool forCritical)
	{
		ShiyuDefenseTeamConfig configByTeamIndex = GetConfigByTeamIndex(teamIndex);
		if (configByTeamIndex.ForCritical == forCritical)
		{
			return false;
		}
		configByTeamIndex.ForCritical = forCritical;
		Save();
		return true;
	}

	private void ConfigurePersistence(Action save)
	{
		_save = save;
	}

	private void Save()
	{
		_save?.Invoke();
	}
}

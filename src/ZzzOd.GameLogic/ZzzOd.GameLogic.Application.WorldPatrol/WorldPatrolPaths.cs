using System.IO;
using OneDragon.Core.Runtime;
using ZzzOd.GameLogic.Const;

namespace ZzzOd.GameLogic.Application.WorldPatrol;

/// <summary>
/// 锄大地资源路径。
/// </summary>
public static class WorldPatrolPaths
{
	/// <summary>锄大地数据目录。</summary>
	public static string WorldPatrolDirectory(OneDragonEnvironment environment)
	{
		return Path.Combine(GameConst.GetGameDataPath(environment), "world_patrol");
	}

	/// <summary>入口数据目录。</summary>
	public static string EntryDirectory(OneDragonEnvironment environment, WorldPatrolEntry entry)
	{
		return Path.Combine(WorldPatrolDirectory(environment), entry.EntryId);
	}

	/// <summary>区域数据目录。</summary>
	public static string AreaDirectory(OneDragonEnvironment environment, WorldPatrolArea area)
	{
		return Path.Combine(EntryDirectory(environment, area.Entry), area.FullId);
	}

	/// <summary>道路掩码图片路径。</summary>
	public static string RoadMaskPath(OneDragonEnvironment environment, WorldPatrolArea area)
	{
		return Path.Combine(AreaDirectory(environment, area), "road_mask.png");
	}

	/// <summary>图标 YAML 路径。</summary>
	public static string IconYamlPath(OneDragonEnvironment environment, WorldPatrolArea area)
	{
		return Path.Combine(AreaDirectory(environment, area), "icon.yml");
	}
}

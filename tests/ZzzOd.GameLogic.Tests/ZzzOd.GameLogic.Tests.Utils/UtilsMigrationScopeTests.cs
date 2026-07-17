using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Utils;

public sealed class UtilsMigrationScopeTests
{
	[Fact]
	public void ZzzOdUtilsPackage_HasNoStandaloneBusinessHelpers()
	{
		string text = FindRepoRoot();
		string[] buffer = new string[5];
		buffer[0] = text;
		buffer[1] = "ZenlessZoneZero-OneDragon";
		buffer[2] = "src";
		buffer[3] = "zzz_od";
		buffer[4] = "utils";
		string utilsDirectory = Path.Combine(buffer);
		string[] actualArray = (from path in Directory.GetFiles(utilsDirectory, "*", SearchOption.AllDirectories)
			select Path.GetRelativePath(utilsDirectory, path).Replace('\\', '/')).Order<string>(StringComparer.Ordinal).ToArray();
		Assert.Equal(new ReadOnlySpan<string>("__init__.py"), actualArray);
		Assert.Equal(0L, new FileInfo(Path.Combine(utilsDirectory, "__init__.py")).Length);
	}

	[Fact]
	public void DistributedUtilityModules_MatchCurrentPythonSourceInventory()
	{
		string path = FindRepoRoot();
		string zzzOdDirectory = Path.Combine(path, "ZenlessZoneZero-OneDragon", "src", "zzz_od");
		string[] actualArray = (from path2 in Directory.GetFiles(zzzOdDirectory, "*_utils.py", SearchOption.AllDirectories)
			select Path.GetRelativePath(zzzOdDirectory, path2).Replace('\\', '/')).Order<string>(StringComparer.Ordinal).ToArray();
		string[] buffer = new string[9];
		buffer[0] = "application/devtools/large_map_recorder/large_map_recorder_utils.py";
		buffer[1] = "application/devtools/large_map_recorder/map_icon_utils.py";
		buffer[2] = "application/shiyu_defense/shiyu_defense_team_utils.py";
		buffer[3] = "application/world_patrol/cal_pos_utils.py";
		buffer[4] = "auto_battle/auto_battle_utils.py";
		buffer[5] = "auto_battle/build_utils.py";
		buffer[6] = "hollow_zero/event/hollow_event_utils.py";
		buffer[7] = "hollow_zero/event/resonium_utils.py";
		buffer[8] = "hollow_zero/hollow_map/hollow_map_utils.py";
		Assert.Equal(buffer, actualArray);
	}

	private static string FindRepoRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			bool flag = Directory.Exists(Path.Combine(directoryInfo.FullName, "openspec"));
			bool flag2 = Directory.Exists(Path.Combine(directoryInfo.FullName, "ZenlessZoneZero-OneDragon"));
			if (flag && flag2)
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("Cannot find repository root.");
	}
}

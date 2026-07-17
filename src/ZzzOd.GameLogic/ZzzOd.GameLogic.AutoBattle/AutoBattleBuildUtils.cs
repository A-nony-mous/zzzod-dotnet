using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.AutoBattle;

public static class AutoBattleBuildUtils
{
	public const string DefaultAutoBattleSubDir = "auto_battle";

	public static IReadOnlyList<AutoBattleMergeBuildRequest> CreateMergeBuildRequests(IEnumerable<string> templateNames)
	{
		List<AutoBattleMergeBuildRequest> list = new List<AutoBattleMergeBuildRequest>();
		foreach (string templateName in templateNames)
		{
			if (!string.IsNullOrWhiteSpace(templateName))
			{
				list.Add(new AutoBattleMergeBuildRequest("auto_battle", templateName, ReadFromMerged: false));
			}
		}
		return list;
	}

	public static int BuildAllMerge(IEnumerable<string> templateNames, Func<AutoBattleMergeBuildRequest, IAutoBattleMergeBuilder> builderFactory)
	{
		int num = 0;
		foreach (AutoBattleMergeBuildRequest item in CreateMergeBuildRequests(templateNames))
		{
			IAutoBattleMergeBuilder autoBattleMergeBuilder = builderFactory(item);
			autoBattleMergeBuilder.Load();
			autoBattleMergeBuilder.SaveAsOneFile();
			num++;
		}
		return num;
	}
}

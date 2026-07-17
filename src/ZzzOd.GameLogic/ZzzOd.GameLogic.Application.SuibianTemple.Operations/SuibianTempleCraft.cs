using System.Collections.Generic;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

public sealed class SuibianTempleCraft(ZContext context, SuibianTempleConfig config) : SuibianTempleSubOperation(context, config, "随便观 制造")
{
	private readonly List<string> _chosenItems = new List<string>();

	[OperationNode("前往制造", IsStartNode = true)]
	public OperationRoundResult GoToCraft()
	{
		return GoToScreenByText("随便观-制造坊", "经营", "制造");
	}

	[NodeFrom("前往制造")]
	[NodeFrom("制造派驻", Status = "已派驻")]
	[OperationNode("点击开工")]
	public OperationRoundResult ClickLetsGo()
	{
		IReadOnlyList<string> texts = new string[3] { "开工", "制造暂停", "开物" };
		IReadOnlyList<string> ignoreTexts = new string[] { "开物" };
		return ClickTextByPriority(texts, null, null, ignoreTexts);
	}

	[NodeFrom("点击开工")]
	[OperationNode("制造派驻")]
	public OperationRoundResult CraftDispatch()
	{
		OperationResult result = new SuibianTempleCraftDispatch(base.ZContext, base.Config, fromCraft: true, _chosenItems).ExecuteAsync().GetAwaiter().GetResult();
		OperationRoundResult result2;
		if (result.IsSuccess)
		{
			object data = result.Data;
			if (data is bool && (bool)data)
			{
				result2 = RoundSuccess("已派驻");
				goto IL_0082;
			}
		}
		result2 = RoundSuccess("派驻失败");
		goto IL_0082;
		IL_0082:
		return result2;
	}

	[NodeFrom("点击开工", Success = false)]
	[NodeFrom("制造派驻", Status = "派驻失败")]
	[OperationNode("返回随便观")]
	public OperationRoundResult BackToEntryNode()
	{
		return BackToEntry();
	}
}

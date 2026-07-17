using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.GameData;

public sealed class AgentStateDef
{
	public string StateName { get; }

	public AgentStateCheckWay CheckWay { get; }

	public string TemplateId { get; }

	public IReadOnlyList<int>? LowerColor { get; }

	public IReadOnlyList<int>? UpperColor { get; }

	public IReadOnlyList<int>? HsvColor { get; }

	public IReadOnlyList<int>? HsvColorDiff { get; }

	public int ConnectCnt { get; }

	public IReadOnlyList<int>? SplitColorRange { get; }

	public int MaxLength { get; }

	public int MinValueTriggerState { get; }

	public double? TemplateThreshold { get; }

	public bool ClearOnZero { get; }

	public AgentStateDef(string stateName, AgentStateCheckWay checkWay = AgentStateCheckWay.COLOR_RANGE_EXIST, string templateId = "", IReadOnlyList<int>? lowerColor = null, IReadOnlyList<int>? upperColor = null, IReadOnlyList<int>? hsvColor = null, IReadOnlyList<int>? hsvColorDiff = null, int? connectCnt = null, IReadOnlyList<int>? splitColorRange = null, int maxLength = 100, int? minValueTriggerState = null, double? templateThreshold = null, bool clearOnZero = false)
	{
		StateName = stateName;
		CheckWay = checkWay;
		TemplateId = (string.IsNullOrWhiteSpace(templateId) ? stateName : templateId);
		LowerColor = lowerColor?.ToArray();
		UpperColor = upperColor?.ToArray();
		HsvColor = hsvColor?.ToArray();
		HsvColorDiff = hsvColorDiff?.ToArray();
		ConnectCnt = connectCnt ?? 1;
		SplitColorRange = splitColorRange?.ToArray();
		MaxLength = maxLength;
		MinValueTriggerState = minValueTriggerState ?? ((checkWay == AgentStateCheckWay.COLOR_RANGE_EXIST) ? 1 : 0);
		TemplateThreshold = templateThreshold;
		ClearOnZero = clearOnZero;
	}
}

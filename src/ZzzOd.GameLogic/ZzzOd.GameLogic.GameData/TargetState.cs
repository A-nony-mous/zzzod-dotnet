using System.Collections.Generic;

namespace ZzzOd.GameLogic.GameData;

public static class TargetState
{
	public static IReadOnlyList<DetectionTask> DetectionTasks { get; } = new DetectionTask[3]
	{
		new DetectionTask
		{
			TaskId = "lock_on",
			PipelineName = "lock-far",
			Interval = 0.0,
			DynamicIntervalConfig = new Dictionary<string, object>
			{
				["state_to_watch"] = "目标-近距离锁定",
				["interval_if_state"] = 1.0,
				["interval_if_not_state"] = 0.0,
				["kwarg_if_state"] = "check_lock_interval_locked",
				["kwarg_if_not_state"] = "check_lock_interval_unlocked"
			},
			StateDefinitions = new TargetStateDef[] { new TargetStateDef
			{
				StateName = "目标-近距离锁定",
				CheckWay = TargetCheckWay.ContourCountInRange,
				CheckParams = new Dictionary<string, object> { ["min_count"] = 2 }
			} }
		},
		new DetectionTask
		{
			TaskId = "abnormal_statuses",
			PipelineName = "ocr-abnormal",
			Enabled = false,
			Interval = 0.0,
			IsAsync = true,
			StateDefinitions = new TargetStateDef[7]
			{
				CreateAbnormalState("目标-异常-灼烧", "灼烧"),
				CreateAbnormalState("目标-异常-冻结", "冻结"),
				CreateAbnormalState("目标-异常-霜灼", "霜灼"),
				CreateAbnormalState("目标-异常-感电", "感电"),
				CreateAbnormalState("目标-异常-碎冰", "碎冰"),
				CreateAbnormalState("目标-异常-侵蚀", "侵蚀"),
				CreateAbnormalState("目标-异常-强击", "强击")
			}
		},
		new DetectionTask
		{
			TaskId = "boss_stun_by_length",
			PipelineName = "boss_stun_line",
			Interval = 0.0,
			Enabled = false,
			IsAsync = true,
			StateDefinitions = new TargetStateDef[] { new TargetStateDef
			{
				StateName = "强敌-失衡值",
				CheckWay = TargetCheckWay.MapContourLengthToPercent,
				CheckParams = new Dictionary<string, object>
				{
					["full_value_length"] = 100,
					["empty_value_length"] = 0
				},
				ClearOnMiss = true
			} }
		}
	};

	public static IReadOnlyList<DetectionTask> DETECTION_TASKS => DetectionTasks;

	private static TargetStateDef CreateAbnormalState(string stateName, string expectedText)
	{
		return new TargetStateDef
		{
			StateName = stateName,
			CheckWay = TargetCheckWay.OcrTextSimilarity,
			CheckParams = new Dictionary<string, object>
			{
				["expected_texts"] = new string[1] { expectedText },
				["threshold"] = 0.5
			}
		};
	}
}

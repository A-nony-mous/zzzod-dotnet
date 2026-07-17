using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;

/// <summary>
/// 枯萎之都设置元数据。
/// </summary>
public static class WitheredDomainSettings
{
	/// <summary>BaselineParity 设置提供器类型。</summary>
	public const string SettingType = "INTERFACE";

	/// <summary>字段列表。</summary>
	public static IReadOnlyList<WitheredDomainSettingField> Fields { get; } = new WitheredDomainSettingField[6]
	{
		new WitheredDomainSettingField("mission_name", "副本", WitheredDomainSettingType.String, "旧都列车-内部"),
		new WitheredDomainSettingField("challenge_config", "挑战配置", WitheredDomainSettingType.String, "默认-专属空洞-艾莲"),
		new WitheredDomainSettingField("weekly_plan_times", "每周计划次数", WitheredDomainSettingType.Integer, 2),
		new WitheredDomainSettingField("daily_plan_times", "每日计划次数", WitheredDomainSettingType.Integer, 99),
		new WitheredDomainSettingField("extra_task", "额外任务", WitheredDomainSettingType.Enum, "刷满周期奖励", WitheredDomainExtraTask.Options),
		new WitheredDomainSettingField("extra_exit", "额外任务退出", WitheredDomainSettingType.Enum, "通关", WitheredDomainExtraExit.Options)
	};
}

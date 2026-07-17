using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 一条龙运行设置。
/// </summary>
public sealed class OneDragonConfig
{
	[YamlMember(Alias = "instance_list", ApplyNamingConventions = false)]
	public List<OneDragonInstanceConfigItem> InstanceList { get; set; } = new List<OneDragonInstanceConfigItem>();

	[YamlMember(Alias = "instance_run", ApplyNamingConventions = false)]
	public string InstanceRun { get; set; } = "全部实例";

	[YamlMember(Alias = "after_done", ApplyNamingConventions = false)]
	public string AfterDone { get; set; } = "无";

	[YamlMember(Alias = "enable_notify", ApplyNamingConventions = false)]
	public bool EnableNotify { get; set; } = true;
}

using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 通知标题配置。
/// </summary>
public sealed class NotifyConfig
{
	[YamlMember(Alias = "title", ApplyNamingConventions = false)]
	public string Title { get; set; } = "一条龙运行通知";

	[YamlMember(Alias = "enable_notify", ApplyNamingConventions = false)]
	public bool EnableNotify { get; set; } = true;

	[YamlMember(Alias = "merge_error_immediate_notify", ApplyNamingConventions = false)]
	public bool MergeErrorImmediateNotify { get; set; } = true;

	[YamlMember(Alias = "applications", ApplyNamingConventions = false)]
	public Dictionary<string, NotifyApplicationSetting> Applications { get; set; } = new Dictionary<string, NotifyApplicationSetting>(StringComparer.Ordinal);

	/// <summary>
	/// 取得应用的二维通知配置。BaselineParity 在未写入 applications 时使用默认值。
	/// </summary>
	public NotifyApplicationSetting GetApplicationSetting(string appId)
	{
		if (Applications.TryGetValue(appId, out NotifyApplicationSetting value))
		{
			return value;
		}
		return new NotifyApplicationSetting();
	}
}

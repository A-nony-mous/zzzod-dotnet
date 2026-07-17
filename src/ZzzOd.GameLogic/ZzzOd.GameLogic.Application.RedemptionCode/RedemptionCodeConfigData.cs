using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.RedemptionCode;

/// <summary>
/// 兑换码配置数据。
/// </summary>
public sealed class RedemptionCodeConfigData
{
	/// <summary>
	/// 兑换码到过期日期的映射，日期格式为 yyyyMMdd。
	/// </summary>
	[YamlMember(Alias = "codes", ApplyNamingConventions = false)]
	public Dictionary<string, int> Codes { get; set; } = new Dictionary<string, int>();
}

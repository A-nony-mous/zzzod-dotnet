using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地藏品或武备。
/// </summary>
public sealed class LostVoidArtifact
{
	/// <summary>分类。</summary>
	[YamlMember(Alias = "category", ApplyNamingConventions = false)]
	public string Category { get; set; } = string.Empty;

	/// <summary>名称。</summary>
	[YamlMember(Alias = "name", ApplyNamingConventions = false)]
	public string Name { get; set; } = string.Empty;

	/// <summary>等级。</summary>
	[YamlMember(Alias = "level", ApplyNamingConventions = false)]
	public string Level { get; set; } = string.Empty;

	/// <summary>是否武备。</summary>
	[YamlMember(Alias = "is_gear", ApplyNamingConventions = false)]
	public bool IsGear { get; set; }

	/// <summary>模板 id。</summary>
	[YamlMember(Alias = "template_id", ApplyNamingConventions = false)]
	public string? TemplateId { get; set; }

	/// <summary>游戏中显示的完整名字。</summary>
	[YamlIgnore]
	public string DisplayName
	{
		get
		{
			string category = Category;
			bool flag = ((category == "卡牌" || category == "无详情") ? true : false);
			return flag ? Name : ("[" + Category + "]" + Name);
		}
	}
}

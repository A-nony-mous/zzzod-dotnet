namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 迷失之地交互目标。
/// </summary>
public sealed class LostVoidInteractTarget
{
	/// <summary>交互文本。</summary>
	public string Name { get; }

	/// <summary>交互图标。</summary>
	public string Icon { get; }

	/// <summary>是否代理人。</summary>
	public bool IsAgent { get; }

	/// <summary>是否 NPC。</summary>
	public bool IsNpc { get; }

	/// <summary>是否下层入口。</summary>
	public bool IsEntry { get; }

	/// <summary>是否感叹号。</summary>
	public bool IsExclamation { get; }

	/// <summary>是否距离白点。</summary>
	public bool IsDistance { get; }

	/// <summary>是否战斗后的交互。</summary>
	public bool AfterBattle { get; }

	/// <summary>
	/// 初始化交互目标。
	/// </summary>
	public LostVoidInteractTarget(string name, string icon, bool isAgent = false, bool isNpc = false, bool isEntry = false, bool isExclamation = false, bool isDistance = false, bool afterBattle = false)
	{
		Name = name;
		Icon = icon;
		IsAgent = isAgent;
		IsNpc = isNpc;
		IsEntry = isEntry;
		IsExclamation = isExclamation;
		IsDistance = isDistance;
		AfterBattle = afterBattle;
	}
}

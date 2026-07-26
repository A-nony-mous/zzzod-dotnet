using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 绝区零游戏配置。
/// </summary>
public sealed class GameConfig
{
	/// <summary>
	/// 旧数字索引手柄按键 到 新描述性键名 的迁移映射表。
	/// </summary>
	private static readonly IReadOnlyDictionary<string, string> LegacyGamepadKeyMap = BuildLegacyGamepadKeyMap();

	private string? _controlMethod;

	[YamlMember(Alias = "background_mode", ApplyNamingConventions = false)]
	public bool BackgroundMode { get; set; }

	[YamlMember(Alias = "background_gamepad_type", ApplyNamingConventions = false)]
	public string BackgroundGamepadType { get; set; } = "xbox";

	[YamlMember(Alias = "turn_dx", ApplyNamingConventions = false)]
	public float TurnDx { get; set; } = -5.5f;

	[YamlMember(Alias = "gamepad_turn_speed", ApplyNamingConventions = false)]
	public float GamepadTurnSpeed { get; set; } = 1000f;

	[YamlMember(Alias = "mouse_flash_duration", ApplyNamingConventions = false)]
	public float MouseFlashDuration { get; set; } = 0.05f;

	[YamlMember(Alias = "xbox_key_press_time", ApplyNamingConventions = false)]
	public float XboxKeyPressTime { get; set; } = 0.02f;

	[YamlMember(Alias = "ds4_key_press_time", ApplyNamingConventions = false)]
	public float Ds4KeyPressTime { get; set; } = 0.02f;

	[YamlMember(Alias = "type_input_way", ApplyNamingConventions = false)]
	public string TypeInputWay { get; set; } = "clipboard";

	[YamlMember(Alias = "hdr", ApplyNamingConventions = false)]
	public bool Hdr { get; set; }

	[YamlMember(Alias = "launch_argument", ApplyNamingConventions = false)]
	public bool LaunchArgument { get; set; }

	[YamlMember(Alias = "screen_size", ApplyNamingConventions = false)]
	public string ScreenSize { get; set; } = "1920x1080";

	[YamlMember(Alias = "full_screen", ApplyNamingConventions = false)]
	public string FullScreen { get; set; } = "0";

	[YamlMember(Alias = "popup_window", ApplyNamingConventions = false)]
	public bool PopupWindow { get; set; }

	[YamlMember(Alias = "monitor", ApplyNamingConventions = false)]
	public string Monitor { get; set; } = "1";

	[YamlMember(Alias = "launch_argument_advance", ApplyNamingConventions = false)]
	public string LaunchArgumentAdvance { get; set; } = string.Empty;

	[YamlMember(Alias = "original_hdr_value", ApplyNamingConventions = false)]
	public string? OriginalHdrValue { get; set; }

	[YamlMember(Alias = "key_interact", ApplyNamingConventions = false)]
	public string KeyInteract { get; set; } = "f";

	[YamlMember(Alias = "key_normal_attack", ApplyNamingConventions = false)]
	public string KeyNormalAttack { get; set; } = "mouse_left";

	[YamlMember(Alias = "key_dodge", ApplyNamingConventions = false)]
	public string KeyDodge { get; set; } = "shift";

	[YamlMember(Alias = "key_switch_next", ApplyNamingConventions = false)]
	public string KeySwitchNext { get; set; } = "space";

	[YamlMember(Alias = "key_switch_prev", ApplyNamingConventions = false)]
	public string KeySwitchPrev { get; set; } = "c";

	[YamlMember(Alias = "key_switch_backup", ApplyNamingConventions = false)]
	public string KeySwitchBackup { get; set; } = "r";

	[YamlMember(Alias = "key_special_attack", ApplyNamingConventions = false)]
	public string KeySpecialAttack { get; set; } = "e";

	[YamlMember(Alias = "key_ultimate", ApplyNamingConventions = false)]
	public string KeyUltimate { get; set; } = "q";

	[YamlMember(Alias = "key_chain_left", ApplyNamingConventions = false)]
	public string KeyChainLeft { get; set; } = "q";

	[YamlMember(Alias = "key_chain_right", ApplyNamingConventions = false)]
	public string KeyChainRight { get; set; } = "e";

	[YamlMember(Alias = "key_move_w", ApplyNamingConventions = false)]
	public string KeyMoveW { get; set; } = "w";

	[YamlMember(Alias = "key_move_s", ApplyNamingConventions = false)]
	public string KeyMoveS { get; set; } = "s";

	[YamlMember(Alias = "key_move_a", ApplyNamingConventions = false)]
	public string KeyMoveA { get; set; } = "a";

	[YamlMember(Alias = "key_move_d", ApplyNamingConventions = false)]
	public string KeyMoveD { get; set; } = "d";

	[YamlMember(Alias = "key_lock", ApplyNamingConventions = false)]
	public string KeyLock { get; set; } = "mouse_middle";

	[YamlMember(Alias = "key_chain_cancel", ApplyNamingConventions = false)]
	public string KeyChainCancel { get; set; } = "mouse_middle";

	private string _xboxKeyInteract = "xbox_a";

	private string _xboxKeyNormalAttack = "xbox_x";

	private string _xboxKeyDodge = "xbox_a";

	private string _xboxKeySwitchNext = "xbox_rb";

	private string _xboxKeySwitchPrev = "xbox_lb";

	private string _xboxKeySwitchBackup = "xbox_b";

	private string _xboxKeySpecialAttack = "xbox_y";

	private string _xboxKeyUltimate = "xbox_rt";

	private string _xboxKeyChainLeft = "xbox_lb";

	private string _xboxKeyChainRight = "xbox_rb";

	private string _xboxKeyMoveW = "xbox_ls_up";

	private string _xboxKeyMoveS = "xbox_ls_down";

	private string _xboxKeyMoveA = "xbox_ls_left";

	private string _xboxKeyMoveD = "xbox_ls_right";

	private string _xboxKeyLock = "xbox_r_thumb";

	private string _xboxKeyChainCancel = "xbox_a";

	private string _ds4KeyInteract = "ds4_cross";

	private string _ds4KeyNormalAttack = "ds4_square";

	private string _ds4KeyDodge = "ds4_cross";

	private string _ds4KeySwitchNext = "ds4_r1";

	private string _ds4KeySwitchPrev = "ds4_l1";

	private string _ds4KeySwitchBackup = "ds4_circle";

	private string _ds4KeySpecialAttack = "ds4_triangle";

	private string _ds4KeyUltimate = "ds4_r2";

	private string _ds4KeyChainLeft = "ds4_l1";

	private string _ds4KeyChainRight = "ds4_r1";

	private string _ds4KeyMoveW = "ds4_ls_up";

	private string _ds4KeyMoveS = "ds4_ls_down";

	private string _ds4KeyMoveA = "ds4_ls_left";

	private string _ds4KeyMoveD = "ds4_ls_right";

	private string _ds4KeyLock = "ds4_r_thumb";

	private string _ds4KeyChainCancel = "ds4_cross";

	[YamlMember(Alias = "xbox_key_interact", ApplyNamingConventions = false)]
	public string XboxKeyInteract
	{
		get => _xboxKeyInteract;
		set => _xboxKeyInteract = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_normal_attack", ApplyNamingConventions = false)]
	public string XboxKeyNormalAttack
	{
		get => _xboxKeyNormalAttack;
		set => _xboxKeyNormalAttack = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_dodge", ApplyNamingConventions = false)]
	public string XboxKeyDodge
	{
		get => _xboxKeyDodge;
		set => _xboxKeyDodge = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_switch_next", ApplyNamingConventions = false)]
	public string XboxKeySwitchNext
	{
		get => _xboxKeySwitchNext;
		set => _xboxKeySwitchNext = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_switch_prev", ApplyNamingConventions = false)]
	public string XboxKeySwitchPrev
	{
		get => _xboxKeySwitchPrev;
		set => _xboxKeySwitchPrev = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_switch_backup", ApplyNamingConventions = false)]
	public string XboxKeySwitchBackup
	{
		get => _xboxKeySwitchBackup;
		set => _xboxKeySwitchBackup = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_special_attack", ApplyNamingConventions = false)]
	public string XboxKeySpecialAttack
	{
		get => _xboxKeySpecialAttack;
		set => _xboxKeySpecialAttack = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_ultimate", ApplyNamingConventions = false)]
	public string XboxKeyUltimate
	{
		get => _xboxKeyUltimate;
		set => _xboxKeyUltimate = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_chain_left", ApplyNamingConventions = false)]
	public string XboxKeyChainLeft
	{
		get => _xboxKeyChainLeft;
		set => _xboxKeyChainLeft = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_chain_right", ApplyNamingConventions = false)]
	public string XboxKeyChainRight
	{
		get => _xboxKeyChainRight;
		set => _xboxKeyChainRight = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_move_w", ApplyNamingConventions = false)]
	public string XboxKeyMoveW
	{
		get => _xboxKeyMoveW;
		set => _xboxKeyMoveW = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_move_s", ApplyNamingConventions = false)]
	public string XboxKeyMoveS
	{
		get => _xboxKeyMoveS;
		set => _xboxKeyMoveS = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_move_a", ApplyNamingConventions = false)]
	public string XboxKeyMoveA
	{
		get => _xboxKeyMoveA;
		set => _xboxKeyMoveA = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_move_d", ApplyNamingConventions = false)]
	public string XboxKeyMoveD
	{
		get => _xboxKeyMoveD;
		set => _xboxKeyMoveD = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_lock", ApplyNamingConventions = false)]
	public string XboxKeyLock
	{
		get => _xboxKeyLock;
		set => _xboxKeyLock = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_key_chain_cancel", ApplyNamingConventions = false)]
	public string XboxKeyChainCancel
	{
		get => _xboxKeyChainCancel;
		set => _xboxKeyChainCancel = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_interact", ApplyNamingConventions = false)]
	public string Ds4KeyInteract
	{
		get => _ds4KeyInteract;
		set => _ds4KeyInteract = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_normal_attack", ApplyNamingConventions = false)]
	public string Ds4KeyNormalAttack
	{
		get => _ds4KeyNormalAttack;
		set => _ds4KeyNormalAttack = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_dodge", ApplyNamingConventions = false)]
	public string Ds4KeyDodge
	{
		get => _ds4KeyDodge;
		set => _ds4KeyDodge = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_switch_next", ApplyNamingConventions = false)]
	public string Ds4KeySwitchNext
	{
		get => _ds4KeySwitchNext;
		set => _ds4KeySwitchNext = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_switch_prev", ApplyNamingConventions = false)]
	public string Ds4KeySwitchPrev
	{
		get => _ds4KeySwitchPrev;
		set => _ds4KeySwitchPrev = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_switch_backup", ApplyNamingConventions = false)]
	public string Ds4KeySwitchBackup
	{
		get => _ds4KeySwitchBackup;
		set => _ds4KeySwitchBackup = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_special_attack", ApplyNamingConventions = false)]
	public string Ds4KeySpecialAttack
	{
		get => _ds4KeySpecialAttack;
		set => _ds4KeySpecialAttack = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_ultimate", ApplyNamingConventions = false)]
	public string Ds4KeyUltimate
	{
		get => _ds4KeyUltimate;
		set => _ds4KeyUltimate = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_chain_left", ApplyNamingConventions = false)]
	public string Ds4KeyChainLeft
	{
		get => _ds4KeyChainLeft;
		set => _ds4KeyChainLeft = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_chain_right", ApplyNamingConventions = false)]
	public string Ds4KeyChainRight
	{
		get => _ds4KeyChainRight;
		set => _ds4KeyChainRight = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_move_w", ApplyNamingConventions = false)]
	public string Ds4KeyMoveW
	{
		get => _ds4KeyMoveW;
		set => _ds4KeyMoveW = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_move_s", ApplyNamingConventions = false)]
	public string Ds4KeyMoveS
	{
		get => _ds4KeyMoveS;
		set => _ds4KeyMoveS = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_move_a", ApplyNamingConventions = false)]
	public string Ds4KeyMoveA
	{
		get => _ds4KeyMoveA;
		set => _ds4KeyMoveA = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_move_d", ApplyNamingConventions = false)]
	public string Ds4KeyMoveD
	{
		get => _ds4KeyMoveD;
		set => _ds4KeyMoveD = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_lock", ApplyNamingConventions = false)]
	public string Ds4KeyLock
	{
		get => _ds4KeyLock;
		set => _ds4KeyLock = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "ds4_key_chain_cancel", ApplyNamingConventions = false)]
	public string Ds4KeyChainCancel
	{
		get => _ds4KeyChainCancel;
		set => _ds4KeyChainCancel = MigrateLegacyGamepadKey(value);
	}

	[YamlMember(Alias = "xbox_action_menu", ApplyNamingConventions = false)]
	public List<string> XboxActionMenu { get; set; }

	[YamlMember(Alias = "xbox_action_map", ApplyNamingConventions = false)]
	public List<string> XboxActionMap { get; set; }

	[YamlMember(Alias = "xbox_action_minimap", ApplyNamingConventions = false)]
	public List<string> XboxActionMinimap { get; set; }

	[YamlMember(Alias = "xbox_action_compendium", ApplyNamingConventions = false)]
	public List<string> XboxActionCompendium { get; set; }

	[YamlMember(Alias = "xbox_action_function_menu", ApplyNamingConventions = false)]
	public List<string> XboxActionFunctionMenu { get; set; }

	[YamlMember(Alias = "ds4_action_menu", ApplyNamingConventions = false)]
	public List<string> Ds4ActionMenu { get; set; }

	[YamlMember(Alias = "ds4_action_map", ApplyNamingConventions = false)]
	public List<string> Ds4ActionMap { get; set; }

	[YamlMember(Alias = "ds4_action_minimap", ApplyNamingConventions = false)]
	public List<string> Ds4ActionMinimap { get; set; }

	[YamlMember(Alias = "ds4_action_compendium", ApplyNamingConventions = false)]
	public List<string> Ds4ActionCompendium { get; set; }

	[YamlMember(Alias = "ds4_action_function_menu", ApplyNamingConventions = false)]
	public List<string> Ds4ActionFunctionMenu { get; set; }

	[YamlMember(Alias = "control_method", ApplyNamingConventions = false)]
	public string ControlMethod
	{
		get
		{
			return string.IsNullOrWhiteSpace(_controlMethod) ? "keyboard" : _controlMethod;
		}
		set
		{
			_controlMethod = value;
		}
	}

	[YamlMember(Alias = "gamepad_type", ApplyNamingConventions = false)]
	public string? LegacyGamepadType
	{
		get
		{
			return null;
		}
		set
		{
			if (string.IsNullOrWhiteSpace(_controlMethod) && !string.IsNullOrWhiteSpace(value))
			{
				_controlMethod = value;
			}
		}
	}

	/// <summary>
	/// 按 "+" 拆分手柄按键组合，把旧数字索引键名逐段迁移为新的描述性键名。
	/// </summary>
	private static string MigrateLegacyGamepadKey(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		string[] segments = value.Split('+');
		for (int i = 0; i < segments.Length; i++)
		{
			if (LegacyGamepadKeyMap.TryGetValue(segments[i], out string? migrated))
			{
				segments[i] = migrated;
			}
		}
		return string.Join("+", segments);
	}

	private static IReadOnlyDictionary<string, string> BuildLegacyGamepadKeyMap()
	{
		string[] xboxKeys = new string[14]
		{
			"xbox_a", "xbox_b", "xbox_x", "xbox_y", "xbox_lt", "xbox_rt", "xbox_lb", "xbox_rb", "xbox_ls_up", "xbox_ls_down",
			"xbox_ls_left", "xbox_ls_right", "xbox_l_thumb", "xbox_r_thumb"
		};
		string[] ds4Keys = new string[14]
		{
			"ds4_cross", "ds4_circle", "ds4_square", "ds4_triangle", "ds4_l2", "ds4_r2", "ds4_l1", "ds4_r1", "ds4_ls_up", "ds4_ls_down",
			"ds4_ls_left", "ds4_ls_right", "ds4_l_thumb", "ds4_r_thumb"
		};
		Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
		for (int i = 0; i < xboxKeys.Length; i++)
		{
			map[$"xbox_{i}"] = xboxKeys[i];
		}
		for (int i = 0; i < ds4Keys.Length; i++)
		{
			map[$"ds4_{i}"] = ds4Keys[i];
		}
		return map;
	}

	public GameConfig()
	{
		int num = 1;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = "xbox_start";
		XboxActionMenu = list;
		num = 1;
		List<string> list2 = new List<string>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = "xbox_dpad_right";
		XboxActionMap = list2;
		num = 1;
		List<string> list3 = new List<string>(num);
		CollectionsMarshal.SetCount(list3, num);
		CollectionsMarshal.AsSpan(list3)[0] = "xbox_back";
		XboxActionMinimap = list3;
		num = 2;
		List<string> list4 = new List<string>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<string> span = CollectionsMarshal.AsSpan(list4);
		span[0] = "xbox_lt";
		span[1] = "xbox_a";
		XboxActionCompendium = list4;
		num = 2;
		List<string> list5 = new List<string>(num);
		CollectionsMarshal.SetCount(list5, num);
		Span<string> span2 = CollectionsMarshal.AsSpan(list5);
		span2[0] = "xbox_lt";
		span2[1] = "xbox_start";
		XboxActionFunctionMenu = list5;
		num = 1;
		List<string> list6 = new List<string>(num);
		CollectionsMarshal.SetCount(list6, num);
		CollectionsMarshal.AsSpan(list6)[0] = "ds4_options";
		Ds4ActionMenu = list6;
		num = 1;
		List<string> list7 = new List<string>(num);
		CollectionsMarshal.SetCount(list7, num);
		CollectionsMarshal.AsSpan(list7)[0] = "ds4_dpad_right";
		Ds4ActionMap = list7;
		num = 1;
		List<string> list8 = new List<string>(num);
		CollectionsMarshal.SetCount(list8, num);
		CollectionsMarshal.AsSpan(list8)[0] = "ds4_touchpad";
		Ds4ActionMinimap = list8;
		num = 2;
		List<string> list9 = new List<string>(num);
		CollectionsMarshal.SetCount(list9, num);
		Span<string> span3 = CollectionsMarshal.AsSpan(list9);
		span3[0] = "ds4_l2";
		span3[1] = "ds4_cross";
		Ds4ActionCompendium = list9;
		num = 2;
		List<string> list10 = new List<string>(num);
		CollectionsMarshal.SetCount(list10, num);
		Span<string> span4 = CollectionsMarshal.AsSpan(list10);
		span4[0] = "ds4_l2";
		span4[1] = "ds4_options";
		Ds4ActionFunctionMenu = list10;
	}
}

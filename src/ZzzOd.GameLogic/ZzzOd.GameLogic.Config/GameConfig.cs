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

	[YamlMember(Alias = "xbox_key_interact", ApplyNamingConventions = false)]
	public string XboxKeyInteract { get; set; } = "xbox_a";

	[YamlMember(Alias = "xbox_key_normal_attack", ApplyNamingConventions = false)]
	public string XboxKeyNormalAttack { get; set; } = "xbox_x";

	[YamlMember(Alias = "xbox_key_dodge", ApplyNamingConventions = false)]
	public string XboxKeyDodge { get; set; } = "xbox_a";

	[YamlMember(Alias = "xbox_key_switch_next", ApplyNamingConventions = false)]
	public string XboxKeySwitchNext { get; set; } = "xbox_rb";

	[YamlMember(Alias = "xbox_key_switch_prev", ApplyNamingConventions = false)]
	public string XboxKeySwitchPrev { get; set; } = "xbox_lb";

	[YamlMember(Alias = "xbox_key_switch_backup", ApplyNamingConventions = false)]
	public string XboxKeySwitchBackup { get; set; } = "xbox_b";

	[YamlMember(Alias = "xbox_key_special_attack", ApplyNamingConventions = false)]
	public string XboxKeySpecialAttack { get; set; } = "xbox_y";

	[YamlMember(Alias = "xbox_key_ultimate", ApplyNamingConventions = false)]
	public string XboxKeyUltimate { get; set; } = "xbox_rt";

	[YamlMember(Alias = "xbox_key_chain_left", ApplyNamingConventions = false)]
	public string XboxKeyChainLeft { get; set; } = "xbox_lb";

	[YamlMember(Alias = "xbox_key_chain_right", ApplyNamingConventions = false)]
	public string XboxKeyChainRight { get; set; } = "xbox_rb";

	[YamlMember(Alias = "xbox_key_move_w", ApplyNamingConventions = false)]
	public string XboxKeyMoveW { get; set; } = "xbox_ls_up";

	[YamlMember(Alias = "xbox_key_move_s", ApplyNamingConventions = false)]
	public string XboxKeyMoveS { get; set; } = "xbox_ls_down";

	[YamlMember(Alias = "xbox_key_move_a", ApplyNamingConventions = false)]
	public string XboxKeyMoveA { get; set; } = "xbox_ls_left";

	[YamlMember(Alias = "xbox_key_move_d", ApplyNamingConventions = false)]
	public string XboxKeyMoveD { get; set; } = "xbox_ls_right";

	[YamlMember(Alias = "xbox_key_lock", ApplyNamingConventions = false)]
	public string XboxKeyLock { get; set; } = "xbox_r_thumb";

	[YamlMember(Alias = "xbox_key_chain_cancel", ApplyNamingConventions = false)]
	public string XboxKeyChainCancel { get; set; } = "xbox_a";

	[YamlMember(Alias = "ds4_key_interact", ApplyNamingConventions = false)]
	public string Ds4KeyInteract { get; set; } = "ds4_cross";

	[YamlMember(Alias = "ds4_key_normal_attack", ApplyNamingConventions = false)]
	public string Ds4KeyNormalAttack { get; set; } = "ds4_square";

	[YamlMember(Alias = "ds4_key_dodge", ApplyNamingConventions = false)]
	public string Ds4KeyDodge { get; set; } = "ds4_cross";

	[YamlMember(Alias = "ds4_key_switch_next", ApplyNamingConventions = false)]
	public string Ds4KeySwitchNext { get; set; } = "ds4_r1";

	[YamlMember(Alias = "ds4_key_switch_prev", ApplyNamingConventions = false)]
	public string Ds4KeySwitchPrev { get; set; } = "ds4_l1";

	[YamlMember(Alias = "ds4_key_switch_backup", ApplyNamingConventions = false)]
	public string Ds4KeySwitchBackup { get; set; } = "ds4_circle";

	[YamlMember(Alias = "ds4_key_special_attack", ApplyNamingConventions = false)]
	public string Ds4KeySpecialAttack { get; set; } = "ds4_triangle";

	[YamlMember(Alias = "ds4_key_ultimate", ApplyNamingConventions = false)]
	public string Ds4KeyUltimate { get; set; } = "ds4_r2";

	[YamlMember(Alias = "ds4_key_chain_left", ApplyNamingConventions = false)]
	public string Ds4KeyChainLeft { get; set; } = "ds4_l1";

	[YamlMember(Alias = "ds4_key_chain_right", ApplyNamingConventions = false)]
	public string Ds4KeyChainRight { get; set; } = "ds4_r1";

	[YamlMember(Alias = "ds4_key_move_w", ApplyNamingConventions = false)]
	public string Ds4KeyMoveW { get; set; } = "ds4_ls_up";

	[YamlMember(Alias = "ds4_key_move_s", ApplyNamingConventions = false)]
	public string Ds4KeyMoveS { get; set; } = "ds4_ls_down";

	[YamlMember(Alias = "ds4_key_move_a", ApplyNamingConventions = false)]
	public string Ds4KeyMoveA { get; set; } = "ds4_ls_left";

	[YamlMember(Alias = "ds4_key_move_d", ApplyNamingConventions = false)]
	public string Ds4KeyMoveD { get; set; } = "ds4_ls_right";

	[YamlMember(Alias = "ds4_key_lock", ApplyNamingConventions = false)]
	public string Ds4KeyLock { get; set; } = "ds4_r_thumb";

	[YamlMember(Alias = "ds4_key_chain_cancel", ApplyNamingConventions = false)]
	public string Ds4KeyChainCancel { get; set; } = "ds4_cross";

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

using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Input;
using OneDragon.Core.Screening;
using OneDragon.Core.Windows.Controller;
using OneDragon.Core.Windows.Input;
using OpenCvSharp;
using Serilog;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Backend;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.ScreenArea;

namespace ZzzOd.GameLogic.Controller;

/// <summary>
/// ZZZ PC 游戏控制器，提供特定业务按键逻辑。
/// </summary>
public sealed class ZPcController : WindowsGameController, IZzzControllerActions, IBackendWindowStatusProvider
{
	private GameConfig _gameConfig;

	private string _actionKeyInteract = "";

	private string _actionKeyNormalAttack = "";

	private string _actionKeyDodge = "";

	private string _actionKeySwitchNext = "";

	private string _actionKeySwitchPrev = "";

	private string _actionKeySwitchBackup = "";

	private string _actionKeySpecialAttack = "";

	private string _actionKeyUltimate = "";

	private string _actionKeyChainLeft = "";

	private string _actionKeyChainRight = "";

	private string _actionKeyMoveW = "";

	private string _actionKeyMoveS = "";

	private string _actionKeyMoveA = "";

	private string _actionKeyMoveD = "";

	private string _actionKeyLock = "";

	private string _actionKeyChainCancel = "";

	private bool _isMoving;

	private readonly Action<float, float> _mouseMoveRelative;

	/// <summary>
	/// 初始化 ZPcController。
	/// </summary>
	public ZPcController(GameConfig gameConfig, string? screenshotMethod = null, int standardWidth = 1920, int standardHeight = 1080, ScreenshotController? screenshotController = null, IInputController? foregroundInputController = null, IInputController? backgroundInputController = null, IButtonController? foregroundButtonController = null, IButtonController? backgroundButtonController = null, IButtonController? backgroundGamepadController = null, bool skipForegroundActivation = false, Action<float, float>? mouseMoveRelative = null)
		: base(null, screenshotMethod, standardWidth, standardHeight, null, screenshotController, foregroundInputController, backgroundInputController, foregroundButtonController, backgroundButtonController, backgroundGamepadController, null, null, skipForegroundActivation)
	{
		_gameConfig = gameConfig;
		RefreshActionKeys("keyboard");
		_isMoving = false;
		_mouseMoveRelative = mouseMoveRelative ?? new Action<float, float>(MouseMoveRelativeByUser32);
	}

	/// <summary>
	/// 切换实例后同步控制器持有的账号级配置。
	/// </summary>
	/// <param name="gameConfig">新实例的游戏配置。</param>
	public void SyncGameConfig(GameConfig gameConfig)
	{
		_gameConfig = gameConfig;
		if (_gameConfig.BackgroundMode)
		{
			ApplyBackgroundGamepadMode();
			return;
		}
		EnableForegroundMode();
		RefreshActionKeys("keyboard");
		ActivateWindow();
	}

	/// <inheritdoc />
	public override bool InitBeforeContextRun()
	{
		ApplyRuntimeControlMode();
		return base.InitBeforeContextRun();
	}

	/// <inheritdoc />
	public WindowStatus GetWindowStatus()
	{
		GameWindowGeometry geometry = GetWindowGeometry();
		OneDragon.Core.Abstractions.Geometry.Rect? rect = geometry.ClientRect;
		return new WindowStatus(
			GameWindowTitle,
			geometry.IsValid,
			geometry.IsActive,
			IsGameWindowScaled,
			rect?.X1,
			rect?.Y1,
			rect?.Width,
			rect?.Height,
			geometry.IsMinimized,
			geometry.Dpi);
	}

	internal void ApplyRuntimeControlMode()
	{
		if (_gameConfig.BackgroundMode)
		{
			Log.Information("ZPcController control mode: background gamepad, type={GamepadType}, configured control_method={ControlMethod}", _gameConfig.BackgroundGamepadType, _gameConfig.ControlMethod);
			ApplyBackgroundGamepadMode();
		}
		else
		{
			Log.Information("ZPcController control mode: foreground keyboard_mouse, configured control_method={ControlMethod}", _gameConfig.ControlMethod);
			EnableForegroundMode();
			RefreshActionKeys("keyboard");
		}
	}

	/// <summary>
	/// 从当前实例配置获取闪切键鼠模式时的单步等待时长。
	/// </summary>
	/// <returns>单步等待时长。</returns>
	protected override TimeSpan GetMouseFlashDuration() => TimeSpan.FromSeconds(_gameConfig.MouseFlashDuration);

	/// <summary>
	/// 遮挡屏幕上 UID 的部分。
	/// </summary>
	/// <param name="screen">原始截图</param>
	/// <returns>遮挡后的当前截图</returns>
	protected override Mat FillUidBlack(Mat screen)
	{
		OneDragon.Core.Abstractions.Geometry.Rect rect = ScreenNormalWorldEnum.Uid.Rect;
		Cv2.Rectangle(screen, new OpenCvSharp.Rect(rect.X1, rect.Y1, rect.Width, rect.Height), GameConst.YoloDefaultColor, -1);
		return screen;
	}

	/// <summary>
	/// 启用键盘模式
	/// </summary>
	public void EnableKeyboard()
	{
		EnsureMouseMode();
		RefreshActionKeys("keyboard");
	}

	/// <summary>
	/// 启用Xbox模式
	/// </summary>
	public void EnableXbox()
	{
		EnsureGamepadMode();
		RefreshActionKeys("xbox");
	}

	/// <summary>
	/// 启用Ds4模式
	/// </summary>
	public void EnableDs4()
	{
		EnsureGamepadMode();
		RefreshActionKeys("ds4");
	}

	private void RefreshActionKeys(string mode)
	{
		if (mode == "xbox")
		{
			_actionKeyInteract = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyInteract ?? "a");
			_actionKeyNormalAttack = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyNormalAttack ?? "x");
			_actionKeyDodge = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyDodge ?? "a");
			_actionKeySwitchNext = NormalizeGamepadKey("xbox", _gameConfig.XboxKeySwitchNext ?? "rb");
			_actionKeySwitchPrev = NormalizeGamepadKey("xbox", _gameConfig.XboxKeySwitchPrev ?? "lb");
			_actionKeySwitchBackup = NormalizeGamepadKey("xbox", _gameConfig.XboxKeySwitchBackup ?? "b");
			_actionKeySpecialAttack = NormalizeGamepadKey("xbox", _gameConfig.XboxKeySpecialAttack ?? "y");
			_actionKeyUltimate = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyUltimate ?? "rt");
			_actionKeyChainLeft = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyChainLeft ?? "lb");
			_actionKeyChainRight = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyChainRight ?? "rb");
			_actionKeyMoveW = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyMoveW ?? "l_stick_w");
			_actionKeyMoveS = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyMoveS ?? "l_stick_s");
			_actionKeyMoveA = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyMoveA ?? "l_stick_a");
			_actionKeyMoveD = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyMoveD ?? "l_stick_d");
			_actionKeyLock = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyLock ?? "r_thumb");
			_actionKeyChainCancel = NormalizeGamepadKey("xbox", _gameConfig.XboxKeyChainCancel ?? "a");
		}
		else if (mode == "ds4")
		{
			_actionKeyInteract = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyInteract ?? "cross");
			_actionKeyNormalAttack = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyNormalAttack ?? "square");
			_actionKeyDodge = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyDodge ?? "cross");
			_actionKeySwitchNext = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeySwitchNext ?? "r1");
			_actionKeySwitchPrev = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeySwitchPrev ?? "l1");
			_actionKeySwitchBackup = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeySwitchBackup ?? "circle");
			_actionKeySpecialAttack = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeySpecialAttack ?? "triangle");
			_actionKeyUltimate = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyUltimate ?? "r2");
			_actionKeyChainLeft = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyChainLeft ?? "l1");
			_actionKeyChainRight = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyChainRight ?? "r1");
			_actionKeyMoveW = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyMoveW ?? "l_stick_w");
			_actionKeyMoveS = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyMoveS ?? "l_stick_s");
			_actionKeyMoveA = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyMoveA ?? "l_stick_a");
			_actionKeyMoveD = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyMoveD ?? "l_stick_d");
			_actionKeyLock = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyLock ?? "r_thumb");
			_actionKeyChainCancel = NormalizeGamepadKey("ds4", _gameConfig.Ds4KeyChainCancel ?? "cross");
		}
		else
		{
			_actionKeyInteract = _gameConfig.KeyInteract ?? "f";
			_actionKeyNormalAttack = _gameConfig.KeyNormalAttack ?? "mouse_left";
			_actionKeyDodge = _gameConfig.KeyDodge ?? "shift";
			_actionKeySwitchNext = _gameConfig.KeySwitchNext ?? "space";
			_actionKeySwitchPrev = _gameConfig.KeySwitchPrev ?? "c";
			_actionKeySwitchBackup = _gameConfig.KeySwitchBackup ?? "r";
			_actionKeySpecialAttack = _gameConfig.KeySpecialAttack ?? "e";
			_actionKeyUltimate = _gameConfig.KeyUltimate ?? "q";
			_actionKeyChainLeft = _gameConfig.KeyChainLeft ?? "q";
			_actionKeyChainRight = _gameConfig.KeyChainRight ?? "e";
			_actionKeyMoveW = _gameConfig.KeyMoveW ?? "w";
			_actionKeyMoveS = _gameConfig.KeyMoveS ?? "s";
			_actionKeyMoveA = _gameConfig.KeyMoveA ?? "a";
			_actionKeyMoveD = _gameConfig.KeyMoveD ?? "d";
			_actionKeyLock = _gameConfig.KeyLock ?? "mouse_middle";
			_actionKeyChainCancel = _gameConfig.KeyChainCancel ?? "mouse_middle";
		}
	}

	private void ActionBtn(string key, bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		Log.Information("ZPcController ActionBtn: key={Key}, press={Press}, release={Release}, pressTimeMs={PressTimeMs}, backgroundMode={BackgroundMode}", key, press, release, pressTime?.TotalMilliseconds, base.IsBackgroundMode);
		if (!IsInputAllowed)
		{
			return;
		}

		if (!TryHandleBackgroundGamepadButton(key, press, pressTime, release))
		{
			if (press)
			{
				GetCurrentInputController().ButtonController.Press(key, pressTime);
			}
			else if (release)
			{
				GetCurrentInputController().ButtonController.Release(key);
			}
			else
			{
				GetCurrentInputController().ButtonController.Tap(key);
			}
		}
	}

	/// <summary>闪避</summary>
	public void Dodge(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyDodge, press, pressTime, release);
	}

	/// <summary>切换下一个角色</summary>
	public void SwitchNext(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeySwitchNext, press, pressTime, release);
	}

	/// <summary>切换上一个角色</summary>
	public void SwitchPrev(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeySwitchPrev, press, pressTime, release);
	}

	/// <summary>切换后备角色</summary>
	public void SwitchBackup(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeySwitchBackup, press, pressTime, release);
	}

	/// <summary>普通攻击</summary>
	public void NormalAttack(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyNormalAttack, press, pressTime, release);
	}

	/// <summary>特殊攻击</summary>
	public void SpecialAttack(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeySpecialAttack, press, pressTime, release);
	}

	/// <summary>终结技</summary>
	public void Ultimate(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyUltimate, press, pressTime, release);
	}

	/// <summary>连携技左</summary>
	public void ChainLeft(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyChainLeft, press, pressTime, release);
	}

	/// <summary>连携技右</summary>
	public void ChainRight(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyChainRight, press, pressTime, release);
	}

	/// <summary>移动前</summary>
	public void MoveW(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyMoveW, press, pressTime, release);
	}

	/// <summary>移动后</summary>
	public void MoveS(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyMoveS, press, pressTime, release);
	}

	/// <summary>移动左</summary>
	public void MoveA(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyMoveA, press, pressTime, release);
	}

	/// <summary>移动右</summary>
	public void MoveD(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyMoveD, press, pressTime, release);
	}

	/// <summary>交互</summary>
	public void Interact(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyInteract, press, pressTime, release);
	}

	/// <summary>打开菜单</summary>
	public bool OpenMenu()
	{
		return RunUiAction("menu", "esc");
	}

	/// <summary>打开地图</summary>
	public bool OpenMap()
	{
		return RunUiAction("map", "j");
	}

	/// <summary>打开小地图</summary>
	public bool OpenMinimap()
	{
		return RunUiAction("minimap", "m");
	}

	/// <summary>打开快捷手册</summary>
	public bool OpenCompendium()
	{
		return RunUiAction("compendium", "f");
	}

	/// <summary>打开功能导览</summary>
	public bool OpenFunctionMenu()
	{
		return RunUiAction("function_menu", "tab");
	}

	/// <summary>锁定</summary>
	public void Lock(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyLock, press, pressTime, release);
	}

	/// <summary>连携技取消</summary>
	public void ChainCancel(bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		ActionBtn(_actionKeyChainCancel, press, pressTime, release);
	}

	/// <summary>
	/// 按 BaselineParity 配置中的中文动作名执行 ZZZ 按键。
	/// </summary>
	public bool RunNamedAction(string actionName, bool press = false, TimeSpan? pressTime = null, bool release = false)
	{
		switch (actionName)
		{
		case "按键-闪避":
			Dodge(press, pressTime, release);
			return true;
		case "按键-切换角色-下一个":
			SwitchNext(press, pressTime, release);
			return true;
		case "按键-切换角色-上一个":
			SwitchPrev(press, pressTime, release);
			return true;
		case "按键-切换后援":
			SwitchBackup(press, pressTime, release);
			return true;
		case "按键-普通攻击":
			NormalAttack(press, pressTime, release);
			return true;
		case "按键-特殊攻击":
			SpecialAttack(press, pressTime, release);
			return true;
		case "按键-终结技":
			Ultimate(press, pressTime, release);
			return true;
		case "按键-连携技-左":
			ChainLeft(press, pressTime, release);
			return true;
		case "按键-连携技-右":
			ChainRight(press, pressTime, release);
			return true;
		case "按键-连携技-取消":
			ChainCancel(press, pressTime, release);
			return true;
		case "按键-移动-前":
			MoveW(press, pressTime, release);
			return true;
		case "按键-移动-后":
			MoveS(press, pressTime, release);
			return true;
		case "按键-移动-左":
			MoveA(press, pressTime, release);
			return true;
		case "按键-移动-右":
			MoveD(press, pressTime, release);
			return true;
		case "按键-锁定敌人":
			Lock(press, pressTime, release);
			return true;
		case "按键-交互":
			Interact(press, pressTime, release);
			return true;
		default:
			return false;
		}
	}

	private bool RunUiAction(string gamepadAction, string keyboardKey)
	{
		UiActionPlan uiActionPlan = CreateUiActionPlan(gamepadAction, keyboardKey);
		Log.Information("ZPcController UI action: action={GamepadAction}, foregroundKey={KeyboardKey}, useBackgroundGamepad={UseBackgroundGamepad}", uiActionPlan.GamepadAction, uiActionPlan.KeyboardKey, uiActionPlan.UseBackgroundGamepadAction);
		if (uiActionPlan.UseBackgroundGamepadAction)
		{
			string gamepadAction2 = uiActionPlan.GamepadAction;
			return Click(null, null, pcAlt: false, gamepadAction2);
		}
		ActionBtn(uiActionPlan.KeyboardKey);
		return true;
	}

	internal UiActionPlan CreateUiActionPlan(string gamepadAction, string keyboardKey)
	{
		return new UiActionPlan(base.IsBackgroundMode, gamepadAction, keyboardKey);
	}

	internal string GetActionKeyForTesting(string actionName)
	{
		if (1 == 0)
		{
		}
		string result;
		if (!(actionName == "按键-特殊攻击"))
		{
			if (!(actionName == "按键-移动-前"))
			{
				throw new ArgumentOutOfRangeException("actionName", actionName, "未知动作");
			}
			result = _actionKeyMoveW;
		}
		else
		{
			result = _actionKeySpecialAttack;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	/// <summary>开始向前移动</summary>
	public void StartMovingForward()
	{
		if (!_isMoving)
		{
			_isMoving = true;
			MoveW(press: true);
		}
	}

	/// <summary>停止向前移动</summary>
	public void StopMovingForward()
	{
		_isMoving = false;
		MoveW(press: false, null, release: true);
	}

	/// <summary>
	/// 横向距离转向
	/// </summary>
	/// <param name="d">正数右转，负数左转</param>
	public void TurnByDistance(float d)
	{
		MoveMouseRelative(d, 0f);
	}

	/// <summary>
	/// 按角度相对转向
	/// </summary>
	/// <param name="angleDiff">角度差，逆时针为正</param>
	public void TurnByAngleDiff(float angleDiff)
	{
		TurnByDistance(_gameConfig.TurnDx * angleDiff);
	}

	/// <summary>更新运行期鼠标转向系数。</summary>
	public void UpdateTurnDx(float turnDx)
	{
		_gameConfig.TurnDx = turnDx;
	}

	/// <summary>更新运行期手柄转向速度。</summary>
	public void UpdateGamepadTurnSpeed(float gamepadTurnSpeed)
	{
		_gameConfig.GamepadTurnSpeed = gamepadTurnSpeed;
	}

	/// <summary>
	/// 纵向距离转向
	/// </summary>
	/// <param name="d">正数下转，负数上转</param>
	public void TurnVerticalByDistance(float d)
	{
		MoveMouseRelative(0f, d);
	}

	/// <summary>
	/// 推动后台手柄右摇杆。
	/// </summary>
	public bool MoveGamepadRightStick(float x, float y, TimeSpan duration)
	{
		return TryMoveBackgroundGamepadRightStick(x, y, duration);
	}

	private void MoveMouseRelative(float dx, float dy)
	{
		if (!IsInputAllowed || (dx == 0f && dy == 0f))
		{
			return;
		}
		if (base.IsBackgroundMode)
		{
			float num = Math.Max(Math.Abs(dx), Math.Abs(dy));
			float gamepadTurnSpeed = _gameConfig.GamepadTurnSpeed;
			if (!(num <= 0f) && !(gamepadTurnSpeed <= 0f))
			{
				EnsureGamepadMode();
				float x = dx / num;
				float y = (0f - dy) / num;
				TimeSpan duration = TimeSpan.FromSeconds(num / gamepadTurnSpeed);
				TryMoveBackgroundGamepadRightStick(x, y, duration);
			}
		}
		else
		{
			EnsureMouseMode();
			_mouseMoveRelative(dx, dy);
		}
	}

	private static void MouseMoveRelativeByUser32(float dx, float dy)
	{
		new WindowsNativeInputSender().MoveMouseRelative((int)dx, (int)dy);
	}

	private void ApplyBackgroundGamepadMode()
	{
		string text = (string.Equals(_gameConfig.BackgroundGamepadType, "ds4", StringComparison.OrdinalIgnoreCase) ? "ds4" : "xbox");
		SetBackgroundGamepadType(text, loadDefaultActionKeys: false);
		SetGamepadActionKeys(CreateGamepadActionKeys(text));
		EnableBackgroundMode();
		RefreshActionKeys(text);
		float keyPressSeconds = ((text == "ds4") ? _gameConfig.Ds4KeyPressTime : _gameConfig.XboxKeyPressTime);
		SetBackgroundGamepadKeyPressTime(TimeSpan.FromSeconds(keyPressSeconds));
	}

	private IReadOnlyDictionary<string, IReadOnlyList<string>> CreateGamepadActionKeys(string gamepadType)
	{
		return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
		{
			["menu"] = NormalizeSequence((gamepadType == "ds4") ? _gameConfig.Ds4ActionMenu : _gameConfig.XboxActionMenu).ToArray(),
			["map"] = NormalizeSequence((gamepadType == "ds4") ? _gameConfig.Ds4ActionMap : _gameConfig.XboxActionMap).ToArray(),
			["minimap"] = NormalizeSequence((gamepadType == "ds4") ? _gameConfig.Ds4ActionMinimap : _gameConfig.XboxActionMinimap).ToArray(),
			["compendium"] = NormalizeSequence((gamepadType == "ds4") ? _gameConfig.Ds4ActionCompendium : _gameConfig.XboxActionCompendium).ToArray(),
			["function_menu"] = NormalizeSequence((gamepadType == "ds4") ? _gameConfig.Ds4ActionFunctionMenu : _gameConfig.XboxActionFunctionMenu).ToArray()
		};
		IEnumerable<string> NormalizeSequence(IEnumerable<string> keys)
		{
			return keys.Select((string key) => NormalizeGamepadKey(gamepadType, key));
		}
	}

	private static string NormalizeGamepadKey(string gamepadType, string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return key;
		}
		if (key.StartsWith("xbox_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("ds4_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("dpad_", StringComparison.OrdinalIgnoreCase))
		{
			return key;
		}
		return gamepadType + "_" + key;
	}
}

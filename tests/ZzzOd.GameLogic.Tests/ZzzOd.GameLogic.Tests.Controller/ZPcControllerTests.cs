using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Input;
using OpenCvSharp;
using Xunit;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.ScreenArea;
using ZzzOd.GameLogic.Tests.TestSupport;

namespace ZzzOd.GameLogic.Tests.Controller;

public class ZPcControllerTests
{
	private sealed record RecordingStickMove(float X, float Y, TimeSpan Duration);

	private sealed class RecordingGamepadController : RecordingButtonController, IAnalogGamepadController
	{
		public List<RecordingStickMove> StickMoves { get; } = new List<RecordingStickMove>();

		public void MoveRightStick(float x, float y, TimeSpan duration)
		{
			StickMoves.Add(new RecordingStickMove(x, y, duration));
		}
	}

	private class RecordingButtonController : IButtonController
	{
		public List<string> Taps { get; } = new List<string>();

		public List<IReadOnlyList<string>> ComboTaps { get; } = new List<IReadOnlyList<string>>();

		public List<(string Key, TimeSpan? PressTime)> Presses { get; } = new List<(string, TimeSpan?)>();

		public List<string> Releases { get; } = new List<string>();

		public int ResetCount { get; private set; }

		public void Tap(string key)
		{
			Taps.Add(key);
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
			ComboTaps.Add(keys.ToArray());
		}

		public void Press(string key, TimeSpan? pressTime = null)
		{
			Presses.Add((key, pressTime));
		}

		public void Release(string key)
		{
			Releases.Add(key);
		}

		public void Reset()
		{
			ResetCount++;
		}
	}

	private sealed class RecordingInputController : IInputController
	{
		public IButtonController ButtonController { get; }

		public List<(OneDragon.Core.Abstractions.Geometry.Point? Position, TimeSpan? PressTime, bool Primary)> Clicks { get; } = new List<(OneDragon.Core.Abstractions.Geometry.Point?, TimeSpan?, bool)>();

		public List<(OneDragon.Core.Abstractions.Geometry.Point End, OneDragon.Core.Abstractions.Geometry.Point? Start, TimeSpan? Duration)> Drags { get; } = new List<(OneDragon.Core.Abstractions.Geometry.Point, OneDragon.Core.Abstractions.Geometry.Point?, TimeSpan?)>();

		public List<(int Clicks, OneDragon.Core.Abstractions.Geometry.Point? Position)> Scrolls { get; } = new List<(int, OneDragon.Core.Abstractions.Geometry.Point?)>();

		public List<string> InputTexts { get; } = new List<string>();

		public List<OneDragon.Core.Abstractions.Geometry.Point> MouseMoves { get; } = new List<OneDragon.Core.Abstractions.Geometry.Point>();

		public RecordingInputController(IButtonController buttonController)
		{
			ButtonController = buttonController;
		}

		public bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool primary = true)
		{
			Clicks.Add((position, pressTime, primary));
			return true;
		}

		public void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
		{
			Drags.Add((end, start, duration));
		}

		public void Scroll(int clicks, OneDragon.Core.Abstractions.Geometry.Point? position = null)
		{
			Scrolls.Add((clicks, position));
		}

		public void InputText(string text)
		{
			InputTexts.Add(text);
		}

		public void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
		{
			MouseMoves.Add(position);
		}
	}

	[Fact]
	public void Constructor_WithValidConfig_DoesNotThrow()
	{
		GameConfig config = new GameConfig();
		Exception ex = Record.Exception(() => new ZPcController(config, null, 1920, 1080, null, null, null, null, null, null, skipForegroundActivation: true));
		Assert.Null(ex);
	}

	[Fact]
	public void ApplyRuntimeControlMode_BackgroundDs4ModeUsesGamepadButtonsAndActions()
	{
		GameConfig obj = new GameConfig
		{
			BackgroundMode = true,
			BackgroundGamepadType = "ds4",
			Ds4KeyDodge = "cross"
		};
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "l2";
		span[1] = "cross";
		obj.Ds4ActionCompendium = list;
		GameConfig gameConfig = obj;
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		RecordingButtonController recordingButtonController2 = new RecordingButtonController();
		RecordingGamepadController recordingGamepadController = new RecordingGamepadController();
		RecordingInputController foregroundInputController = new RecordingInputController(recordingButtonController);
		RecordingInputController backgroundInputController = new RecordingInputController(recordingButtonController2);
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, foregroundInputController, backgroundInputController, recordingButtonController, recordingButtonController2, recordingGamepadController, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		zPcController.Dodge();
		zPcController.OpenCompendium();
		Assert.True(zPcController.IsBackgroundMode);
		Assert.Contains("ds4_cross", (IEnumerable<string>)recordingGamepadController.Taps);
		Assert.Contains((IEnumerable<IReadOnlyList<string>>)recordingGamepadController.ComboTaps, (Predicate<IReadOnlyList<string>>)((IReadOnlyList<string> combo) => combo.SequenceEqual(new string[2] { "ds4_l2", "ds4_cross" })));
		Assert.Empty(recordingButtonController2.Taps);
		Assert.Empty(recordingButtonController2.ComboTaps);
	}

	[Fact]
	public void ApplyRuntimeControlMode_ForegroundKeyboardModeUsesConfiguredKeys()
	{
		GameConfig gameConfig = new GameConfig
		{
			KeySpecialAttack = "j"
		};
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, new RecordingInputController(recordingButtonController), null, recordingButtonController, null, null, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		Assert.Equal("j", zPcController.GetActionKeyForTesting("按键-特殊攻击"));
	}

	[Fact]
	public void OpenMap_ForegroundModePlansKeyboardMapKey()
	{
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		RecordingInputController recordingInputController = new RecordingInputController(recordingButtonController);
		ZPcController zPcController = new ZPcController(new GameConfig
		{
			BackgroundMode = false
		}, null, 1920, 1080, null, recordingInputController, null, recordingButtonController, null, null, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		UiActionPlan uiActionPlan = zPcController.CreateUiActionPlan("map", "j");
		Assert.False(uiActionPlan.UseBackgroundGamepadAction);
		Assert.Equal("map", uiActionPlan.GamepadAction);
		Assert.Equal("j", uiActionPlan.KeyboardKey);
		Assert.Empty(recordingButtonController.Taps);
		Assert.Empty(recordingInputController.Clicks);
	}

	[Fact]
	public void ApplyRuntimeControlMode_ForegroundModeIgnoresControlMethodForOrdinaryUiClick()
	{
		GameConfig gameConfig = new GameConfig
		{
			BackgroundMode = false,
			ControlMethod = "xbox"
		};
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		RecordingButtonController recordingButtonController2 = new RecordingButtonController();
		RecordingGamepadController recordingGamepadController = new RecordingGamepadController();
		RecordingInputController recordingInputController = new RecordingInputController(recordingButtonController);
		RecordingInputController recordingInputController2 = new RecordingInputController(recordingButtonController2);
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, recordingInputController, recordingInputController2, recordingButtonController, recordingButtonController2, recordingGamepadController, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		bool condition = zPcController.Click(new OneDragon.Core.Abstractions.Geometry.Point(960, 540), null, pcAlt: false, "compendium");
		bool condition2 = zPcController.OpenMap();
		Assert.True(condition);
		Assert.True(condition2);
		Assert.False(zPcController.IsBackgroundMode);
		Assert.Single(recordingInputController.Clicks);
		Assert.Equal("j", Assert.Single(recordingButtonController.Taps));
		Assert.Empty(recordingInputController2.Clicks);
		Assert.Empty(recordingButtonController2.Taps);
		Assert.Empty(recordingButtonController2.ComboTaps);
		Assert.Empty(recordingGamepadController.Taps);
		Assert.Empty(recordingGamepadController.ComboTaps);
	}

	[Fact]
	public void ApplyRuntimeControlMode_ForegroundKeyboardModeUsesConfiguredMoveKey()
	{
		GameConfig gameConfig = new GameConfig
		{
			KeyMoveW = "up"
		};
		RecordingButtonController recordingButtonController = new RecordingButtonController();
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, new RecordingInputController(recordingButtonController), null, recordingButtonController, null, null, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		Assert.Equal("up", zPcController.GetActionKeyForTesting("按键-移动-前"));
	}

	[Fact]
	public void TurnByDistance_BackgroundModeUsesGamepadRightStick()
	{
		GameConfig gameConfig = new GameConfig
		{
			BackgroundMode = true,
			BackgroundGamepadType = "xbox",
			GamepadTurnSpeed = 1000f
		};
		RecordingGamepadController recordingGamepadController = new RecordingGamepadController();
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, null, new RecordingInputController(new RecordingButtonController()), null, new RecordingButtonController(), recordingGamepadController, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		zPcController.TurnByDistance(250f);
		RecordingStickMove recordingStickMove = Assert.Single(recordingGamepadController.StickMoves);
		Assert.Equal(1f, recordingStickMove.X);
		Assert.Equal(0f, recordingStickMove.Y);
		Assert.Equal(TimeSpan.FromMilliseconds(250L), recordingStickMove.Duration);
	}

	[Fact]
	public void TurnVerticalByDistance_BackgroundModeUsesRightStickY()
	{
		GameConfig gameConfig = new GameConfig
		{
			BackgroundMode = true,
			BackgroundGamepadType = "xbox",
			GamepadTurnSpeed = 500f
		};
		RecordingGamepadController recordingGamepadController = new RecordingGamepadController();
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, null, new RecordingInputController(new RecordingButtonController()), null, new RecordingButtonController(), recordingGamepadController, skipForegroundActivation: true);
		zPcController.ApplyRuntimeControlMode();
		zPcController.TurnVerticalByDistance(125f);
		RecordingStickMove recordingStickMove = Assert.Single(recordingGamepadController.StickMoves);
		Assert.Equal(0f, recordingStickMove.X);
		Assert.Equal(-1f, recordingStickMove.Y);
		Assert.Equal(TimeSpan.FromMilliseconds(250L), recordingStickMove.Duration);
	}

	[Fact]
	public void TurnByAngleDiff_ForegroundModeUsesInjectedMouseMover()
	{
		List<(float Dx, float Dy)> moves = new List<(float, float)>();
		GameConfig gameConfig = new GameConfig
		{
			TurnDx = 10f
		};
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, null, null, null, null, null, skipForegroundActivation: true, delegate(float dx, float dy)
		{
			moves.Add((dx, dy));
		});
		zPcController.TurnByAngleDiff(25f);
		int num = 1;
		List<(float, float)> list = new List<(float, float)>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = (250f, 0f);
		Assert.Equal<List<(float, float)>>(list, moves);
	}

	[Fact]
	public void TurnByAngleDiff_UsesUpdatedRuntimeTurnDx()
	{
		List<(float Dx, float Dy)> moves = new List<(float, float)>();
		GameConfig gameConfig = new GameConfig
		{
			TurnDx = 10f
		};
		ZPcController zPcController = new ZPcController(gameConfig, null, 1920, 1080, null, null, null, null, null, null, skipForegroundActivation: true, delegate(float dx, float dy)
		{
			moves.Add((dx, dy));
		});
		zPcController.UpdateTurnDx(20f);
		zPcController.TurnByAngleDiff(25f);
		Assert.Equal(20f, gameConfig.TurnDx);
		int num = 1;
		List<(float, float)> list = new List<(float, float)>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = (500f, 0f);
		Assert.Equal<List<(float, float)>>(list, moves);
	}

	[Fact]
	public void FillUidBlack_UsesYoloDefaultColor()
	{
		OpenCvTestRuntime.RequireAvailable();
		ZPcController obj = new ZPcController(new GameConfig(), null, 1920, 1080, null, null, null, null, null, null, skipForegroundActivation: true);
		using Mat mat = new Mat(new Size(1920, 1080), MatType.CV_8UC3, Scalar.All(0.0));
		MethodInfo method = typeof(ZPcController).GetMethod("FillUidBlack", BindingFlags.Instance | BindingFlags.NonPublic);
		Mat mat2 = (Mat)method.Invoke(obj, new object[1] { mat });
		OneDragon.Core.Abstractions.Geometry.Rect rect = ScreenNormalWorldEnum.Uid.Rect;
		Vec3b vec3b = mat2.At<Vec3b>(rect.Y1 + 1, rect.X1 + 1);
		Assert.Same(mat, mat2);
		Assert.Equal((byte)GameConst.YoloDefaultColor.Val0, vec3b.Item0);
		Assert.Equal((byte)GameConst.YoloDefaultColor.Val1, vec3b.Item1);
		Assert.Equal((byte)GameConst.YoloDefaultColor.Val2, vec3b.Item2);
	}
}

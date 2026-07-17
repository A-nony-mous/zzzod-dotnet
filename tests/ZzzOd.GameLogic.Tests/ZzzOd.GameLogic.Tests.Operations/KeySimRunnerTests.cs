using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Input;
using OneDragon.Core.Runtime;
using Xunit;
using ZzzOd.GameLogic.Config;
using ZzzOd.GameLogic.Context;
using ZzzOd.GameLogic.Controller;
using ZzzOd.GameLogic.Operations;

namespace ZzzOd.GameLogic.Tests.Operations;

public sealed class KeySimRunnerTests : IDisposable
{
	private sealed class RecordingButtonController : IButtonController
	{
		public List<string> Taps { get; } = new List<string>();

		public List<(string Key, TimeSpan? PressTime)> Presses { get; } = new List<(string, TimeSpan?)>();

		public List<string> Releases { get; } = new List<string>();

		public int ResetCount { get; private set; }

		public void Tap(string key)
		{
			Taps.Add(key);
		}

		public void TapCombo(IReadOnlyList<string> keys)
		{
			Taps.Add(string.Join("+", keys));
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

	private sealed class RecordingInputController(IButtonController buttonController) : IInputController
	{
		public IButtonController ButtonController { get; } = buttonController;

		public bool Click(Point? position = null, TimeSpan? pressTime = null, bool primary = true)
		{
			return true;
		}

		public void DragTo(Point end, Point? start = null, TimeSpan? duration = null)
		{
		}

		public void Scroll(int clicks, Point? position = null)
		{
		}

		public void InputText(string text)
		{
		}

		public void MouseMove(Point position)
		{
		}
	}

	private readonly string _rootDirectory;

	public KeySimRunnerTests()
	{
		_rootDirectory = Path.Combine(Path.GetTempPath(), "zzzod-key-sim-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_rootDirectory, "config", "key_sim"));
	}

	[Fact]
	public async Task ExecuteAsync_LoadsSampleConfigAndRunsNamedControllerActions()
	{
		File.WriteAllText(Path.Combine(_rootDirectory, "config", "key_sim", "demo.sample.yml"), "operations:\n  - op_name: \"按键-移动-前-按下\"\n  - op_name: \"等待秒数\"\n    seconds: 0.2\n  - op_name: \"按键-闪避\"\n    post_delay: 0.1\n  - op_name: \"按键-移动-右-按下\"\n    press: 3\n  - op_name: \"按键-移动-前-松开\"");
		RecordingButtonController buttons = new RecordingButtonController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(buttons), null, buttons, null, null, skipForegroundActivation: true);
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		context.AttachController(controller);
		List<TimeSpan> waits = new List<TimeSpan>();
		KeySimRunner operation = new KeySimRunner(context, "demo", waits.Add);
		OperationResult result = await operation.ExecuteAsync();
		Assert.True(result.IsSuccess);
		Assert.Equal("执行完成", result.Status);
		Assert.Contains<(string, TimeSpan?)>(buttons.Presses, delegate((string Key, TimeSpan? PressTime) press)
		{
			int result2;
			if (press.Key == "w")
			{
				TimeSpan? item = press.PressTime;
				result2 = ((!item.HasValue) ? 1 : 0);
			}
			else
			{
				result2 = 0;
			}
			return (byte)result2 != 0;
		});
		Assert.Contains<(string, TimeSpan?)>(buttons.Presses, ((string Key, TimeSpan? PressTime) press) => press.Key == "d" && press.PressTime == TimeSpan.FromSeconds(3L));
		Assert.Contains("shift", (IEnumerable<string>)buttons.Taps);
		Assert.Contains("w", (IEnumerable<string>)buttons.Releases);
		Assert.Equal(new List<TimeSpan>(2)
		{
			TimeSpan.FromSeconds(0.2),
			TimeSpan.FromSeconds(0.1)
		}, waits);
	}

	[Fact]
	public async Task ExecuteAsync_UnknownOperationFailsWithDiagnosticStatus()
	{
		File.WriteAllText(Path.Combine(_rootDirectory, "config", "key_sim", "bad.yml"), "operations:\n  - op_name: \"按键-不存在\"");
		RecordingButtonController buttons = new RecordingButtonController();
		ZPcController controller = new ZPcController(new GameConfig(), null, 1920, 1080, null, new RecordingInputController(buttons), null, buttons, null, null, skipForegroundActivation: true);
		using ZContext context = new ZContext(new OneDragonEnvironment(_rootDirectory, _rootDirectory));
		context.AttachController(controller);
		KeySimRunner operation = new KeySimRunner(context, "bad", delegate
		{
		});
		OperationResult result = await operation.ExecuteAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("非法的指令 按键-不存在", result.Status);
		Assert.Empty(buttons.Taps);
		Assert.Empty(buttons.Presses);
		Assert.Empty(buttons.Releases);
	}

	public void Dispose()
	{
		if (Directory.Exists(_rootDirectory))
		{
			Directory.Delete(_rootDirectory, recursive: true);
		}
	}
}

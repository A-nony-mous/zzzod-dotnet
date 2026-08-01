using System;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Opens the configured ZZZ executable.
/// </summary>
public sealed class OpenGame : ZOperation
{
	private readonly Func<string, bool> _startCommand;

	private readonly TimeSpan _successDelay;

	/// <summary>
	/// Initialize the open-game operation.
	/// </summary>
	public OpenGame(ZContext context, Func<string, bool>? startCommand = null, TimeSpan? successDelay = null)
		: base(context, "打开游戏", needCheckGameWindow: false)
	{
		_startCommand = startCommand ?? new Func<string, bool>(StartCommandWithProcess);
		_successDelay = successDelay ?? TimeSpan.FromSeconds(5L);
	}

	[OperationNode("打开游戏", IsStartNode = true, ScreenshotBeforeRound = false)]
	private OperationRoundResult Open()
	{
		string gamePath = base.ZContext.GameAccountConfig.GamePath;
		if (string.IsNullOrWhiteSpace(gamePath))
		{
			return RoundFail("未配置游戏路径，请前往 [ 账户管理 ] -> [ 游戏路径 ] 手动设置");
		}
		string arg = OpenGameCommandBuilder.Build(gamePath, base.ZContext.GameConfig.LaunchArgument, base.ZContext.GameConfig.ScreenSize, base.ZContext.GameConfig.FullScreen, base.ZContext.GameConfig.PopupWindow, base.ZContext.GameConfig.Monitor, base.ZContext.GameConfig.LaunchArgumentAdvance);
		return _startCommand(arg) ? RoundSuccess("打开游戏", null, _successDelay) : RoundFail("打开游戏失败");
	}

	private static bool StartCommandWithProcess(string command)
	{
		return OpenGameProcessLauncher.Start(command);
	}
}

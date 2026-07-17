namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 覆盖验证方式。
/// </summary>
public enum E2EVerificationMode
{
	/// <summary>真实游戏窗口端到端测试。</summary>
	RealGameE2E,
	/// <summary>截图或固定资产回归。</summary>
	ScreenshotOrFixedAssetRegression,
	/// <summary>纯逻辑测试。</summary>
	PureLogic,
	/// <summary>受账号、前置条件、安全或环境限制阻塞。</summary>
	Blocked
}

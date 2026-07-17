namespace ZzzOd.GameLogic.Application;

/// <summary>
/// ZZZ 应用启动器初始化步骤。
/// </summary>
public sealed record ZApplicationLauncherInitializationStep(ZApplicationLauncherInitializationStage Stage, string Source, string Description);

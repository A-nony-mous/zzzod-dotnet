namespace ZzzOd.AppHost.Backend;

/// <summary>保存挑战配置请求。</summary>
/// <param name="OriginalModuleName">重命名前名称。</param>
/// <param name="Config">待保存配置。</param>
public sealed record ZzzSaveLostVoidChallengeConfigRequest(string? OriginalModuleName, ZzzLostVoidChallengeConfigDto Config);

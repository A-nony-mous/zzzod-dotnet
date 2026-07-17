namespace ZzzOd.AppHost.Backend;

/// <summary>挑战配置摘要。</summary>
/// <param name="ModuleName">配置名称。</param>
/// <param name="IsSample">是否只读 sample。</param>
public sealed record ZzzLostVoidChallengeSummaryDto(string ModuleName, bool IsSample);

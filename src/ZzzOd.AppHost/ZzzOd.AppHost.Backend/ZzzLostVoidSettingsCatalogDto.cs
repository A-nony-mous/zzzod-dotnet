using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>迷失之地基础设置目录。</summary>
/// <param name="Missions">真实副本目录。</param>
/// <param name="ChallengeConfigs">真实挑战配置目录。</param>
/// <param name="RunRecord">运行记录。</param>
public sealed record ZzzLostVoidSettingsCatalogDto(IReadOnlyList<string> Missions, IReadOnlyList<string> ChallengeConfigs, ZzzLostVoidRunRecordDto RunRecord);

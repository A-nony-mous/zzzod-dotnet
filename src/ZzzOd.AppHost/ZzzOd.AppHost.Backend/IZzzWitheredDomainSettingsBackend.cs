using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 枯萎之都设置页专用真实后端。
/// </summary>
public interface IZzzWitheredDomainSettingsBackend
{
	/// <summary>读取基础目录、挑战配置和运行记录。</summary>
	ZzzBackendResult<ZzzWitheredDomainSettingsCatalogDto> GetWitheredDomainSettingsCatalog(int instanceIndex);

	/// <summary>保存或重命名用户挑战配置。</summary>
	ZzzBackendResult<ZzzWitheredDomainChallengeConfigDto> SaveWitheredDomainChallengeConfig(ZzzSaveWitheredDomainChallengeConfigRequest request);

	/// <summary>删除用户挑战配置。</summary>
	ZzzBackendResult<IReadOnlyList<ZzzWitheredDomainChallengeConfigDto>> DeleteWitheredDomainChallengeConfig(string moduleName);

	/// <summary>重置指定实例的枯萎之都每周运行记录。</summary>
	ZzzBackendResult<ZzzWitheredDomainRunRecordDto> ResetWitheredDomainRunRecord(int instanceIndex);
}

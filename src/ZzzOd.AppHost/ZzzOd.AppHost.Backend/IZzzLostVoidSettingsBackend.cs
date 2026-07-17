namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 迷失之地设置页专用真实后端。
/// </summary>
public interface IZzzLostVoidSettingsBackend
{
	/// <summary>读取基础设置目录和运行记录。</summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>设置目录。</returns>
	ZzzBackendResult<ZzzLostVoidSettingsCatalogDto> GetLostVoidSettingsCatalog(int instanceIndex);

	/// <summary>重置日、周运行记录。</summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>重置后的记录。</returns>
	ZzzBackendResult<ZzzLostVoidRunRecordDto> ResetLostVoidRunRecord(int instanceIndex);

	/// <summary>读取挑战配置编辑器目录。</summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>编辑器目录。</returns>
	ZzzBackendResult<ZzzLostVoidChallengeCatalogDto> GetLostVoidChallengeCatalog(int instanceIndex);

	/// <summary>读取指定挑战配置。</summary>
	/// <param name="moduleName">配置名称。</param>
	/// <returns>挑战配置。</returns>
	ZzzBackendResult<ZzzLostVoidChallengeConfigDto> GetLostVoidChallengeConfig(string moduleName);

	/// <summary>按 BaselineParity 命名规则创建未落盘草稿。</summary>
	/// <returns>新配置草稿。</returns>
	ZzzBackendResult<ZzzLostVoidChallengeConfigDto> CreateLostVoidChallengeConfigDraft();

	/// <summary>复制现有配置为未落盘草稿。</summary>
	/// <param name="moduleName">来源配置名称。</param>
	/// <returns>复制草稿。</returns>
	ZzzBackendResult<ZzzLostVoidChallengeConfigDto> CopyLostVoidChallengeConfigDraft(string moduleName);

	/// <summary>保存或重命名挑战配置。</summary>
	/// <param name="request">保存请求。</param>
	/// <returns>保存后的配置。</returns>
	ZzzBackendResult<ZzzLostVoidChallengeConfigDto> SaveLostVoidChallengeConfig(ZzzSaveLostVoidChallengeConfigRequest request);

	/// <summary>删除用户挑战配置。</summary>
	/// <param name="moduleName">配置名称。</param>
	/// <returns>是否删除。</returns>
	ZzzBackendResult<bool> DeleteLostVoidChallengeConfig(string moduleName);

	/// <summary>按 BaselineParity 规则解析优先级文本。</summary>
	/// <param name="kind">文本类型。</param>
	/// <param name="text">多行文本。</param>
	/// <returns>解析结果。</returns>
	ZzzBackendResult<ZzzLostVoidPriorityParseDto> ParseLostVoidPriority(ZzzLostVoidPriorityKind kind, string text);
}

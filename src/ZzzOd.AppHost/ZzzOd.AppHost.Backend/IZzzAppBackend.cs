using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// GUI 与 API 共用的 ZZZ 业务门面。
/// </summary>
public interface IZzzAppBackend
{
	/// <summary>
	/// 获取健康检查信息。
	/// </summary>
	/// <returns>健康检查结果。</returns>
	ZzzBackendResult<ZzzHealthDto> GetHealth();

	/// <summary>
	/// 获取实例列表。
	/// </summary>
	/// <returns>实例列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> GetInstances();

	/// <summary>
	/// 获取当前实例。
	/// </summary>
	/// <returns>当前实例。</returns>
	ZzzBackendResult<ZzzInstanceDto> GetCurrentInstance();

	/// <summary>
	/// 激活实例。
	/// </summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>实例列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> ActivateInstance(int instanceIndex);

	/// <summary>
	/// 新增实例。
	/// </summary>
	/// <returns>实例列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> CreateInstance();

	/// <summary>
	/// 更新实例元数据。
	/// </summary>
	/// <param name="request">更新请求。</param>
	/// <returns>实例列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> UpdateInstance(ZzzUpdateInstanceRequest request);

	/// <summary>
	/// 删除实例。
	/// </summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>实例列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzInstanceDto>> DeleteInstance(int instanceIndex);

	/// <summary>
	/// 对指定实例执行登录操作。
	/// </summary>
	/// <param name="instanceIndex">实例编号。</param>
	/// <returns>运行状态。</returns>
	ZzzBackendResult<ZzzRunStatusDto> LoginInstance(int instanceIndex);

	/// <summary>
	/// 获取应用列表。
	/// </summary>
	/// <returns>应用列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzAppDto>> GetApps();

	/// <summary>
	/// 按 BaselineParity RunContext.default_group_apps 注册顺序获取独立运行可添加应用。
	/// </summary>
	/// <returns>默认组应用列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzAppDto>> GetStandaloneApps()
	{
		return GetApps();
	}

	/// <summary>
	/// 按 BaselineParity ApplicationGroupManager 语义读取一条龙应用列表。
	/// </summary>
	ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> GetOneDragonApps(int? instanceIndex = null);

	/// <summary>
	/// 保存一条龙已注册应用的顺序和启用状态，并保留配置中的未注册应用原位。
	/// </summary>
	ZzzBackendResult<IReadOnlyList<ZzzOneDragonAppDto>> SaveOneDragonApps(ZzzSaveOneDragonAppsRequest request);

	/// <summary>
	/// 获取体力计划页面使用的真实手册、预备编队和自动战斗配置目录。
	/// </summary>
	ZzzBackendResult<ZzzChargePlanCatalogDto> GetChargePlanCatalog();

	/// <summary>
	/// 重置指定实例的式舆防卫战运行记录。
	/// </summary>
	ZzzBackendResult<ZzzShiyuDefenseRunRecordDto> ResetShiyuDefenseRunRecord(int instanceIndex);

	/// <summary>
	/// 获取真实自动战斗与闪避配置目录。
	/// </summary>
	ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> GetBattleAssistantConfigCatalog();

	/// <summary>
	/// 删除普通自动战斗或闪避配置，并返回刷新后的目录。
	/// </summary>
	ZzzBackendResult<ZzzBattleAssistantConfigCatalogDto> DeleteBattleAssistantConfig(ZzzDeleteBattleAssistantConfigRequest request);

	/// <summary>
	/// 获取真实战斗助手任务和状态快照。
	/// </summary>
	ZzzBackendResult<ZzzBattleAssistantRuntimeDto> GetBattleAssistantRuntime();

	/// <summary>
	/// 获取生命热线当前实例的真实当日运行记录。
	/// </summary>
	ZzzBackendResult<ZzzLifeOnLineRunRecordDto> GetLifeOnLineRunRecord(int? instanceIndex = null);

	/// <summary>
	/// 订阅真实自动战斗指令加载事件。
	/// </summary>
	void SubscribeBattleAssistantOperationLoaded(Action callback);

	/// <summary>
	/// 取消订阅真实自动战斗指令加载事件。
	/// </summary>
	void UnsubscribeBattleAssistantOperationLoaded(Action callback);

	/// <summary>
	/// 获取最近日志。
	/// </summary>
	/// <param name="limit">最大条数。</param>
	/// <returns>日志列表。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzLogEntryDto>> GetRecentLogs(int limit = 200);

	/// <summary>
	/// 获取配置 scope 描述。
	/// </summary>
	/// <returns>配置 scope 描述。</returns>
	ZzzBackendResult<IReadOnlyList<ZzzConfigScopeDescriptorDto>> GetConfigScopes();

	/// <summary>
	/// 读取配置 scope。
	/// </summary>
	/// <param name="scope">scope 名称。</param>
	/// <param name="instanceIndex">实例编号。</param>
	/// <param name="groupId">应用组编号。</param>
	/// <returns>scope 值。</returns>
	ZzzBackendResult<ZzzConfigScopeValuesDto> GetConfigScope(string scope, int? instanceIndex = null, string? groupId = null);

	/// <summary>
	/// 保存配置 scope。
	/// </summary>
	/// <param name="request">保存请求。</param>
	/// <returns>scope 值。</returns>
	ZzzBackendResult<ZzzConfigScopeValuesDto> SaveConfigScope(ZzzSaveConfigScopeRequest request);

	/// <summary>
	/// 获取窗口状态。
	/// </summary>
	/// <returns>窗口状态。</returns>
	ZzzBackendResult<ZzzWindowStatusDto> GetWindow();

	/// <summary>
	/// 获取截图。
	/// </summary>
	/// <returns>截图。</returns>
	ZzzBackendResult<ZzzScreenshotDto> GetScreenshot();

	/// <summary>
	/// 启动运行。
	/// </summary>
	/// <param name="request">启动请求。</param>
	/// <returns>运行状态。</returns>
	Task<ZzzBackendResult<ZzzRunStatusDto>> StartRunAsync(ZzzStartRunRequest request);

	/// <summary>
	/// 暂停当前运行。
	/// </summary>
	/// <returns>运行状态。</returns>
	ZzzBackendResult<ZzzRunStatusDto> PauseRun();

	/// <summary>
	/// 恢复当前运行。
	/// </summary>
	/// <returns>运行状态。</returns>
	ZzzBackendResult<ZzzRunStatusDto> ResumeRun();

	/// <summary>
	/// 停止当前运行。
	/// </summary>
	/// <returns>运行状态。</returns>
	Task<ZzzBackendResult<ZzzRunStatusDto>> StopRunAsync();

	/// <summary>
	/// 获取当前运行状态。
	/// </summary>
	/// <returns>运行状态。</returns>
	ZzzBackendResult<ZzzRunStatusDto> GetCurrentRun();

	/// <summary>
	/// 订阅事件。
	/// </summary>
	/// <returns>事件读取器。</returns>
	ChannelReader<ZzzBackendEvent> SubscribeEvents();

	/// <summary>
	/// 取消事件订阅。
	/// </summary>
	/// <param name="reader">事件读取器。</param>
	void UnsubscribeEvents(ChannelReader<ZzzBackendEvent> reader);
}

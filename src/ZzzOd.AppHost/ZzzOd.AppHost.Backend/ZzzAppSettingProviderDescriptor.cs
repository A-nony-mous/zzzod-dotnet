namespace ZzzOd.AppHost.Backend;

/// <summary>
/// BaselineParity *_app_setting.py provider 的产品迁移状态。
/// </summary>
/// <param name="AppId">应用编号。</param>
/// <param name="SettingType">BaselineParity provider 声明的展示方式。</param>
/// <param name="ImplementedTarget">已迁移的 AXAML 目标；未迁移时为空。</param>
public sealed record ZzzAppSettingProviderDescriptor(string AppId, ZzzAppSettingType SettingType, string? ImplementedTarget)
{
	/// <summary>是否已有可打开的产品目标。</summary>
	public bool IsImplemented => !string.IsNullOrWhiteSpace(ImplementedTarget);
}

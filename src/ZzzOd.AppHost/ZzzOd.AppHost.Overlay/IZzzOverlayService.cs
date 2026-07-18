using System.Collections.Generic;

namespace ZzzOd.AppHost.Overlay;

/// <summary>
/// ZZZ Overlay 服务接口。
/// </summary>
public interface IZzzOverlayService
{
	/// <summary>
	/// 获取当前 Overlay 状态。
	/// </summary>
	/// <returns>Overlay 状态。</returns>
	ZzzOverlayStatusDto GetStatus();

	/// <summary>
	/// 设置 Overlay 是否启用。
	/// </summary>
	/// <param name="enabled">是否启用。</param>
	void SetEnabled(bool enabled);

	/// <summary>
	/// 配置快照展示筛选。
	/// </summary>
	/// <param name="options">展示筛选。</param>
	void ConfigureDisplay(ZzzOverlayDisplayOptionsDto options)
	{
	}

	/// <summary>
	/// 获取同一时刻的 Overlay 运行期快照。
	/// </summary>
	/// <returns>不可变快照。</returns>
	ZzzOverlaySnapshotDto GetSnapshot() => ZzzOverlaySnapshotDto.FromLegacy(GetStatus(), GetLastFrame(), GetPerformanceSamples());

	/// <summary>
	/// 获取最后绘制帧。
	/// </summary>
	/// <returns>最后绘制帧。</returns>
	ZzzOverlayFrameDto? GetLastFrame();

	/// <summary>
	/// 提交运行期性能采样。
	/// </summary>
	/// <param name="sample">性能采样。</param>
	void SubmitPerformanceSample(ZzzOverlayPerformanceSampleDto sample);

	/// <summary>
	/// 获取尚未过期的最新性能采样。
	/// </summary>
	/// <returns>按 BaselineParity 核心指标顺序排列的采样。</returns>
	IReadOnlyList<ZzzOverlayPerformanceSampleDto> GetPerformanceSamples();
}

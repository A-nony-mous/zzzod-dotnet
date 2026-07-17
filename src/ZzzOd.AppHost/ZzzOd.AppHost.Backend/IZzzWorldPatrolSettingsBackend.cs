using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 锄大地设置、路线名单和运行记录真实后端。
/// </summary>
public interface IZzzWorldPatrolSettingsBackend
{
	/// <summary>读取真实区域、路线、名单、自动战斗目录和运行记录。</summary>
	ZzzBackendResult<ZzzWorldPatrolCatalogDto> GetWorldPatrolCatalog(int instanceIndex);

	/// <summary>保存路线名单。</summary>
	ZzzBackendResult<ZzzWorldPatrolCatalogDto> SaveWorldPatrolRouteList(ZzzSaveWorldPatrolRouteListRequest request);

	/// <summary>删除路线名单。</summary>
	ZzzBackendResult<ZzzWorldPatrolCatalogDto> DeleteWorldPatrolRouteList(int instanceIndex, string name);

	/// <summary>保存路线文件和完整操作列表。</summary>
	ZzzBackendResult<ZzzWorldPatrolCatalogDto> SaveWorldPatrolRoute(ZzzSaveWorldPatrolRouteRequest request);

	/// <summary>删除路线文件。</summary>
	ZzzBackendResult<ZzzWorldPatrolCatalogDto> DeleteWorldPatrolRoute(int instanceIndex, string fullId);

	/// <summary>从真实游戏截图计算当前路线位置。</summary>
	ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> CaptureWorldPatrolRoutePosition(ZzzCaptureWorldPatrolRoutePositionRequest request);

	/// <summary>由真实大地图和当前操作列表渲染路线录制图。</summary>
	ZzzBackendResult<ZzzWorldPatrolRouteVisualDto> RenderWorldPatrolRouteRecorder(ZzzWorldPatrolRouteVisualRequest request);

	/// <summary>把 Uniform 显示区域点击坐标转换为真实大地图像素坐标。</summary>
	ZzzBackendResult<ZzzWorldPatrolRoutePositionDto> ConvertWorldPatrolRouteRecorderClick(ZzzWorldPatrolRouteMapClickRequest request);

	/// <summary>按 BaselineParity DebugRouteRunner 语义从指定操作下标调试路线。</summary>
	Task<ZzzBackendResult<ZzzWorldPatrolRouteDebugDto>> DebugWorldPatrolRouteAsync(ZzzDebugWorldPatrolRouteRequest request);

	/// <summary>加载指定区域的真实大地图录制会话。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> LoadWorldPatrolLargeMapRecorder(int instanceIndex, string areaId);

	/// <summary>把当前大地图录制会话保存到真实 road_mask.png 和 icon.yml。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> SaveWorldPatrolLargeMapRecorder(int instanceIndex);

	/// <summary>删除当前区域的真实 road_mask.png 和 icon.yml，并清空当前录制会话；未落盘的新地图也执行清空。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> DeleteWorldPatrolLargeMapRecorder(int instanceIndex);

	/// <summary>取消当前大地图录制会话并释放快照。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> CancelWorldPatrolLargeMapRecorder(int instanceIndex);

	/// <summary>真实截图两次，中间转向 180 度，并合并小地图快照。</summary>
	Task<ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto>> CaptureWorldPatrolLargeMapRecorderAsync(int instanceIndex, double iconThreshold);

	/// <summary>按图标优先、道路兜底的 BaselineParity 顺序定位小地图。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> CalculateWorldPatrolLargeMapRecorderPosition(int instanceIndex, bool useIcon);

	/// <summary>切换小地图重叠显示模式。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> ToggleWorldPatrolLargeMapRecorderOverlap(int instanceIndex);

	/// <summary>按 BaselineParity 当前 copy_road=false 语义合并小地图图标并扩边。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> MergeWorldPatrolLargeMapRecorder(int instanceIndex);

	/// <summary>回退最近一次大地图合并；没有上一步时按 BaselineParity 语义把当前地图和坐标恢复为空。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> UndoWorldPatrolLargeMapRecorder(int instanceIndex);

	/// <summary>按百分比缩放道路掩码并保持 BaselineParity 图标坐标语义。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> ScaleWorldPatrolLargeMapRecorder(int instanceIndex, int percent);

	/// <summary>横移或纵移当前定位坐标。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> MoveWorldPatrolLargeMapRecorder(int instanceIndex, int deltaX, int deltaY);

	/// <summary>通过大地图点击更新当前角色坐标。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> SetWorldPatrolLargeMapRecorderPosition(int instanceIndex, int x, int y);

	/// <summary>替换当前大地图录制会话的图标列表。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> UpdateWorldPatrolLargeMapRecorderIcons(int instanceIndex, IReadOnlyList<ZzzWorldPatrolLargeMapIconDto> icons);

	/// <summary>高亮图标编辑器选中的图标，传入 -1 清除高亮。</summary>
	ZzzBackendResult<ZzzWorldPatrolLargeMapRecorderStateDto> SelectWorldPatrolLargeMapRecorderIcon(int instanceIndex, int iconIndex);

	/// <summary>重置指定实例运行记录。</summary>
	ZzzBackendResult<ZzzWorldPatrolRunRecordDto> ResetWorldPatrolRunRecord(int instanceIndex);
}

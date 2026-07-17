using System;
using Xunit;
using ZzzOd.Gui.Shell;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// GUI 操作状态记录测试。
/// </summary>
public sealed class GuiOperationTrackerTests
{
	/// <summary>
	/// 终态保留操作标识和首个异常。
	/// </summary>
	[Fact]
	public void StartAndCompletePreservesOperationIdentityAndFirstException()
	{
		ZzzGuiOperationTracker zzzGuiOperationTracker = new ZzzGuiOperationTracker();
		Guid guid = zzzGuiOperationTracker.Start("home", "load-notices");
		InvalidOperationException ex = new InvalidOperationException("服务失败");
		zzzGuiOperationTracker.Complete(guid, ZzzGuiOperationState.Failed, null, ex);
		ZzzGuiOperationRecord zzzGuiOperationRecord = Assert.Single(zzzGuiOperationTracker.Records);
		Assert.Equal(guid, zzzGuiOperationRecord.OperationId);
		Assert.Equal("home", zzzGuiOperationRecord.Route);
		Assert.Equal("load-notices", zzzGuiOperationRecord.Operation);
		Assert.Equal(ZzzGuiOperationState.Failed, zzzGuiOperationRecord.State);
		Assert.NotNull(zzzGuiOperationRecord.EndedAt);
		Assert.Same(ex, zzzGuiOperationRecord.FirstException);
	}

	/// <summary>
	/// 取消页面只影响该页面仍在进行的操作。
	/// </summary>
	[Fact]
	public void CancelRouteOnlyEndsLoadingOperationsForThatRoute()
	{
		ZzzGuiOperationTracker zzzGuiOperationTracker = new ZzzGuiOperationTracker();
		Guid homeOperation = zzzGuiOperationTracker.Start("home", "load-media");
		Guid settingsOperation = zzzGuiOperationTracker.Start("settings", "load-settings");
		zzzGuiOperationTracker.CancelRoute("home", "page-leave");
		ZzzGuiOperationRecord zzzGuiOperationRecord = Assert.Single(zzzGuiOperationTracker.Records, (ZzzGuiOperationRecord record) => record.OperationId == homeOperation);
		ZzzGuiOperationRecord zzzGuiOperationRecord2 = Assert.Single(zzzGuiOperationTracker.Records, (ZzzGuiOperationRecord record) => record.OperationId == settingsOperation);
		Assert.Equal(ZzzGuiOperationState.Canceled, zzzGuiOperationRecord.State);
		Assert.Equal("page-leave", zzzGuiOperationRecord.CancellationReason);
		Assert.Equal(ZzzGuiOperationState.Loading, zzzGuiOperationRecord2.State);
	}
}

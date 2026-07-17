using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.E2E;

namespace ZzzOd.GameLogic.Tests.E2E;

/// <summary>
/// 测试 E2E 覆盖矩阵。
/// </summary>
public sealed class E2ECoverageMatrixTests
{
	[Fact]
	public void Matrix_ShouldCoverEveryNonFrontendApplication()
	{
		string[] actual = (from item in E2ECoverageMatrix.Items
			where item.Area == E2ECoverageArea.Application
			select item.Id.Substring("application.".Length)).Order<string>(StringComparer.Ordinal).ToArray();
		Assert.Equal(ZzzApplicationIds.All.Order<string>(StringComparer.Ordinal), actual);
	}

	[Fact]
	public void Matrix_ShouldCoverRequiredBusinessAndRuntimeAreas()
	{
		E2ECoverageArea[] values = Enum.GetValues<E2ECoverageArea>();
		foreach (E2ECoverageArea area in values)
		{
			Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Area == area));
		}
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "autobattle.context"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "hollow.runner"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "input.virtual_gamepad"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "capture.wgc"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "ocr.profile"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "yolo.lost_void"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "audio.dodge_recorder"));
		Assert.Contains((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Id == "notification.push_service"));
	}

	[Fact]
	public void Matrix_ShouldHaveUniqueIdsAndEvidenceForEveryItem()
	{
		string[] collection = (from @group in E2ECoverageMatrix.Items.GroupBy<E2ECoverageMatrixItem, string>((E2ECoverageMatrixItem item) => item.Id, StringComparer.Ordinal)
			where @group.Count() > 1
			select @group.Key).ToArray();
		Assert.Empty(collection);
		Assert.All(E2ECoverageMatrix.Items, delegate(E2ECoverageMatrixItem item)
		{
			Assert.NotEmpty(item.Components);
			Assert.False(string.IsNullOrWhiteSpace(item.Evidence));
			if (item.VerificationMode == E2EVerificationMode.Blocked)
			{
				Assert.False(string.IsNullOrWhiteSpace(item.BlockedReason));
			}
		});
	}

	[Fact]
	public void Matrix_ShouldNotUseDeferredEvidencePlaceholders()
	{
		Assert.DoesNotContain((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.Evidence.Contains("7.4 写入", StringComparison.Ordinal)));
		Assert.DoesNotContain((IEnumerable<E2ECoverageMatrixItem>)E2ECoverageMatrix.Items, (Predicate<E2ECoverageMatrixItem>)((E2ECoverageMatrixItem item) => item.VerificationMode == E2EVerificationMode.RealGameE2E));
		Assert.All(E2ECoverageMatrix.Items.Where((E2ECoverageMatrixItem item) => item.VerificationMode == E2EVerificationMode.Blocked), delegate(E2ECoverageMatrixItem item)
		{
			Assert.Contains("实机 E2E blocked evidence", item.Evidence);
			Assert.Contains("非 E2E 回归 evidence", item.Evidence);
			Assert.Contains("真实游戏窗口", item.BlockedReason);
			Assert.Contains("账号", item.BlockedReason);
		});
	}

	[Fact]
	public void Matrix_ShouldRecordShiyuAndIntelBoardBlockedEvidenceWithRegressionFallback()
	{
		E2ECoverageMatrixItem item = Find("application.shiyu_defense");
		E2ECoverageMatrixItem item2 = Find("operation.shiyu_battle_completion");
		E2ECoverageMatrixItem item3 = Find("application.intel_board");
		E2ECoverageMatrixItem item4 = Find("operation.intel_board_battle_completion");
		AssertBlockedWithReason(item, "账号已解锁式舆防卫战", "ShiyuDefenseAppTests", "ProductionPlaceholderAuditTests");
		AssertBlockedWithReason(item2, "式舆战斗", "DefaultShiyuDefenseBattleServices_RunAutoBattle_UsesPreviousAutoBattleEndResult", "ShiyuDefenseBattle_FailureExitFlowUsesScreenshotsAndReturnsFailure");
		AssertBlockedWithReason(item3, "账号已解锁情报板", "IntelBoardAppTests", "ProductionPlaceholderAuditTests");
		AssertBlockedWithReason(item4, "情报板委托", "DefaultIntelBoardOperationServices_RunBattleAsync_UsesAutoBattleEndResult", "DetectsBackToList");
	}

	[Fact]
	public void Matrix_ShouldRecordLostVoidAndWitheredDomainBlockedEvidenceWithRegressionFallback()
	{
		E2ECoverageMatrixItem item = Find("application.lost_void");
		E2ECoverageMatrixItem item2 = Find("operation.lost_void_failure_recovery");
		E2ECoverageMatrixItem item3 = Find("application.withered_domain");
		E2ECoverageMatrixItem item4 = Find("operation.withered_domain_hollow_runner");
		E2ECoverageMatrixItem e2ECoverageMatrixItem = Find("hollow.runner");
		AssertBlockedWithReason(item, "账号已解锁迷失之地", "LostVoidContextTests", "ProductionPlaceholderAuditTests");
		AssertBlockedWithReason(item2, "真实迷失之地空洞", "ScreenRunLevelRuntime_ConfirmChallengeResult_ClicksConfirmAndWaitsUntilHidden", "RunLevel_ExecuteAsync_TraversesBattleFailureExitGraphWithRecordedRuntimeCalls");
		AssertBlockedWithReason(item3, "账号已解锁枯萎之都", "WitheredDomainAppTests", "HollowRunnerTests");
		AssertBlockedWithReason(item4, "真实枯萎之都空洞", "HollowRunnerWitheredDomainRunner_UsesScreenshotMapSourceUntilMissionComplete", "HollowRunnerWitheredDomainRunner_ReturnsFailureWhenDefaultScreenshotMapCannotDetect");
		AssertBlockedWithReason(e2ECoverageMatrixItem, "真实空洞地图截图", "HollowRunnerTests", "WitheredDomainAppTests");
		AssertLostVoidRealMachinePrerequisites(item);
		AssertLostVoidRealMachinePrerequisites(item2);
		AssertWitheredDomainRealMachinePrerequisites(item3);
		AssertWitheredDomainRealMachinePrerequisites(item4);
		Assert.Contains("真实截图捕获", e2ECoverageMatrixItem.Evidence);
		Assert.Contains("HollowEvent YOLO 检测", e2ECoverageMatrixItem.Evidence);
	}

	[Fact]
	public void Matrix_ShouldRecordCommissionAssistantAndDriveDiscBlockedEvidenceWithRegressionFallback()
	{
		E2ECoverageMatrixItem item = Find("application.commission_assistant");
		E2ECoverageMatrixItem item2 = Find("operation.commission_assistant_detection");
		E2ECoverageMatrixItem item3 = Find("application.drive_disc_dismantle");
		E2ECoverageMatrixItem item4 = Find("operation.drive_disc_dismantle_input");
		AssertBlockedWithReason(item, "账号当前处于可安全自动对话", "CommissionAssistantAppTests", "ProductionPlaceholderAuditTests");
		AssertBlockedWithReason(item2, "委托助手对话", "DefaultServices_ClickDialogConfirm_ClicksMatchedConfirmText", "DefaultServices_HandleFishing_ReturnsUnsupportedWithoutZzzControllerActions");
		AssertBlockedWithReason(item3, "账号当前驱动盘仓库状态", "DriveDiscDismantleAppTests", "ProductionPlaceholderAuditTests");
		AssertBlockedWithReason(item4, "拆解物品可安全操作", "DefaultServices_GotoSalvage_NavigatesFromDriveDiscStorageToSalvageScreen", "DefaultServices_ClickArea_ReturnsFailureWhenAreaTextIsMissing");
		AssertCommissionAssistantRealMachinePrerequisites(item);
		AssertCommissionAssistantRealMachinePrerequisites(item2);
		AssertDriveDiscRealMachinePrerequisites(item3);
		AssertDriveDiscRealMachinePrerequisites(item4);
	}

	private static E2ECoverageMatrixItem Find(string id)
	{
		return E2ECoverageMatrix.Items.Single((E2ECoverageMatrixItem item) => item.Id == id);
	}

	private static void AssertBlockedWithReason(E2ECoverageMatrixItem item, string blockedEvidenceText, string regressionEvidenceText, string extraRegressionEvidenceText)
	{
		Assert.Equal(E2EVerificationMode.Blocked, item.VerificationMode);
		Assert.Contains("未确认真实游戏窗口", item.BlockedReason);
		Assert.Contains("允许真实输入", item.BlockedReason);
		Assert.Contains("账号", item.BlockedReason);
		Assert.Contains("风险", item.BlockedReason);
		Assert.Contains("实机 E2E blocked evidence", item.Evidence);
		Assert.Contains("非 E2E 回归 evidence", item.Evidence);
		Assert.Contains(blockedEvidenceText, item.Evidence);
		Assert.Contains(regressionEvidenceText, item.Evidence);
		Assert.Contains(extraRegressionEvidenceText, item.Evidence);
	}

	private static void AssertLostVoidRealMachinePrerequisites(E2ECoverageMatrixItem item)
	{
		Assert.Contains("真实截图捕获", item.Evidence);
		Assert.Contains("真实 OCR 结果", item.Evidence);
		Assert.Contains("LostVoid YOLO 检测", item.Evidence);
		Assert.Contains("AutoBattle 接管", item.Evidence);
	}

	private static void AssertWitheredDomainRealMachinePrerequisites(E2ECoverageMatrixItem item)
	{
		Assert.Contains("真实截图捕获", item.Evidence);
		Assert.Contains("HollowEvent YOLO 检测", item.Evidence);
		Assert.Contains("AutoBattle 战斗事件接管", item.Evidence);
		Assert.Contains("不直接依赖 OCR", item.Evidence);
	}

	private static void AssertCommissionAssistantRealMachinePrerequisites(E2ECoverageMatrixItem item)
	{
		Assert.Contains("真实截图捕获", item.Evidence);
		Assert.Contains("真实 OCR", item.Evidence);
		Assert.Contains("真实输入", item.Evidence);
		Assert.Contains("自动战斗", item.Evidence);
	}

	private static void AssertDriveDiscRealMachinePrerequisites(E2ECoverageMatrixItem item)
	{
		Assert.Contains("真实截图捕获", item.Evidence);
		Assert.Contains("真实 OCR/模板检测", item.Evidence);
		Assert.Contains("真实输入", item.Evidence);
		Assert.Contains("拆解物品", item.Evidence);
	}
}

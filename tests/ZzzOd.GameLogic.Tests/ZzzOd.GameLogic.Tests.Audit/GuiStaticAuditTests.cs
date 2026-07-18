using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Xunit;
using ZzzOd.Gui.Shell;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// GUI AXAML 和 FluentAvalonia 静态审计。
/// </summary>
public sealed class GuiStaticAuditTests
{
	private sealed record GuiAuditResult(IReadOnlyList<string> MissingAxaml, IReadOnlyList<string> DynamicVisualTree, IReadOnlyList<string> HardcodedVisualSurface, IReadOnlyList<string> UnapprovedControls, IReadOnlyList<string> HandwrittenFluentReplacements);

	private static readonly string[] RequiredAxamlFiles = new string[27]
	{
		"Views/MainWindow.axaml", "Pages/Home/ZzzHomePage.axaml", "Pages/GameAssistant/ZzzGameAssistantPage.axaml", "Pages/GameAssistant/ZzzBattleAssistantPage.axaml", "Pages/GameAssistant/ZzzCommissionAssistantPage.axaml", "Pages/OneDragon/ZzzOneDragonPage.axaml", "Pages/OneDragon/ZzzOneDragonRunPage.axaml", "Pages/OneDragon/ZzzNotifySettingsPage.axaml", "Pages/OneDragon/ZzzChargePlanPage.axaml", "Pages/OneDragon/ZzzPredefinedTeamPage.axaml",
		"Pages/OneDragon/ZzzMouseSensitivityCheckerPage.axaml", "Pages/Standalone/ZzzStandaloneAppRunPage.axaml", "Pages/Accounts/ZzzAccountsPage.axaml", "Pages/Settings/ZzzSettingsPage.axaml", "Pages/Settings/ZzzGameSettingsPage.axaml", "Pages/Settings/ZzzOverlaySettingsPage.axaml", "Pages/Settings/ZzzResourceDownloadPage.axaml", "Pages/Settings/ZzzEnvironmentSettingsPage.axaml", "Pages/Settings/ZzzPushSettingsPage.axaml", "Pages/Settings/ZzzCustomSettingsPage.axaml",
		"Pages/Devtools/ZzzDevtoolsPage.axaml", "Pages/Devtools/ZzzImageAnalysisPage.axaml", "Pages/Devtools/ZzzTemplateHelperPage.axaml", "Pages/Devtools/ZzzScreenManagePage.axaml", "Pages/Devtools/ZzzAgentTemplateGeneratorPage.axaml", "Pages/Devtools/ZzzScreenshotHelperPage.axaml", "Pages/Devtools/ZzzOperationDebugAxamlPage.axaml"
	};

	/// <summary>
	/// 审计器应确认纳入范围页面均已迁移，且不存在动态视觉树、硬编码表面和未批准控件。
	/// </summary>
	[Fact]
	public void StaticAuditDetectsCurrentMigrationDebt()
	{
		GuiAuditResult guiAuditResult = Scan();
		Assert.Empty(guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Views/MainWindow.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Home/ZzzHomePage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/GameAssistant/ZzzGameAssistantPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/GameAssistant/ZzzBattleAssistantPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/GameAssistant/ZzzCommissionAssistantPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/OneDragon/ZzzOneDragonPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/OneDragon/ZzzOneDragonRunPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/OneDragon/ZzzNotifySettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/OneDragon/ZzzChargePlanPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/OneDragon/ZzzPredefinedTeamPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/OneDragon/ZzzMouseSensitivityCheckerPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Standalone/ZzzStandaloneAppRunPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Accounts/ZzzAccountsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzSettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzGameSettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzOverlaySettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzResourceDownloadPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzEnvironmentSettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzPushSettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Settings/ZzzCustomSettingsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzDevtoolsPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzScreenshotHelperPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzOperationDebugAxamlPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzImageAnalysisPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzTemplateHelperPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzScreenManagePage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Pages/Devtools/ZzzAgentTemplateGeneratorPage.axaml", (IEnumerable<string>)guiAuditResult.MissingAxaml);
		Assert.DoesNotContain("Views/MainWindow.cs", (IEnumerable<string>)guiAuditResult.DynamicVisualTree);
		Assert.DoesNotContain("Pages/Home/ZzzHomePage.cs", (IEnumerable<string>)guiAuditResult.DynamicVisualTree);
		Assert.Empty(guiAuditResult.DynamicVisualTree);
		Assert.Empty(guiAuditResult.HardcodedVisualSurface);
		Assert.Empty(guiAuditResult.UnapprovedControls);
	}

	[Fact]
	public void MultiShellWindowsUseOfficialFluentControlsWithoutExplanatoryCopy()
	{
		string path = FindGuiRoot();
		foreach (string fileName in new[] { "MixedShellWindow.axaml", "FrontierShellWindow.axaml" })
		{
			string text = File.ReadAllText(Path.Combine(path, "Views", fileName));
			Assert.Contains("<fa:NavigationView", text, StringComparison.Ordinal);
			Assert.Contains("<fa:NavigationViewItem", text, StringComparison.Ordinal);
			Assert.Contains("<fa:Frame", text, StringComparison.Ordinal);
			Assert.Contains("<fa:FontIconSource", text, StringComparison.Ordinal);
			Assert.DoesNotContain("对应 Python", text, StringComparison.Ordinal);
			Assert.DoesNotContain("后端尚未", text, StringComparison.Ordinal);
			Assert.DoesNotContain("fallback", text, StringComparison.OrdinalIgnoreCase);
		}
		string mixedResources = File.ReadAllText(Path.Combine(path, "Theme", "MixedShellResources.axaml"));
		string frontierResources = File.ReadAllText(Path.Combine(path, "Theme", "FrontierShellResources.axaml"));
		string mixed = File.ReadAllText(Path.Combine(path, "Views", "MixedShellWindow.axaml"));
		string frontier = File.ReadAllText(Path.Combine(path, "Views", "FrontierShellWindow.axaml"));
		Assert.Contains("ZzzMixedContentSurfaceBrush", mixedResources, StringComparison.Ordinal);
		Assert.Contains("ZzzMixedContentBorderBrush", mixedResources, StringComparison.Ordinal);
		Assert.Contains("ZzzMixedContentPadding", mixed, StringComparison.Ordinal);
		Assert.Contains("ZzzFrontierContentSurfaceBrush", frontierResources, StringComparison.Ordinal);
		Assert.Contains("ZzzFrontierContentBorderBrush", frontierResources, StringComparison.Ordinal);
		Assert.Contains("ZzzFrontierContentPadding", frontier, StringComparison.Ordinal);
	}

	[Fact]
	public void ShellResourcesDoNotContainExplanatoryProductCopy()
	{
		string path = FindGuiRoot();
		string content = string.Join(
			Environment.NewLine,
			Directory.EnumerateFiles(Path.Combine(path, "Views"), "*ShellWindow.axaml")
				.Concat(Directory.EnumerateFiles(Path.Combine(path, "Theme"), "*ShellResources.axaml"))
				.Select(File.ReadAllText));
		foreach (string forbidden in new[] { "对应 Python", "后端尚未", "fallback", "本页面", "此面板", "来源说明", "实现说明", "数据读取自" })
		{
			Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// 游戏助手容器使用 AXAML FluentAvalonia TabView，并固定 BaselineParity 子页顺序。
	/// </summary>
	[Fact]
	public void GameAssistantContainerUsesAxamlFluentPivot()
	{
		string path = FindGuiRoot();
		string text = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPage.cs"));
		string text2 = File.ReadAllText(Path.Combine(path, "Pages", "ZzzPageFactory.cs"));
		Assert.Contains("<fa:TabView", text, StringComparison.Ordinal);
		Assert.Contains("<fa:TabViewItem", text, StringComparison.Ordinal);
		Assert.Contains("Header=\"战斗助手\"", text, StringComparison.Ordinal);
		Assert.Contains("Header=\"委托助手\"", text, StringComparison.Ordinal);
		Assert.True(text.IndexOf("Header=\"战斗助手\"", StringComparison.Ordinal) < text.IndexOf("Header=\"委托助手\"", StringComparison.Ordinal));
		Assert.Contains("x:Name=\"BattleFrame\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"CommissionFrame\"", text, StringComparison.Ordinal);
		Assert.Contains("IsAddTabButtonVisible=\"False\"", text, StringComparison.Ordinal);
		Assert.Contains("CanDragTabs=\"False\"", text, StringComparison.Ordinal);
		Assert.Contains("CanReorderTabs=\"False\"", text, StringComparison.Ordinal);
		Assert.Contains("IZzzPivotNavigationHost", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzGameAssistantPage(_backend, _runIntent)", text2, StringComparison.Ordinal);
		int num = text2.IndexOf("CreateGameAssistantPage", StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzPivotPage", text2.Substring(num, text2.IndexOf("CreateOneDragonPage", StringComparison.Ordinal) - num), StringComparison.Ordinal);
	}

	/// <summary>
	/// 一条龙容器使用 AXAML FluentAvalonia TabView，并固定 BaselineParity 子页顺序。
	/// </summary>
	[Fact]
	public void OneDragonContainerUsesAxamlFluentPivot()
	{
		string path = FindGuiRoot();
		string text = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonPage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonPage.cs"));
		string text2 = File.ReadAllText(Path.Combine(path, "Pages", "ZzzPageFactory.cs"));
		Assert.Contains("<fa:TabView", text, StringComparison.Ordinal);
		Assert.Contains("Header=\"一条龙运行\"", text, StringComparison.Ordinal);
		Assert.Contains("Header=\"体力计划\"", text, StringComparison.Ordinal);
		Assert.Contains("Header=\"预备编队\"", text, StringComparison.Ordinal);
		Assert.Contains("Header=\"灵敏度校准\"", text, StringComparison.Ordinal);
		Assert.True(text.IndexOf("Header=\"一条龙运行\"", StringComparison.Ordinal) < text.IndexOf("Header=\"体力计划\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Header=\"体力计划\"", StringComparison.Ordinal) < text.IndexOf("Header=\"预备编队\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("Header=\"预备编队\"", StringComparison.Ordinal) < text.IndexOf("Header=\"灵敏度校准\"", StringComparison.Ordinal));
		Assert.Contains("x:Name=\"RunFrame\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"ChargePlanFrame\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"PredefinedTeamFrame\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"SensitivityFrame\"", text, StringComparison.Ordinal);
		Assert.Contains("IsAddTabButtonVisible=\"False\"", text, StringComparison.Ordinal);
		Assert.Contains("CanDragTabs=\"False\"", text, StringComparison.Ordinal);
		Assert.Contains("CanReorderTabs=\"False\"", text, StringComparison.Ordinal);
		Assert.Contains("IZzzPivotNavigationHost", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzOneDragonPage(_backend, _runIntent, _operations)", text2, StringComparison.Ordinal);
		int num = text2.IndexOf("CreateOneDragonPage", StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzPivotPage", text2.Substring(num, text2.IndexOf("CreateStandalonePage", StringComparison.Ordinal) - num), StringComparison.Ordinal);
	}

	/// <summary>
	/// 一条龙运行页使用 AXAML 左右分栏，并把列表与运行设置放在 BaselineParity 对应区域。
	/// </summary>
	[Fact]
	public void OneDragonRunPageUsesAxamlPythonSplitLayout()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonRunPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonRunPage.cs"));
		Assert.Contains("ColumnDefinitions=\"*,10,*\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"AppList\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<ItemsControl.ItemTemplate>", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:SettingsExpanderItem", actualString, StringComparison.Ordinal);
		Assert.Contains("Glyph=\"{Binding StatusGlyph}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Description=\"{Binding LastRunText}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("ToolTip.Tip=\"应用设置\"", actualString, StringComparison.Ordinal);
		Assert.Contains("ToolTip.Tip=\"更多\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Header=\"通知设置\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Header=\"移到顶部\"", actualString, StringComparison.Ordinal);
		Assert.Contains("ToolTip.Tip=\"运行\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<ToggleSwitch IsChecked=\"{Binding Enabled}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"使用说明\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"应用通知\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"运行实例\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"结束后\"", actualString, StringComparison.Ordinal);
		Assert.Contains("DragDrop.AllowDrop=\"True\"", actualString, StringComparison.Ordinal);
		Assert.Contains("PointerMoved=\"OnAppPointerMoved\"", actualString, StringComparison.Ordinal);
		Assert.Contains("DragDrop.DragOver=\"OnAppDragOver\"", actualString, StringComparison.Ordinal);
		Assert.Contains("DragDrop.Drop=\"OnAppDrop\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"AppNotifyTip\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"AppLifecycleCombo\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"AppDetailCombo\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"RunHost\"", actualString, StringComparison.Ordinal);
		Assert.Contains("new ZzzRunPanel", actualString2, StringComparison.Ordinal);
		Assert.Contains("SecondaryPageRequested", actualString2, StringComparison.Ordinal);
		Assert.Contains("SubscribeEvents", actualString2, StringComparison.Ordinal);
		Assert.Contains("instance.activeChanged", actualString2, StringComparison.Ordinal);
		Assert.Contains("run.stateChanged", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzSettingCard", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 一条龙通知二级页使用 AXAML Fluent 设置项和两列应用通知模板。
	/// </summary>
	[Fact]
	public void OneDragonNotifySettingsUsesAxamlFluentControls()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzNotifySettingsPage.axaml"));
		Assert.Contains("Text=\"通用\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"合并模式失败节点立即通知\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"应用通知\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<UniformGrid Columns=\"2\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAComboBox", actualString, StringComparison.Ordinal);
		Assert.Contains("SelectedLifecycle", actualString, StringComparison.Ordinal);
		Assert.Contains("SelectedDetail", actualString, StringComparison.Ordinal);
	}

	/// <summary>
	/// 体力计划页使用 AXAML、Fluent 设置组件、ContentDialog 和真实可拖拽模板。
	/// </summary>
	[Fact]
	public void ChargePlanPageUsesAxamlFluentPlanEditor()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzChargePlanPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzChargePlanPage.cs"));
		Assert.Contains("Content=\"体力计划说明\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Description=\"合理安排每日体力消耗，支持自定义优先级和循环执行\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"点此查看指南\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:SettingsExpander Header=\"双倍活动\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:CommandBar", actualString, StringComparison.Ordinal);
		Assert.Contains("Label=\"撤销\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Label=\"删除已完成\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Label=\"删除所有\"", actualString, StringComparison.Ordinal);
		Assert.Contains("DragDrop.AllowDrop=\"True\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Key=\"AddPlanDialog\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Title=\"新增体力计划\"", actualString, StringComparison.Ordinal);
		Assert.Contains("PrimaryButtonText=\"确定\"", actualString, StringComparison.Ordinal);
		Assert.Contains("CloseButtonText=\"取消\"", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("DefaultPlan", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzSettingCard", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 预备编队和灵敏度页面使用独立 AXAML，并保留 BaselineParity 原文与运行面。
	/// </summary>
	[Fact]
	public void PredefinedTeamAndSensitivityUseAxamlFluentControls()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzPredefinedTeamPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzPredefinedTeamPage.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzMouseSensitivityCheckerPage.axaml"));
		string actualString4 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzMouseSensitivityCheckerPage.cs"));
		string actualString5 = File.ReadAllText(Path.Combine(path, "Pages", "OneDragon", "ZzzOneDragonPages.cs"));
		Assert.Contains("ColumnDefinitions=\"500,10,*\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"战斗配置\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAComboBox", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"预备编队识别\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"使用说明\"", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("代理人 1", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("代理人 2", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("代理人 3", actualString, StringComparison.Ordinal);
		Assert.Contains("AgentEnum.Values", actualString2, StringComparison.Ordinal);
		Assert.Contains("GetBattleAssistantConfigCatalog()", actualString2, StringComparison.Ordinal);
		Assert.Contains("ZzzApplicationIds.PredefinedTeamChecker", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzSettingCard", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
		Assert.Contains("Content=\"使用说明\"", actualString3, StringComparison.Ordinal);
		Assert.Contains("Description=\"点击「开始」后将自动校准鼠标/手柄的转向灵敏度，用于视角转动\"", actualString3, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"SensitivityRunHost\"", actualString3, StringComparison.Ordinal);
		Assert.Contains("ZzzApplicationIds.MouseSensitivityChecker", actualString4, StringComparison.Ordinal);
		Assert.DoesNotContain("one-dragon.com", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("class ZzzPredefinedTeamPage", actualString5, StringComparison.Ordinal);
		Assert.DoesNotContain("class ZzzMouseSensitivityCheckerPage", actualString5, StringComparison.Ordinal);
	}

	/// <summary>
	/// 战斗助手使用 AXAML BaselineParity 等价左右分栏，并保留真实空闲状态表结构。
	/// </summary>
	[Fact]
	public void BattleAssistantUsesAxamlPythonSplitLayout()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzBattleAssistantPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPages.cs"));
		Assert.Contains("Margin=\"11\"", actualString, StringComparison.Ordinal);
		Assert.Contains("ColumnDefinitions=\"*,12,Auto\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Width=\"350\"", actualString, StringComparison.Ordinal);
		Assert.Contains("MinWidth=\"350\"", actualString, StringComparison.Ordinal);
		Assert.Contains("MaxWidth=\"400\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"SettingsHost\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"RunHost\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"TaskDisplay\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"BattleStateDisplay\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"[触发器]\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"[条件集]\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"[持续时间]\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Watermark=\"输入状态关键词过滤...\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"触发秒数\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"状态值\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"TaskTriggerValue\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"TaskExpressionValue\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"TaskDurationValue\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzBattleAssistantStateRowModel\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding TriggerSecondsText}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding ValueText}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("GetBattleAssistantRuntime()", actualString2, StringComparison.Ordinal);
		Assert.Contains("TimeSpan.FromMilliseconds(100)", actualString2, StringComparison.Ordinal);
		Assert.Contains("SubscribeBattleAssistantOperationLoaded", actualString2, StringComparison.Ordinal);
		Assert.Contains("UnsubscribeBattleAssistantOperationLoaded", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("等待运行事件", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("Python TaskDisplay 对应区域", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ZzzSettingCard(\"任务状态\"", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 战斗助手帮助动作使用 AXAML Fluent CommandBar 和 ContentDialog。
	/// </summary>
	[Fact]
	public void BattleAssistantHelpUsesOfficialCommandBarAndContentDialog()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzBattleAssistantSettings.axaml"));
		string text = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPages.cs"));
		Assert.Contains("<fa:CommandBar", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:CommandBarButton Label=\"如何让AI打得更好？\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:CommandBarButton Label=\"查看指南\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:CommandBarButton Label=\"前往社区\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:ContentDialog", actualString, StringComparison.Ordinal);
		Assert.Contains("Title=\"使用说明\"", actualString, StringComparison.Ordinal);
		Assert.Contains("CloseButtonText=\"确认\"", actualString, StringComparison.Ordinal);
		Assert.Contains("为了让您的自动战斗体验更加顺畅", actualString, StringComparison.Ordinal);
		Assert.Contains("祝您游戏愉快！", actualString, StringComparison.Ordinal);
		Assert.Contains("https://one-dragon.com/zzz/zh/feat_game_assistant.html", text, StringComparison.Ordinal);
		Assert.Contains("https://pd.qq.com/g/onedrag00n", text, StringComparison.Ordinal);
		int num = text.IndexOf("ZzzBattleAssistantSettings", StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzMultiPushSettingCard", text.Substring(num, text.IndexOf("ZzzCommissionAssistantSettings", StringComparison.Ordinal) - num), StringComparison.Ordinal);
	}

	/// <summary>
	/// 战斗与闪避配置使用真实目录门面和官方删除命令，不向空目录追加当前值。
	/// </summary>
	[Fact]
	public void BattleAssistantCatalogUsesBackendAndOfficialDeleteCommands()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzBattleAssistantSettings.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPages.cs"));
		Assert.Contains("x:Name=\"DeleteAutoBattleConfigButton\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"DeleteDodgeConfigButton\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:CommandBarButton", actualString, StringComparison.Ordinal);
		Assert.Contains("GetBattleAssistantConfigCatalog()", actualString2, StringComparison.Ordinal);
		Assert.Contains("DeleteBattleAssistantConfig", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("values.Add(currentValue)", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new AutoBattleConfigProvider", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 战斗助手设置项由 AXAML 官方 Fluent 控件声明，并直接绑定真实 scope。
	/// </summary>
	[Fact]
	public void BattleAssistantSettingsUseAxamlFluentControlsAndRealScopes()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzBattleAssistantSettings.axaml"));
		string text = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPages.cs"));
		int num = text.IndexOf("internal sealed partial class ZzzBattleAssistantSettings", StringComparison.Ordinal);
		string actualString2 = text.Substring(num, text.IndexOf("internal sealed partial class ZzzCommissionAssistantSettings", StringComparison.Ordinal) - num);
		Assert.Contains("<fa:SettingsExpanderItem Content=\"终结技一好就放\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:SettingsExpanderItem Content=\"使用合并配置文件\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:SettingsExpanderItem Content=\"GPU运算\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:NumberBox x:Name=\"ScreenshotIntervalNumber\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Minimum=\"0.02\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Maximum=\"0.1\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAComboBox x:Name=\"ControlMethodCombo\"", actualString, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(\"battle-assistant\")", actualString2, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(\"model\")", actualString2, StringComparison.Ordinal);
		Assert.Contains("SaveConfigScope", actualString2, StringComparison.Ordinal);
		Assert.Contains("string.Equals(evidenceTab, \"闪避助手\"", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("BuildAutoBattleSettings", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzBackendConfigBinding", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzComboBoxSettingCard", actualString2, StringComparison.Ordinal);
	}

	/// <summary>
	/// 战斗助手模式切换由 AXAML TabView 和代码中的同一控件状态驱动。
	/// </summary>
	[Fact]
	public void BattleAssistantModeUsesTabViewBinding()
	{
		string path = FindGuiRoot();
		string axaml = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzBattleAssistantSettings.axaml"));
		string source = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPages.cs"));
		Assert.Contains("<fa:TabView x:Name=\"ModeTabs\"", axaml, StringComparison.Ordinal);
		Assert.Contains("FindControl<TabView>(\"ModeTabs\")", source, StringComparison.Ordinal);
		Assert.Contains("_modeTabs.SelectionChanged += OnModeSelectionChanged", source, StringComparison.Ordinal);
		Assert.Contains("_modeTabs.SelectedIndex != 1", source, StringComparison.Ordinal);
		Assert.DoesNotContain("AutoBattleModeButton", source, StringComparison.Ordinal);
		Assert.DoesNotContain("DodgeAssistantModeButton", source, StringComparison.Ordinal);
		Assert.DoesNotContain("AutoBattleModeContent", source, StringComparison.Ordinal);
		Assert.DoesNotContain("DodgeAssistantModeContent", source, StringComparison.Ordinal);
		Assert.DoesNotContain("AutoUltimateToggleLabel", source, StringComparison.Ordinal);
		Assert.DoesNotContain("MergedFileToggleLabel", source, StringComparison.Ordinal);
		Assert.DoesNotContain("GpuToggleLabel", source, StringComparison.Ordinal);
	}

	/// <summary>
	/// 委托助手使用 AXAML 两列设置区、真实配置 scope 和 BaselineParity 原文。
	/// </summary>
	[Fact]
	public void CommissionAssistantUsesAxamlTwoColumnLayoutAndRealConfig()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzCommissionAssistantPage.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzCommissionAssistantSettings.axaml"));
		string text = File.ReadAllText(Path.Combine(path, "Pages", "GameAssistant", "ZzzGameAssistantPages.cs"));
		string actualString3 = text.Substring(text.IndexOf("internal sealed partial class ZzzCommissionAssistantSettings", StringComparison.Ordinal));
		Assert.Contains("RowDefinitions=\"*,12,Auto\"", actualString, StringComparison.Ordinal);
		Assert.Contains("ColumnDefinitions=\"*,12,*\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("Content=\"使用说明\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("Content=\"点此查看指南\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("Content=\"对话点击间隔(秒)\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("Maximum=\"10\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("SmallChange=\"0.05\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("当画面检测不到任何内容时, 开启下一轮检测的等待时间", actualString2, StringComparison.Ordinal);
		Assert.Contains("Content=\"自动闪避开关\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("Content=\"自动战斗开关\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"DodgeSwitchKeyBox\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"AutoBattleSwitchKeyBox\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("GetConfigScope(", actualString3, StringComparison.Ordinal);
		Assert.Contains("\"commission-assistant\"", actualString3, StringComparison.Ordinal);
		Assert.Contains("GetBattleAssistantConfigCatalog()", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzBackendConfigBinding", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("ZzzVerticalScrollPage.CreateStack", actualString3, StringComparison.Ordinal);
	}

	/// <summary>
	/// 共享运行面和日志使用 AXAML，且不再接受伪造截图运行状态。
	/// </summary>
	[Fact]
	public void SharedRunSurfaceUsesAxamlAndProductionEventsOnly()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Controls", "ZzzRunPanel.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Controls", "ZzzRunPanel.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Controls", "ZzzLogDisplayCard.axaml"));
		string actualString4 = File.ReadAllText(Path.Combine(path, "Controls", "ZzzLogDisplayCard.cs"));
		Assert.Contains("x:Name=\"StateText\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"PrimaryButton\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"StopButton\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"LogHost\"", actualString, StringComparison.Ordinal);
		Assert.Contains("run.stateChanged", actualString2, StringComparison.Ordinal);
		Assert.Contains("run.progress", actualString2, StringComparison.Ordinal);
		Assert.Contains("log.appended", actualString4, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"OutputText\"", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("ApplyEvidenceRunState", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("ZZZOD_GUI_EVIDENCE_RUN_STATE", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("证据采集状态", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("run.stateChanged\" or \"run.progress", actualString4, StringComparison.Ordinal);
	}

	/// <summary>
	/// 共享兼容层不得继续以 Border 或普通按钮模拟 Fluent 控件。
	/// </summary>
	[Fact]
	public void SharedControlsUseOfficialFluentAvaloniaBases()
	{
		GuiAuditResult guiAuditResult = Scan();
		Assert.Empty(guiAuditResult.HandwrittenFluentReplacements);
	}

	/// <summary>
	/// 主窗口视觉树必须由 AXAML 声明，并使用 FluentAvalonia 导航和 Frame。
	/// </summary>
	[Fact]
	public void MainWindowUsesAxamlNavigationViewAndFrame()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.cs"));
		Assert.Contains("<fa:NavigationView", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:NavigationViewItem", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:NavigationView.MenuItemTemplate>", actualString, StringComparison.Ordinal);
		Assert.Contains("PaneDisplayMode=\"Left\"", actualString, StringComparison.Ordinal);
		Assert.Contains("OpenPaneLength=\"{DynamicResource ZzzNavigationPaneWidth}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Classes=\"zzz-navigation-item\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding Text}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Margin=\"-4,0,0,0\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<DataTemplate", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:Frame", actualString, StringComparison.Ordinal);
		Assert.Contains("HorizontalContentAlignment=\"Stretch\"", actualString, StringComparison.Ordinal);
		Assert.Contains("VerticalContentAlignment=\"Stretch\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"TitleBar\"", actualString, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"TitleBarIcon\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Background=\"Transparent\"", actualString, StringComparison.Ordinal);
		Assert.Contains("ShowActivated=\"True\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding WindowTitle}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"{Binding LauncherVersionText}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Content=\"{Binding CodeVersionText}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<TextBlock Text=\"问题反馈\"", actualString, StringComparison.Ordinal);
		Assert.Contains("OnMinimizeClicked", actualString, StringComparison.Ordinal);
		Assert.Contains("OnMaximizeClicked", actualString, StringComparison.Ordinal);
		Assert.Contains("OnCloseClicked", actualString, StringComparison.Ordinal);
		Assert.Contains("Path.Combine(runRoot, \"assets\", \"ui\", \"logo.ico\")", actualString2, StringComparison.Ordinal);
		Assert.Contains("new Bitmap(iconPath)", actualString2, StringComparison.Ordinal);
		Assert.Contains("IZzzShellWindowRuntime", actualString2, StringComparison.Ordinal);
		Assert.Contains("_windowRuntime.Attach(this, _toastBar)", actualString2, StringComparison.Ordinal);
		string actualString4 = File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellWindowRuntime.cs"));
		Assert.Contains("ZzzWindowBackdropService", actualString4, StringComparison.Ordinal);
		Assert.Contains("_overlayController.Start();", actualString4, StringComparison.Ordinal);
		Assert.Contains("_globalInputMonitor.InputPressed += OnGlobalInputPressed", actualString4, StringComparison.Ordinal);
		Assert.Contains("if (!IsActive)", actualString2, StringComparison.Ordinal);
		Assert.Contains("BeginMoveDrag(args)", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("PageHeader", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new NavigationView", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new NavigationViewItem", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new ContentControl", actualString2, StringComparison.Ordinal);
	}

	[Fact]
	public void ShellPageHostExposesSharedRouteLifecycleAndBackNavigationContract()
	{
		Assert.True(typeof(IZzzShellPageHost).IsAssignableFrom(typeof(ZzzShellPageHost)));
		string path = FindGuiRoot();
		string text = File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellPageHost.cs"));
		Assert.Contains("Dictionary<string, Control> _pageCache", text, StringComparison.Ordinal);
		Assert.Contains("ZzzPageLifecycleService", text, StringComparison.Ordinal);
		Assert.Contains("IZzzShellBackNavigationHost", text, StringComparison.Ordinal);
		Assert.Contains("NavigateToRequestedTarget", text, StringComparison.Ordinal);
	}

	[Fact]
	public void ClassicShellKeepsPythonPrimaryAndFooterNavigationOrder()
	{
		ZzzNavigationRegistry registry = new ZzzNavigationRegistry();
		Assert.Equal(
			new[] { "home", "game-assistant", "one-dragon", "standalone" },
			registry.Entries.Where(entry => entry.Placement == ZzzNavigationPlacement.Primary).Select(entry => entry.Key));
		Assert.Equal(
			new[] { "devtools", "accounts", "settings" },
			registry.Entries.Where(entry => entry.Placement == ZzzNavigationPlacement.Footer).Select(entry => entry.Key).Take(3));
		Assert.All(registry.Entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.IconGlyph)));
		Assert.All(registry.Entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.AccessibleName)));
	}

	[Fact]
	public void ClassicShellTitleBarUsesProjectInstanceVersionsIssueAndWindowControls()
	{
		string path = FindGuiRoot();
		string window = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.axaml"));
		string viewModel = File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellViewModel.cs"));
		Assert.Contains("Text=\"{Binding WindowTitle}\"", window, StringComparison.Ordinal);
		Assert.Contains("Content=\"{Binding LauncherVersionText}\"", window, StringComparison.Ordinal);
		Assert.Contains("Content=\"{Binding CodeVersionText}\"", window, StringComparison.Ordinal);
		Assert.Contains("Text=\"问题反馈\"", window, StringComparison.Ordinal);
		Assert.Contains("OnMinimizeClicked", window, StringComparison.Ordinal);
		Assert.Contains("OnMaximizeClicked", window, StringComparison.Ordinal);
		Assert.Contains("OnCloseClicked", window, StringComparison.Ordinal);
		Assert.Contains("GetCurrentInstance()", viewModel, StringComparison.Ordinal);
		Assert.Contains("_issueUrl", viewModel, StringComparison.Ordinal);
	}

	[Fact]
	public void ClassicHomeUsesRealMediaAnnouncementsAndProductActions()
	{
		string path = FindGuiRoot();
		string home = File.ReadAllText(Path.Combine(path, "Pages", "Home", "ZzzHomePage.axaml"));
		string homeCode = File.ReadAllText(Path.Combine(path, "Pages", "Home", "ZzzHomePage.cs"));
		Assert.Contains("x:Name=\"StartButton\"", home, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"HomeLinkButton\"", home, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"GithubLinkButton\"", home, StringComparison.Ordinal);
		Assert.Contains("GetDashboardMediaAsync", homeCode, StringComparison.Ordinal);
		Assert.Contains("ZzzNoticeCard", home, StringComparison.Ordinal);
	}

	[Fact]
	public void ShellResourcesStayIsolatedFromClassicWindow()
	{
		string path = FindGuiRoot();
		string classic = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.axaml"));
		Assert.Contains("Theme/ClassicShellResources.axaml", classic, StringComparison.Ordinal);
		Assert.DoesNotContain("Theme/MixedShellResources.axaml", classic, StringComparison.Ordinal);
		Assert.DoesNotContain("Theme/FrontierShellResources.axaml", classic, StringComparison.Ordinal);
	}

	/// <summary>
	/// 首页路由使用零边距和标题栏首页样式，离开首页恢复 BaselineParity 普通页面边距。
	/// </summary>
	[Fact]
	public void MainWindowSwitchesPythonEquivalentHomeModeState()
	{
		ZzzShellRouteVisualState zzzShellRouteVisualState = ZzzShellRouteVisualState.ForRoute("home");
		ZzzShellRouteVisualState zzzShellRouteVisualState2 = ZzzShellRouteVisualState.ForRoute("settings");
		Assert.True(zzzShellRouteVisualState.IsHomeMode);
		Assert.Equal(new Thickness(0.0), zzzShellRouteVisualState.ContentMargin);
		Assert.False(zzzShellRouteVisualState2.IsHomeMode);
		Assert.Equal(new Thickness(11.0, 32.0, 11.0, 0.0), zzzShellRouteVisualState2.ContentMargin);
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.cs"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Theme", "ZzzComponentStyles.axaml"));
		Assert.Contains("Classes=\"zzz-titlebar\"", actualString, StringComparison.Ordinal);
		Assert.Contains("_pageHost.RouteChanged += OnRouteChanged", actualString2, StringComparison.Ordinal);
		Assert.Contains("_titleBar.Classes.Set(\"home-mode\", state.IsHomeMode)", actualString2, StringComparison.Ordinal);
		Assert.Contains("Grid.zzz-titlebar.home-mode TextBlock.zzz-titlebar-title", actualString3, StringComparison.Ordinal);
		Assert.Contains("Grid.zzz-titlebar.home-mode Button.zzz-titlebar-action", actualString3, StringComparison.Ordinal);
		Assert.Contains("Grid.zzz-titlebar.home-mode Button.zzz-window-control", actualString3, StringComparison.Ordinal);
		Assert.Contains("<DropShadowDirectionEffect", actualString3, StringComparison.Ordinal);
	}

	/// <summary>
	/// 首页主体使用 AXAML、Fluent icon button、TeachingTip、ContentDialog 和 NoticeCard 复合模块。
	/// </summary>
	[Fact]
	public void HomePageUsesAxamlFluentComposition()
	{
		string path = FindGuiRoot();
		string text = File.ReadAllText(Path.Combine(path, "Pages", "Home", "ZzzHomePage.axaml"));
		string actualString = File.ReadAllText(Path.Combine(path, "Pages", "Home", "ZzzHomePage.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Theme", "ZzzComponentStyles.axaml"));
		string text2 = File.ReadAllText(Path.Combine(path, "Controls", "Home", "ZzzNoticeCard.axaml"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Controls", "Home", "ZzzNoticeCard.axaml.cs"));
		Assert.Contains("<home:ZzzNoticeCard", text, StringComparison.Ordinal);
		Assert.Contains("<fa:SymbolIcon Symbol=\"Home\"", text, StringComparison.Ordinal);
		Assert.Contains("<fa:FAPathIcon", text, StringComparison.Ordinal);
		Assert.Contains("<fa:SymbolIcon Symbol=\"Library\"", text, StringComparison.Ordinal);
		Assert.Contains("<fa:SymbolIcon Symbol=\"Message\"", text, StringComparison.Ordinal);
		Assert.True(text.IndexOf("x:Name=\"HomeLinkButton\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"GithubLinkButton\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("x:Name=\"GithubLinkButton\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"DocsLinkButton\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("x:Name=\"DocsLinkButton\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"ChannelLinkButton\"", StringComparison.Ordinal));
		Assert.Contains("<fa:TeachingTip", text, StringComparison.Ordinal);
		Assert.Contains("<fa:ContentDialog", text, StringComparison.Ordinal);
		Assert.Contains("Title=\"运行前检查\"", text, StringComparison.Ordinal);
		Assert.Contains("PrimaryButtonText=\"前往配置\"", text, StringComparison.Ordinal);
		Assert.Contains("CloseButtonText=\"仍然继续\"", text, StringComparison.Ordinal);
		Assert.Contains("Text=\"以下配置项未就绪，可能影响正常运行：\"", text, StringComparison.Ordinal);
		Assert.Contains("MinWidth=\"420\"", text, StringComparison.Ordinal);
		Assert.Contains("Classes=\"zzz-home-start\"", text, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"StartButtonIcon\"", text, StringComparison.Ordinal);
		Assert.Contains("Symbol=\"PlayFilled\"", text, StringComparison.Ordinal);
		Assert.Contains("Height=\"48\"", text, StringComparison.Ordinal);
		Assert.Contains("MinWidth=\"180\"", text, StringComparison.Ordinal);
		Assert.Contains("CornerRadius\" Value=\"24\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("ZzzHomeStartBackgroundBrush", actualString2, StringComparison.Ordinal);
		Assert.Contains("ZzzHomeStartForegroundBrush", actualString2, StringComparison.Ordinal);
		Assert.Contains("ButtonBackgroundPointerOver", actualString, StringComparison.Ordinal);
		Assert.Contains("ButtonForegroundPointerOver", actualString, StringComparison.Ordinal);
		Assert.Contains("Symbol.PlayFilled : Symbol.Settings", actualString, StringComparison.Ordinal);
		Assert.Contains("Width=\"589\"", text2, StringComparison.Ordinal);
		Assert.Contains("Width=\"225\"", text2, StringComparison.Ordinal);
		Assert.Contains("<fa:TabView", text2, StringComparison.Ordinal);
		Assert.Contains("SelectedIndex=\"2\"", text2, StringComparison.Ordinal);
		Assert.Contains("IsAddTabButtonVisible=\"False\"", text2, StringComparison.Ordinal);
		Assert.Equal(4, text2.Split("IsClosable=\"False\"").Length - 1);
		Assert.Contains("Text=\"{Binding Title}\"", text2, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding Date}\"", text2, StringComparison.Ordinal);
		Assert.Contains("SelectionChanged=\"OnPostSelectionChanged\"", text2, StringComparison.Ordinal);
		Assert.Contains("Title=\"公告加载失败\"", text2, StringComparison.Ordinal);
		Assert.Contains("_failureInfoBar.Message = FailureMessage", actualString3, StringComparison.Ordinal);
		Assert.Contains("_failureInfoBar.IsOpen = true", actualString3, StringComparison.Ordinal);
		Assert.DoesNotContain("new StackPanel", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new Border", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new Button", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new TextBlock", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("zzz-home-media-scrim", text, StringComparison.Ordinal);
		string actualString4 = text + Environment.NewLine + text2;
		string[] array = new string[12]
		{
			"readiness", "宿主模式", "业务上下文", "应用注册", "窗口就绪", "模型就绪", "PageModel", "链接来源", "实现说明", "对应 Python",
			"Python 页面", "来源说明"
		};
		foreach (string expectedSubstring in array)
		{
			Assert.DoesNotContain(expectedSubstring, actualString4, StringComparison.OrdinalIgnoreCase);
		}
	}

	/// <summary>
	/// Fluent Pivot 必须复用官方 TabView 主题，并让两层 Frame 拉伸实际页面。
	/// </summary>
	[Fact]
	public void FluentPivotUsesBaseTabViewThemeAndStretchFrames()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Controls", "ZzzPivotPage.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Controls", "ZzzPageStackHost.axaml"));
		string actualString3 = File.ReadAllText(Path.Combine(path, "Views", "MainWindow.axaml"));
		Assert.Contains("StyleKeyOverride => typeof(TabView)", actualString, StringComparison.Ordinal);
		Assert.Contains("((IList)TabItems).Add(tabItem)", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("_compatibilityFrame", actualString, StringComparison.Ordinal);
		Assert.Contains("HorizontalContentAlignment=\"Stretch\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("VerticalContentAlignment=\"Stretch\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("HorizontalContentAlignment=\"Stretch\"", actualString3, StringComparison.Ordinal);
		Assert.Contains("VerticalContentAlignment=\"Stretch\"", actualString3, StringComparison.Ordinal);
	}

	/// <summary>
	/// 二级页面栈必须使用 AXAML Fluent Frame，禁止代码绘制返回容器。
	/// </summary>
	[Fact]
	public void SecondaryPageStackUsesAxamlFluentFrame()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Controls", "ZzzPageStackHost.axaml"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Controls", "ZzzPageStackHost.axaml.cs"));
		Assert.Contains("<fa:Frame", actualString, StringComparison.Ordinal);
		Assert.Contains("HorizontalContentAlignment=\"Stretch\"", actualString, StringComparison.Ordinal);
		Assert.Contains("VerticalContentAlignment=\"Stretch\"", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("new Frame", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new Border", actualString2, StringComparison.Ordinal);
		Assert.DoesNotContain("new Button", actualString2, StringComparison.Ordinal);
	}

	private static GuiAuditResult Scan()
	{
		string guiRoot = FindGuiRoot();
		string path = Path.Combine(guiRoot, "Pages");
		List<string> missingAxaml = RequiredAxamlFiles.Where((string relativePath) => !File.Exists(Path.Combine(guiRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))).ToList();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		IEnumerable<string> enumerable = Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Append(Path.Combine(guiRoot, "Views", "MainWindow.cs"));
		foreach (string item2 in enumerable)
		{
			string text = File.ReadAllText(item2);
			string item = Normalize(Path.GetRelativePath(guiRoot, item2));
			if (ContainsAny(text, "new StackPanel", "new Border", "new Button", "new TextBox", "new ComboBox", "new NumericUpDown"))
			{
				list.Add(item);
			}
			if (ContainsAny(text, "Background = Brushes.", "CornerRadius =", "BorderBrush = Brushes."))
			{
				list2.Add(item);
			}
			if (ContainsAny(text, "new ComboBox", "new NumericUpDown", "ComboBox = new", "NumericUpDown = new"))
			{
				list3.Add(item);
			}
		}
		string path2 = Path.Combine(guiRoot, "Controls");
		List<string> list4 = new List<string>();
		foreach (string item3 in Directory.EnumerateFiles(path2, "*.cs", SearchOption.AllDirectories))
		{
			string text2 = File.ReadAllText(item3);
			if (ContainsAny(
				text2,
				"class ZzzSettingCard : Border",
				"class ZzzSettingsGroup",
				"class ZzzStatusPill",
				"class ZzzCommandBar",
				"class ZzzInfoBar",
				"Classes = { \"zzz-segmented\" }"))
			{
				list4.Add(Normalize(Path.GetRelativePath(guiRoot, item3)));
			}
		}
		return new GuiAuditResult(missingAxaml, list.Distinct<string>(StringComparer.Ordinal).ToArray(), list2.Distinct<string>(StringComparer.Ordinal).ToArray(), list3.Distinct<string>(StringComparer.Ordinal).ToArray(), list4.Distinct<string>(StringComparer.Ordinal).ToArray());
	}

	private static string FindGuiRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			string text = Path.Combine(directoryInfo.FullName, "zzzod-dotnet", "src", "ZzzOd.Gui");
			if (Directory.Exists(text))
			{
				return text;
			}
		}
		throw new DirectoryNotFoundException("未找到 ZzzOd.Gui 源码目录。");
	}

	private static bool ContainsAny(string text, params string[] values)
	{
		return values.Any((string value) => text.Contains(value, StringComparison.Ordinal));
	}

	private static string Normalize(string path)
	{
		return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
	}
}

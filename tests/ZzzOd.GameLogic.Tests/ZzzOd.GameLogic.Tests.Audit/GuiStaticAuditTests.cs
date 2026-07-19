using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
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
	public void FrontierShellUsesIndependentSampleMainViewWithoutExplanatoryCopy()
	{
		string path = FindGuiRoot();
		string window = File.ReadAllText(Path.Combine(path, "Views", "FrontierShellWindow.axaml"));
		string windowCode = File.ReadAllText(Path.Combine(path, "Views", "FrontierShellWindow.cs"));
		string mainView = File.ReadAllText(Path.Combine(path, "Views", "FrontierMainView.axaml"));
		string mainViewCode = File.ReadAllText(Path.Combine(path, "Views", "FrontierMainView.cs"));
		string navigationResources = File.ReadAllText(Path.Combine(path, "Theme", "FrontierNavigationResources.axaml"));
		string text = window + Environment.NewLine + mainView;
		Assert.Contains("x:Name=\"MainViewHost\"", window, StringComparison.Ordinal);
		Assert.Contains("<fa:FANavigationView", mainView, StringComparison.Ordinal);
		Assert.Contains("<fa:FAFrame", mainView, StringComparison.Ordinal);
		Assert.Contains("<fa:FAInfoBar", mainView, StringComparison.Ordinal);
		Assert.Contains("Grid.RowSpan=\"2\"", mainView, StringComparison.Ordinal);
		Assert.Contains("OpenPaneLength=\"108\"", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("frontier-selection-indicator", mainView, StringComparison.Ordinal);
		Assert.Contains("uip|FANavigationViewItemPresenter", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("<ControlTemplate>", mainView, StringComparison.Ordinal);
		Assert.Contains("/template/ Border#LayoutRoot", mainView, StringComparison.Ordinal);
		Assert.Contains("/template/ ContentPresenter#ContentPresenter", mainView, StringComparison.Ordinal);
		Assert.Contains("Setter Property=\"Width\" Value=\"72\"", mainView, StringComparison.Ordinal);
		Assert.Contains("NavigationViewSelectionIndicatorWidth", navigationResources, StringComparison.Ordinal);
		Assert.Contains("Thickness x:Key=\"NavigationViewContentMargin\">0,48,0,0", mainView, StringComparison.Ordinal);
		Assert.Contains("x:Name=\"PaneTitleSpacer\"", mainView, StringComparison.Ordinal);
		Assert.Contains("Height=\"40\"", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("ZIndex=\"10\"", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("Panel.ZIndex", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("FANavigationViewItemPresenter:selected /template/ Border#SelectionIndicator", mainView, StringComparison.Ordinal);
		Assert.Contains("FocusAdorner\" Value=\"{x:Null}\"", mainView, StringComparison.Ordinal);
		Assert.Contains("Height=\"48\"", mainView, StringComparison.Ordinal);
		Assert.Contains("Width=\"18\"", mainView, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding FrontierWindowTitle}\"", mainView, StringComparison.Ordinal);
		Assert.Contains("TitleBar.Height = 48", windowCode, StringComparison.Ordinal);
		Assert.Contains("TitleBar.ExtendsContentIntoTitleBar = true", windowCode, StringComparison.Ordinal);
		Assert.DoesNotContain("ExtendClientAreaToDecorationsHint", window, StringComparison.Ordinal);
		Assert.DoesNotContain("ExtendClientAreaTitleBarHeightHint", window, StringComparison.Ordinal);
		Assert.DoesNotContain("OnMinimizeClicked", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("OnMaximizeClicked", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("OnCloseClicked", mainView, StringComparison.Ordinal);
		Assert.Contains("ThicknessTransition Property=\"Margin\" Duration=\"0:0:0.25\"", mainView, StringComparison.Ordinal);
		Assert.Contains("animation.RunAsync(icon, cancellationToken)", mainViewCode, StringComparison.Ordinal);
		Assert.DoesNotContain("RunAsync(scale)", mainViewCode, StringComparison.Ordinal);
		Assert.DoesNotContain("SearchBox", mainView, StringComparison.Ordinal);
		Assert.DoesNotContain("对应 Python", text, StringComparison.Ordinal);
		Assert.DoesNotContain("后端尚未", text, StringComparison.Ordinal);
		Assert.DoesNotContain("fallback", text, StringComparison.OrdinalIgnoreCase);
		Assert.False(File.Exists(Path.Combine(path, "Views", "MixedShellWindow.axaml")));
		Assert.False(File.Exists(Path.Combine(path, "Theme", "MixedShellResources.axaml")));

		foreach (string fileName in new[] { "FrontierShellResources.axaml", "FrontierNavigationResources.axaml", "FrontierPageResources.axaml" })
		{
			Assert.True(File.Exists(Path.Combine(path, "Theme", fileName)));
		}

		string pageHost = File.ReadAllText(Path.Combine(path, "Views", "FrontierPageHost.axaml"));
		Assert.Contains("<ScrollViewer x:Name=\"PageScrollViewer\"", pageHost, StringComparison.Ordinal);
		Assert.Contains("Padding=\"{DynamicResource SampleAppPageMargin}\"", pageHost, StringComparison.Ordinal);
		Assert.Contains("<StackPanel x:Name=\"StandardContentStack\"", pageHost, StringComparison.Ordinal);
		Assert.Contains("Spacing=\"{DynamicResource SampleAppSectionSpacing}\"", pageHost, StringComparison.Ordinal);
		Assert.Contains("<ContentControl x:Name=\"StandardContent\"", pageHost, StringComparison.Ordinal);
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

	[Fact]
	public void Avalonia12TopLevelsKeepOwnerTransparencyAndDialogOwnerBoundaries()
	{
		string path = FindGuiRoot();
		string overlay = File.ReadAllText(Path.Combine(path, "Overlay", "ZzzOverlayTechnicalWindow.cs"))
			+ File.ReadAllText(Path.Combine(path, "Overlay", "ZzzOverlayInfoPanelWindow.cs"))
			+ File.ReadAllText(Path.Combine(path, "Overlay", "ZzzOverlayNativeWindow.cs"))
			+ File.ReadAllText(Path.Combine(path, "Overlay", "ZzzOverlayController.cs"))
			+ File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellWindowRuntime.cs"));
		Assert.Contains("TransparencyLevelHint", overlay, StringComparison.Ordinal);
		Assert.Contains("WindowDecorations.None", overlay, StringComparison.Ordinal);
		Assert.Contains("AttachOwner", overlay, StringComparison.Ordinal);
		Assert.Contains("Show(_ownerWindow)", overlay, StringComparison.Ordinal);
		Assert.Contains("SetWindowDisplayAffinity", overlay, StringComparison.Ordinal);

		string[] guiSources = Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).ToArray();
		foreach (string sourcePath in guiSources)
		{
			string source = File.ReadAllText(sourcePath);
			Assert.DoesNotContain("ShowAsync()", source, StringComparison.Ordinal);
		}

		string editor = File.ReadAllText(Path.Combine(path, "Pages", "ApplicationSettings", "ZzzWorldPatrolLargeMapIconEditorWindow.axaml.cs"));
		Assert.Contains("ShowAsync(this)", editor, StringComparison.Ordinal);
		string dialog = File.ReadAllText(Path.Combine(path, "Services", "Dialogs", "ZzzDialogService.cs"));
		string shellRuntime = File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellWindowRuntime.cs"));
		Assert.Contains("FATeachingTip", dialog, StringComparison.Ordinal);
		Assert.Contains("FAContentDialog", dialog, StringComparison.Ordinal);
		Assert.Contains("FAInfoBar", shellRuntime, StringComparison.Ordinal);
		Assert.Contains("ShowToast", shellRuntime, StringComparison.Ordinal);
	}

	[Fact]
	public void FrontierPageFactoryUsesDedicatedRouteViewsAndSampleContainers()
	{
		string path = FindGuiRoot();
		string factory = File.ReadAllText(Path.Combine(path, "Shell", "ZzzFrontierPageFactory.cs"));
		string views = File.ReadAllText(Path.Combine(path, "Views", "ZzzFrontierPageViews.cs"));
		string host = File.ReadAllText(Path.Combine(path, "Views", "FrontierPageHost.axaml"));
		foreach (string viewType in new[]
		{
			"FrontierHomePage", "FrontierGameAssistantPage", "FrontierOneDragonPage",
			"FrontierStandalonePage", "FrontierDevtoolsPage", "FrontierAccountsPage",
			"FrontierSettingsPage",
		})
		{
			Assert.Contains(viewType, factory + views, StringComparison.Ordinal);
		}

		Assert.Contains("ScrollViewer x:Name=\"PageScrollViewer\"", host, StringComparison.Ordinal);
		Assert.Contains("ContentControl x:Name=\"StandardContent\"", host, StringComparison.Ordinal);
		Assert.Contains("FASettingsExpander", host, StringComparison.Ordinal);
		Assert.Contains("FATabView", host, StringComparison.Ordinal);
		Assert.Contains("FACommandBar", host, StringComparison.Ordinal);
		Assert.Contains("FAInfoBar", host, StringComparison.Ordinal);
		Assert.DoesNotContain("BackgroundVideoHost", factory + views + host, StringComparison.Ordinal);
		Assert.DoesNotContain("LibVLC", factory + views + host, StringComparison.Ordinal);

		string worldPatrol = File.ReadAllText(Path.Combine(path, "Pages", "ApplicationSettings", "ZzzWorldPatrolAppSettingPage.axaml"));
		foreach (string action in new[] { "保存", "回退", "合并", "移除", "移动", "应用", "运行" })
		{
			Assert.Contains(action, worldPatrol, StringComparison.Ordinal);
		}
	}

	[Fact]
	public void FrontierDedicatedPagesUseIndependentSampleVisualTrees()
	{
		string path = FindGuiRoot();
		string account = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "Accounts", "ZzzFrontierAccountsPage.axaml"));
		string oneDragon = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "OneDragon", "FrontierOneDragonPage.axaml"));
		string oneDragonChildren = string.Join(Environment.NewLine,
			Directory.EnumerateFiles(Path.Combine(path, "Views", "FrontierPages", "OneDragon"), "*.axaml")
				.Select(File.ReadAllText));
		string devtools = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "DevTools", "FrontierDevtoolsPage.axaml"));
		string worldPatrol = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "WorldPatrol", "FrontierWorldPatrolPage.axaml"));
		string home = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "Home", "FrontierHomePage.axaml"));
		string gameAssistant = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "GameAssistant", "FrontierGameAssistantPage.axaml"));
		string gameAssistantPages = string.Join(Environment.NewLine,
			Directory.EnumerateFiles(Path.Combine(path, "Views", "FrontierPages", "GameAssistant"), "*.axaml")
				.Select(File.ReadAllText));
		string standalone = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "Standalone", "FrontierStandalonePage.axaml"));
		string standaloneRun = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "Standalone", "FrontierStandaloneAppRunPage.axaml"));
		string settings = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "Settings", "FrontierSettingsPage.axaml"));
		string homeCode = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "Home", "FrontierHomePage.axaml.cs"));
		string factory = File.ReadAllText(Path.Combine(path, "Shell", "ZzzFrontierPageFactory.cs"));

		Assert.Contains("ScrollViewer Padding=\"{DynamicResource SampleAppPageMargin}\"", account, StringComparison.Ordinal);
		Assert.Contains("StackPanel Spacing=\"{DynamicResource SampleAppSectionSpacing}\"", account, StringComparison.Ordinal);
		Assert.Contains("FASettingsExpander", account, StringComparison.Ordinal);
		Assert.Contains("FASettingsExpanderItem", account, StringComparison.Ordinal);
		Assert.Contains("FASettingsExpander.Footer", account, StringComparison.Ordinal);
		Assert.Contains("HeaderGrid", account, StringComparison.Ordinal);
		Assert.Contains("AddInstanceButton", account, StringComparison.Ordinal);
		Assert.Contains("StartButton", home, StringComparison.Ordinal);
		Assert.Contains("NoticeCard", home, StringComparison.Ordinal);
		Assert.Contains("HomeLinkButton", home, StringComparison.Ordinal);
		Assert.Contains("GetDashboardMediaAsync", homeCode, StringComparison.Ordinal);
		Assert.Contains("ApplyThemeColor", homeCode, StringComparison.Ordinal);
		Assert.Contains("LoadVideoRepresentativeFrame", homeCode, StringComparison.Ordinal);
		Assert.DoesNotContain("_videoTimer", homeCode, StringComparison.Ordinal);
		Assert.DoesNotContain("RenderNextVideoFrame", homeCode, StringComparison.Ordinal);
		Assert.DoesNotContain("BackgroundVideoHost", home + homeCode, StringComparison.Ordinal);
		Assert.Contains("FATabView", oneDragon, StringComparison.Ordinal);
		Assert.Contains("FAFrame", oneDragonChildren, StringComparison.Ordinal);
		Assert.Contains("ScrollViewer", oneDragonChildren, StringComparison.Ordinal);
		Assert.Contains("FACommandBar", oneDragonChildren, StringComparison.Ordinal);
		Assert.Contains("FATabView", devtools, StringComparison.Ordinal);
		Assert.Contains("FAFrame", devtools, StringComparison.Ordinal);
		Assert.Contains("FACommandBar", string.Join(Environment.NewLine,
			Directory.EnumerateFiles(Path.Combine(path, "Views", "FrontierPages", "DevTools"), "*.axaml")
				.Select(File.ReadAllText)), StringComparison.Ordinal);
		Assert.Contains("FATabView", worldPatrol, StringComparison.Ordinal);
		Assert.Contains("FASettingsExpander", worldPatrol, StringComparison.Ordinal);
		Assert.Contains("FACommandBar", worldPatrol, StringComparison.Ordinal);
		Assert.Contains("BattleFrame", gameAssistant, StringComparison.Ordinal);
		Assert.Contains("CommissionFrame", gameAssistant, StringComparison.Ordinal);
		Assert.Contains("FrontierBattleAssistantPage", gameAssistantPages, StringComparison.Ordinal);
		Assert.Contains("FrontierCommissionAssistantPage", gameAssistantPages, StringComparison.Ordinal);
		Assert.Contains("ContentHost", standalone, StringComparison.Ordinal);
		Assert.Contains("AppList", standaloneRun, StringComparison.Ordinal);
		Assert.Contains("RunHost", standaloneRun, StringComparison.Ordinal);
		Assert.Contains("GameFrame", settings, StringComparison.Ordinal);
		Assert.Contains("OverlayFrame", settings, StringComparison.Ordinal);
		Assert.Contains("ResourceDownloadFrame", settings, StringComparison.Ordinal);
		Assert.Contains("EnvironmentFrame", settings, StringComparison.Ordinal);
		Assert.Contains("PushFrame", settings, StringComparison.Ordinal);
		Assert.Contains("CustomFrame", settings, StringComparison.Ordinal);
		Assert.Contains("FrontierHomeVisual", factory, StringComparison.Ordinal);
		Assert.Contains("FrontierGameAssistantVisual", factory, StringComparison.Ordinal);
		Assert.Contains("FrontierStandaloneVisual", factory, StringComparison.Ordinal);
		Assert.Contains("FrontierSettingsVisual", factory, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,*\"", standalone, StringComparison.Ordinal);
		Assert.Contains("ZzzFrontierAccountsPage", factory, StringComparison.Ordinal);
		Assert.Contains("FrontierOneDragonVisual", factory, StringComparison.Ordinal);
		Assert.Contains("FrontierDevtoolsVisual", factory, StringComparison.Ordinal);
	}

	[Fact]
	public void FrontierCollectionTemplatesUseTheirRuntimeModelNamespaces()
	{
		string root = Path.Combine(FindGuiRoot(), "Views", "FrontierPages");
		string standalone = File.ReadAllText(Path.Combine(root, "Standalone", "FrontierStandaloneAppRunPage.axaml"));
		string resourceDownload = File.ReadAllText(Path.Combine(root, "Settings", "FrontierResourceDownloadPage.axaml"));
		string environment = File.ReadAllText(Path.Combine(root, "Settings", "FrontierEnvironmentSettingsPage.axaml"));
		string push = File.ReadAllText(Path.Combine(root, "Settings", "FrontierPushSettingsPage.axaml"));
		string notoriousHunt = File.ReadAllText(Path.Combine(root, "ApplicationSettings", "FrontierNotoriousHuntAppSettingPage.axaml"));
		string redemptionCode = File.ReadAllText(Path.Combine(root, "ApplicationSettings", "FrontierRedemptionCodeAppSettingPage.axaml"));
		string shiyuDefense = File.ReadAllText(Path.Combine(root, "ApplicationSettings", "FrontierShiyuDefenseAppSettingPage.axaml"));

		Assert.Contains("xmlns:local=\"using:ZzzOd.Gui.Views.FrontierPages.Standalone\"", standalone, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzStandaloneAppRowModel\"", standalone, StringComparison.Ordinal);
		Assert.Contains("xmlns:local=\"using:ZzzOd.Gui.Views.FrontierPages.Settings\"", resourceDownload, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzResourceModelOption\"", resourceDownload, StringComparison.Ordinal);
		Assert.Contains("xmlns:local=\"using:ZzzOd.Gui.Views.FrontierPages.Settings\"", environment, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzEnvironmentOption\"", environment, StringComparison.Ordinal);
		Assert.Contains("xmlns:settings=\"using:ZzzOd.Gui.Views.FrontierPages.Settings\"", push, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"settings:ZzzPushFieldModel\"", push, StringComparison.Ordinal);
		Assert.Contains("xmlns:local=\"using:ZzzOd.Gui.Views.FrontierPages.ApplicationSettings\"", notoriousHunt, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzNotoriousHuntPlanRowModel\"", notoriousHunt, StringComparison.Ordinal);
		Assert.Contains("xmlns:local=\"using:ZzzOd.Gui.Views.FrontierPages.ApplicationSettings\"", redemptionCode, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzRedemptionCodeRowModel\"", redemptionCode, StringComparison.Ordinal);
		Assert.Contains("xmlns:local=\"using:ZzzOd.Gui.Views.FrontierPages.ApplicationSettings\"", shiyuDefense, StringComparison.Ordinal);
		Assert.Contains("x:DataType=\"local:ZzzShiyuDefenseTeamRowModel\"", shiyuDefense, StringComparison.Ordinal);

		foreach (string content in new[] { standalone, resourceDownload, environment, push, notoriousHunt, redemptionCode, shiyuDefense })
		{
			Assert.DoesNotContain("using:ZzzOd.Gui.Pages.Standalone", content, StringComparison.Ordinal);
			Assert.DoesNotContain("using:ZzzOd.Gui.Pages.Settings", content, StringComparison.Ordinal);
			Assert.DoesNotContain("using:ZzzOd.Gui.Pages.ApplicationSettings", content, StringComparison.Ordinal);
		}
	}

	[Fact]
	public void FrontierApplicationSettingsFactoryMapsEveryRegisteredProvider()
	{
		string path = FindGuiRoot();
		string factory = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "ApplicationSettings", "FrontierAppSettingPageFactory.cs"));
		string[] targets =
		[
			"world-patrol-settings", "withered-domain-settings", "one-dragon-charge-plan",
			"drive-disc-dismantle-flyout", "redemption-code-settings", "lost-void-settings",
			"suibian-temple-settings", "coffee-settings", "notorious-hunt-settings",
			"random-play-flyout", "life-on-line-flyout", "intel-board-flyout", "shiyu-defense-settings",
		];
		foreach (string target in targets)
		{
			Assert.Contains($"\"{target}\"", factory, StringComparison.Ordinal);
		}

		foreach (string pageType in new[]
		{
			"FrontierWorldPatrolPage", "FrontierWitheredDomainAppSettingPage", "FrontierChargePlanPage",
			"FrontierDriveDiscDismantleSettingsFlyoutContent", "FrontierRedemptionCodeAppSettingPage",
			"FrontierLostVoidAppSettingPage", "FrontierSuibianTempleAppSettingPage", "FrontierCoffeeAppSettingPage",
			"FrontierNotoriousHuntAppSettingPage", "FrontierRandomPlaySettingsFlyoutContent",
			"FrontierLifeOnLineSettingsFlyoutContent", "FrontierIntelBoardSettingsFlyoutContent",
			"FrontierShiyuDefenseAppSettingPage",
		})
		{
			Assert.Contains(pageType, factory, StringComparison.Ordinal);
		}
	}

	[Fact]
	public void FrontierPagesRetainBaselineNamesAndEventHandlers()
	{
		string guiRoot = FindGuiRoot();
		string frontierRoot = Path.Combine(guiRoot, "Views", "FrontierPages");
		string classicRoot = Path.Combine(guiRoot, "Pages");
		HashSet<string> eventAttributes = new(StringComparer.Ordinal)
		{
			"Click", "SelectionChanged", "LostFocus", "ValueChanged", "KeyDown", "PointerPressed",
			"PointerReleased", "PointerMoved", "DragOver", "Drop", "TextChanged", "Checked", "Unchecked",
		};
		HashSet<string> intentionallyMovedNames = new(StringComparer.Ordinal)
		{
			"BackgroundActionList", "KeyboardKeyList", "GamepadKeyList",
		};

		foreach (string frontierPath in Directory.EnumerateFiles(frontierRoot, "*.axaml", SearchOption.AllDirectories))
		{
			string relative = Path.GetRelativePath(frontierRoot, frontierPath);
			string directory = Path.GetDirectoryName(relative) ?? string.Empty;
			if (string.Equals(directory, "DevTools", StringComparison.Ordinal))
			{
				directory = "Devtools";
			}

			string name = Path.GetFileNameWithoutExtension(relative);
			string classicName = name.Replace("Frontier", "Zzz", StringComparison.Ordinal);
			if (string.Equals(name, "FrontierWorldPatrolPage", StringComparison.Ordinal))
			{
				classicName = "ZzzWorldPatrolAppSettingPage";
			}

			string classicPath = Path.Combine(classicRoot, directory, classicName + ".axaml");
			if (!File.Exists(classicPath))
			{
				continue;
			}

			XDocument frontier = XDocument.Load(frontierPath);
			XDocument classic = XDocument.Load(classicPath);
			HashSet<string> frontierNames = frontier.Descendants()
				.SelectMany(element => element.Attributes())
				.Where(attribute => attribute.Name.LocalName == "Name")
				.Select(attribute => attribute.Value)
				.ToHashSet(StringComparer.Ordinal);
			foreach (string expectedName in classic.Descendants()
				.SelectMany(element => element.Attributes())
				.Where(attribute => attribute.Name.LocalName == "Name")
				.Select(attribute => attribute.Value)
				.Distinct(StringComparer.Ordinal)
				.Where(expected => !intentionallyMovedNames.Contains(expected)))
			{
				Assert.Contains(expectedName, frontierNames);
			}

			HashSet<string> frontierHandlers = frontier.Descendants()
				.SelectMany(element => element.Attributes())
				.Where(attribute => eventAttributes.Contains(attribute.Name.LocalName))
				.Select(attribute => attribute.Value)
				.ToHashSet(StringComparer.Ordinal);
			foreach (string expectedHandler in classic.Descendants()
				.SelectMany(element => element.Attributes())
				.Where(attribute => eventAttributes.Contains(attribute.Name.LocalName))
				.Select(attribute => attribute.Value)
				.Distinct(StringComparer.Ordinal))
			{
				Assert.Contains(expectedHandler, frontierHandlers);
			}
		}
	}

	[Fact]
	public void FrontierSettingsExpanderItemsUseSampleParentRelationship()
	{
		string root = Path.Combine(FindGuiRoot(), "Views", "FrontierPages");
		foreach (string file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
		{
			XDocument document = XDocument.Load(file, LoadOptions.SetLineInfo);
			XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
			foreach (XElement expander in document.Descendants().Where(element => element.Name.LocalName == "FASettingsExpander"))
			{
				Assert.Null(expander.Attribute("Content"));
				Assert.DoesNotContain(expander.Ancestors(), ancestor => ancestor.Name.LocalName == "FASettingsExpander");
			}

			foreach (XElement item in document.Descendants().Where(element => element.Name.LocalName == "FASettingsExpanderItem"))
			{
				bool directItem = item.Parent?.Name.LocalName == "FASettingsExpander";
				bool itemTemplate = item.Parent?.Name.LocalName == "DataTemplate"
					&& item.Parent.Parent?.Name.LocalName == "FASettingsExpander.ItemTemplate";
				string? resourceKey = item.Parent?.Attribute(x + "Key")?.Value;
				bool resourceTemplate = !string.IsNullOrWhiteSpace(resourceKey)
					&& document.Descendants()
						.Where(element => element.Name.LocalName == "FASettingsExpander")
						.Any(element => string.Equals(
							element.Attribute("ItemTemplate")?.Value,
							$"{{StaticResource {resourceKey}}}",
							StringComparison.Ordinal));
				Assert.True(
					directItem || itemTemplate || resourceTemplate,
					$"{Path.GetRelativePath(root, file)} 的 SettingsExpanderItem 必须直接放在 SettingsExpander 或其 ItemTemplate 下。行 {((IXmlLineInfo)item).LineNumber}");
			}
		}
	}

	[Fact]
	public void FrontierLargeEditorsKeepFixedActionsAndOneContentScrollBoundary()
	{
		string root = Path.Combine(FindGuiRoot(), "Views", "FrontierPages");
		string imageAnalysis = File.ReadAllText(Path.Combine(root, "DevTools", "FrontierImageAnalysisPage.axaml"));
		string screenTable = File.ReadAllText(Path.Combine(root, "DevTools", "FrontierScreenAreaTable.axaml"));
		string screenManage = File.ReadAllText(Path.Combine(root, "DevTools", "FrontierScreenManagePage.axaml"));
		string templateHelper = File.ReadAllText(Path.Combine(root, "DevTools", "FrontierTemplateHelperPage.axaml"));
		string agentGenerator = File.ReadAllText(Path.Combine(root, "DevTools", "FrontierAgentTemplateGeneratorPage.axaml"));
		string oneDragonRun = File.ReadAllText(Path.Combine(root, "OneDragon", "FrontierOneDragonRunPage.axaml"));
		string worldPatrol = File.ReadAllText(Path.Combine(root, "WorldPatrol", "FrontierWorldPatrolPage.axaml"));

		Assert.Contains("RowDefinitions=\"Auto,*\"", imageAnalysis, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,*\"", screenTable, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,*\"", screenManage, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,Auto,Auto,*\"", templateHelper, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,*\"", agentGenerator, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,*\"", oneDragonRun, StringComparison.Ordinal);
		Assert.Contains("RowDefinitions=\"Auto,*,Auto,*\"", worldPatrol, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FATabView", text, StringComparison.Ordinal);
		Assert.Contains("<fa:FATabViewItem", text, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FATabView", text, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FASettingsExpanderItem", actualString, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FASettingsExpander Header=\"双倍活动\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FACommandBar", actualString, StringComparison.Ordinal);
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
		string spacing = File.ReadAllText(Path.Combine(path, "Theme", "ZzzSpacing.axaml"));
		Assert.Contains("Margin=\"{DynamicResource SampleAppPageMargin}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<Thickness x:Key=\"SampleAppPageMargin\">11</Thickness>", spacing, StringComparison.Ordinal);
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
		Assert.Contains("PlaceholderText=\"输入状态关键词过滤...\"", actualString, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FACommandBar", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FACommandBarButton Label=\"如何让AI打得更好？\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FACommandBarButton Label=\"查看指南\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FACommandBarButton Label=\"前往社区\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAContentDialog", actualString, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FACommandBarButton", actualString, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FASettingsExpanderItem Content=\"终结技一好就放\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FASettingsExpanderItem Content=\"使用合并配置文件\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FASettingsExpanderItem Content=\"GPU运算\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FANumberBox x:Name=\"ScreenshotIntervalNumber\"", actualString, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FATabView x:Name=\"ModeTabs\"", axaml, StringComparison.Ordinal);
		Assert.Contains("FindControl<FATabView>(\"ModeTabs\")", source, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FANavigationView", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FANavigationViewItem", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FANavigationView.MenuItemTemplate>", actualString, StringComparison.Ordinal);
		Assert.Contains("PaneDisplayMode=\"Left\"", actualString, StringComparison.Ordinal);
		Assert.Contains("OpenPaneLength=\"{DynamicResource ZzzNavigationPaneWidth}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Classes=\"zzz-navigation-item\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Text=\"{Binding Text}\"", actualString, StringComparison.Ordinal);
		Assert.Contains("Margin=\"-4,0,0,0\"", actualString, StringComparison.Ordinal);
		Assert.Contains("<DataTemplate", actualString, StringComparison.Ordinal);
		Assert.Contains("<fa:FAFrame", actualString, StringComparison.Ordinal);
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
		Assert.DoesNotContain("ZzzWindowBackdropService", actualString4, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FASymbolIcon Symbol=\"Home\"", text, StringComparison.Ordinal);
		Assert.Contains("<fa:FAPathIcon", text, StringComparison.Ordinal);
		Assert.Contains("<fa:FASymbolIcon Symbol=\"Library\"", text, StringComparison.Ordinal);
		Assert.Contains("<fa:FASymbolIcon Symbol=\"Message\"", text, StringComparison.Ordinal);
		Assert.True(text.IndexOf("x:Name=\"HomeLinkButton\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"GithubLinkButton\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("x:Name=\"GithubLinkButton\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"DocsLinkButton\"", StringComparison.Ordinal));
		Assert.True(text.IndexOf("x:Name=\"DocsLinkButton\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"ChannelLinkButton\"", StringComparison.Ordinal));
		Assert.Contains("<fa:FATeachingTip", text, StringComparison.Ordinal);
		Assert.Contains("<fa:FAContentDialog", text, StringComparison.Ordinal);
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
		Assert.Contains("FASymbol.PlayFilled : FASymbol.Settings", actualString, StringComparison.Ordinal);
		Assert.Contains("Width=\"589\"", text2, StringComparison.Ordinal);
		Assert.Contains("Width=\"225\"", text2, StringComparison.Ordinal);
		Assert.Contains("<fa:FATabView", text2, StringComparison.Ordinal);
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
		Assert.Contains("StyleKeyOverride => typeof(FATabView)", actualString, StringComparison.Ordinal);
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
		Assert.Contains("<fa:FAFrame", actualString, StringComparison.Ordinal);
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

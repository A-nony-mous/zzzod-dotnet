using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Xunit;
using ZzzOd.Gui.Shell;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// GUI AXAML 和 FluentAvalonia 静态审计。
/// </summary>
[Trait("Category", "Audit")]
public sealed class GuiStaticAuditTests
{
	private sealed record GuiAuditResult(IReadOnlyList<string> HandwrittenFluentReplacements);

	[Fact]
	public void FrontierShellUsesIndependentSampleMainViewWithoutExplanatoryCopy()
	{
		string path = FindGuiRoot();
		string window = File.ReadAllText(Path.Combine(path, "Views", "FrontierShellWindow.axaml"));
		string windowCode = File.ReadAllText(Path.Combine(path, "Views", "FrontierShellWindow.cs"));
		string mainView = File.ReadAllText(Path.Combine(path, "Views", "FrontierMainView.axaml"));
		string mainViewCode = File.ReadAllText(Path.Combine(path, "Views", "FrontierMainView.cs"));
		string splash = File.ReadAllText(Path.Combine(path, "Views", "FrontierStartupSplash.axaml"));
		string splashCode = File.ReadAllText(Path.Combine(path, "Views", "FrontierStartupSplash.axaml.cs"));
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
		Assert.Contains("SplashScreen = _startupSplash", windowCode, StringComparison.Ordinal);
		Assert.Contains("IFAApplicationSplashScreen", splashCode, StringComparison.Ordinal);
		Assert.Contains("DispatcherPriority.Background", splashCode, StringComparison.Ordinal);
		Assert.Contains("StartInitialNavigation", mainViewCode, StringComparison.Ordinal);
		Assert.Contains("<fa:FAProgressRing", splash, StringComparison.Ordinal);
		Assert.Contains("Width=\"132\"", splash, StringComparison.Ordinal);
		Assert.DoesNotContain("AppTitleText", splash, StringComparison.Ordinal);
		Assert.DoesNotContain("FrontierWindowTitle", splashCode, StringComparison.Ordinal);
		Assert.DoesNotContain("Loading", splash, StringComparison.OrdinalIgnoreCase);
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
	}

	[Fact]
	public void RunToastAndCommandLayoutAreSharedAcrossRunHosts()
	{
		string root = FindGuiRoot();
		string frontier = File.ReadAllText(Path.Combine(root, "Views", "FrontierMainView.axaml"));
		string shellRuntime = File.ReadAllText(Path.Combine(root, "Shell", "ZzzShellWindowRuntime.cs"));
		string runPanel = File.ReadAllText(Path.Combine(root, "Controls", "ZzzRunPanel.axaml"));
		string componentStyles = File.ReadAllText(Path.Combine(root, "Theme", "ZzzComponentStyles.axaml"));
		string spacing = File.ReadAllText(Path.Combine(root, "Theme", "ZzzSpacing.axaml"));

		Assert.Contains("x:Name=\"ToastBar\"", frontier, StringComparison.Ordinal);
		Assert.Contains("run.stateChanged", shellRuntime, StringComparison.Ordinal);
		Assert.Contains("ShowToast", shellRuntime, StringComparison.Ordinal);
		Assert.Contains("Classes=\"accent zzz-run-command\"", runPanel, StringComparison.Ordinal);
		Assert.Contains("ColumnSpacing=\"{DynamicResource ZzzRunCommandSpacing}\"", runPanel, StringComparison.Ordinal);
		Assert.Contains("ZzzRunCommandMinHeight", componentStyles, StringComparison.Ordinal);
		Assert.Contains("<x:Double x:Key=\"ZzzRunCommandMinHeight\">40</x:Double>", spacing, StringComparison.Ordinal);

		string[] localOverrides =
		[
			Path.Combine(root, "Views", "FrontierPages", "GameAssistant", "FrontierBattleAssistantPage.axaml"),
			Path.Combine(root, "Views", "FrontierPages", "GameAssistant", "FrontierCommissionAssistantPage.axaml"),
		];
		foreach (string path in localOverrides)
		{
			string source = File.ReadAllText(path);
			Assert.DoesNotContain("Value=\"327\"", source, StringComparison.Ordinal);
			Assert.DoesNotContain("Value=\"508\"", source, StringComparison.Ordinal);
			Assert.DoesNotContain("Selector=\"Button#PrimaryButton\"", source, StringComparison.Ordinal);
			Assert.DoesNotContain("Selector=\"Button#StopButton\"", source, StringComparison.Ordinal);
		}
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

		string editor = File.ReadAllText(Path.Combine(path, "Views", "FrontierPages", "WorldPatrol", "FrontierWorldPatrolLargeMapIconEditorWindow.cs"));
		Assert.Contains("ShowAsync(this)", editor, StringComparison.Ordinal);
		string dialog = File.ReadAllText(Path.Combine(path, "Services", "Dialogs", "ZzzDialogService.cs"));
		string shellRuntime = File.ReadAllText(Path.Combine(path, "Shell", "ZzzShellWindowRuntime.cs"));
		Assert.Contains("FATeachingTip", dialog, StringComparison.Ordinal);
		Assert.Contains("FAContentDialog", dialog, StringComparison.Ordinal);
		Assert.Contains("FAInfoBar", shellRuntime, StringComparison.Ordinal);
		Assert.Contains("ShowToast", shellRuntime, StringComparison.Ordinal);
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
			Assert.DoesNotContain("using:ZzzOd.Gui.PageModels.Standalone", content, StringComparison.Ordinal);
			Assert.DoesNotContain("using:ZzzOd.Gui.PageModels.Settings", content, StringComparison.Ordinal);
			Assert.DoesNotContain("using:ZzzOd.Gui.PageModels.ApplicationSettings", content, StringComparison.Ordinal);
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
				// 不带折叠外壳的设置行列表用 ItemsControl 承载，行本身仍是 SettingsExpanderItem。
				bool itemTemplate = item.Parent?.Name.LocalName == "DataTemplate"
					&& item.Parent.Parent?.Name.LocalName is "FASettingsExpander.ItemTemplate" or "ItemsControl.ItemTemplate";
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

	[Fact]
	public void NavigationRegistryKeepsPythonPrimaryAndFooterNavigationOrder()
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

	/// <summary>
	/// Fluent Pivot 必须复用官方 TabView 主题，并让两层 Frame 拉伸实际页面。
	/// </summary>
	[Fact]
	public void FluentPivotUsesBaseTabViewThemeAndStretchFrames()
	{
		string path = FindGuiRoot();
		string actualString = File.ReadAllText(Path.Combine(path, "Controls", "ZzzPivotPage.cs"));
		string actualString2 = File.ReadAllText(Path.Combine(path, "Controls", "ZzzPageStackHost.axaml"));
		Assert.Contains("StyleKeyOverride => typeof(FATabView)", actualString, StringComparison.Ordinal);
		Assert.Contains("((IList)TabItems).Add(tabItem)", actualString, StringComparison.Ordinal);
		Assert.DoesNotContain("_compatibilityFrame", actualString, StringComparison.Ordinal);
		Assert.Contains("HorizontalContentAlignment=\"Stretch\"", actualString2, StringComparison.Ordinal);
		Assert.Contains("VerticalContentAlignment=\"Stretch\"", actualString2, StringComparison.Ordinal);
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
		return new GuiAuditResult(list4.Distinct<string>(StringComparer.Ordinal).ToArray());
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

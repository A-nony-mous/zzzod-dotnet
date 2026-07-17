using System;
using System.Collections.Generic;
using FluentAvalonia.UI.Controls;
using Xunit;
using ZzzOd.Gui;
using ZzzOd.Gui.Architecture;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// FluentAvalonia 官方组件映射测试。
/// </summary>
public sealed class FluentComponentMapTests
{
	/// <summary>
	/// 证据主题参数应覆盖亮色、暗色和高对比。
	/// </summary>
	[Theory]
	[InlineData(new object[] { "light" })]
	[InlineData(new object[] { "dark" })]
	[InlineData(new object[] { "highcontrast" })]
	[InlineData(new object[] { "high-contrast" })]
	public void EvidenceThemeResolverSupportsRequiredVariants(string value)
	{
		Assert.NotNull(App.ResolveEvidenceThemeVariant(value));
	}

	/// <summary>
	/// 所有批准角色都必须映射到官方类型。
	/// </summary>
	[Fact]
	public void ApprovedRolesMapToOfficialFluentAvaloniaControls()
	{
		Dictionary<ZzzFluentComponentRole, Type> dictionary = new Dictionary<ZzzFluentComponentRole, Type>();
		dictionary[ZzzFluentComponentRole.SettingsGroup] = typeof(SettingsExpander);
		dictionary[ZzzFluentComponentRole.SettingsItem] = typeof(SettingsExpanderItem);
		dictionary[ZzzFluentComponentRole.ComboBox] = typeof(FAComboBox);
		dictionary[ZzzFluentComponentRole.NumberInput] = typeof(NumberBox);
		dictionary[ZzzFluentComponentRole.CommandBar] = typeof(CommandBar);
		dictionary[ZzzFluentComponentRole.Dialog] = typeof(ContentDialog);
		dictionary[ZzzFluentComponentRole.TeachingTip] = typeof(TeachingTip);
		dictionary[ZzzFluentComponentRole.InfoBar] = typeof(InfoBar);
		dictionary[ZzzFluentComponentRole.Tab] = typeof(TabView);
		dictionary[ZzzFluentComponentRole.Frame] = typeof(Frame);
		dictionary[ZzzFluentComponentRole.Navigation] = typeof(NavigationView);
		dictionary[ZzzFluentComponentRole.SymbolIcon] = typeof(SymbolIcon);
		dictionary[ZzzFluentComponentRole.FontIcon] = typeof(FontIcon);
		Dictionary<ZzzFluentComponentRole, Type> dictionary2 = dictionary;
		Assert.Equal(dictionary2.Count, ZzzFluentComponentMap.All.Count);
		foreach (var (role, type2) in dictionary2)
		{
			Assert.Equal(type2, ZzzFluentComponentMap.GetRequired(role));
			Assert.Equal(typeof(NavigationView).Assembly, type2.Assembly);
		}
	}
}

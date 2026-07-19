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
		dictionary[ZzzFluentComponentRole.SettingsGroup] = typeof(FASettingsExpander);
		dictionary[ZzzFluentComponentRole.SettingsItem] = typeof(FASettingsExpanderItem);
		dictionary[ZzzFluentComponentRole.ComboBox] = typeof(FAComboBox);
		dictionary[ZzzFluentComponentRole.NumberInput] = typeof(FANumberBox);
		dictionary[ZzzFluentComponentRole.FACommandBar] = typeof(FACommandBar);
		dictionary[ZzzFluentComponentRole.Dialog] = typeof(FAContentDialog);
		dictionary[ZzzFluentComponentRole.FATeachingTip] = typeof(FATeachingTip);
		dictionary[ZzzFluentComponentRole.FAInfoBar] = typeof(FAInfoBar);
		dictionary[ZzzFluentComponentRole.Tab] = typeof(FATabView);
		dictionary[ZzzFluentComponentRole.FAFrame] = typeof(FAFrame);
		dictionary[ZzzFluentComponentRole.Navigation] = typeof(FANavigationView);
		dictionary[ZzzFluentComponentRole.FASymbolIcon] = typeof(FASymbolIcon);
		dictionary[ZzzFluentComponentRole.FAFontIcon] = typeof(FAFontIcon);
		Dictionary<ZzzFluentComponentRole, Type> dictionary2 = dictionary;
		Assert.Equal(dictionary2.Count, ZzzFluentComponentMap.All.Count);
		foreach (var (role, type2) in dictionary2)
		{
			Assert.Equal(type2, ZzzFluentComponentMap.GetRequired(role));
			Assert.Equal(typeof(FANavigationView).Assembly, type2.Assembly);
		}
	}
}

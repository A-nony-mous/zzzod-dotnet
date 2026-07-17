using System;
using Xunit;
using ZzzOd.RealGameE2E;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class RealGameE2EHotkeyTests
{
	[Theory]
	[InlineData(new object[] { "f9", 120 })]
	[InlineData(new object[] { "F10", 121 })]
	[InlineData(new object[] { "escape", 27 })]
	[InlineData(new object[] { "a", 65 })]
	public void ResolveVirtualKey_UsesConfiguredStopKey(string key, int expected)
	{
		Assert.Equal(expected, RealGameE2EHotkey.ResolveVirtualKey(key));
	}

	[Fact]
	public void ResolveVirtualKey_RejectsUnsupportedStopKey()
	{
		Assert.Throws<ArgumentException>(() => RealGameE2EHotkey.ResolveVirtualKey("ctrl+f10"));
	}
}

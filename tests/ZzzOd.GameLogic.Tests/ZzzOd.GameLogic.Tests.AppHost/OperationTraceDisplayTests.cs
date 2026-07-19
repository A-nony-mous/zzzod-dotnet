using System;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Controls;

namespace ZzzOd.GameLogic.Tests.AppHost;

public sealed class OperationTraceDisplayTests
{
	[Fact]
	public void FormatOperationTraceIncludesTransitionAndStatus()
	{
		ZzzOperationTraceDto trace = new ZzzOperationTraceDto(
			"suibian_temple",
			0,
			"随便观",
			"处理德丰大押",
			"处理好物铺",
			"完成后返回",
			0,
			"transition",
			"大世界-普通",
			null,
			null,
			new DateTimeOffset(2026, 7, 19, 19, 47, 24, TimeSpan.FromHours(8)));

		string line = ZzzLogDisplayCard.FormatOperationTrace(trace);

		Assert.Contains("随便观", line, StringComparison.Ordinal);
		Assert.Contains("处理德丰大押 -> 完成后返回", line, StringComparison.Ordinal);
		Assert.Contains("返回状态 大世界-普通", line, StringComparison.Ordinal);
	}

	[Fact]
	public void FormatOperationTraceIncludesRetryAndException()
	{
		ZzzOperationTraceDto trace = new ZzzOperationTraceDto(
			"coffee",
			0,
			"咖啡店",
			"选择咖啡",
			null,
			null,
			2,
			"exception",
			"识别失败",
			"InvalidOperationException",
			"识别失败",
			DateTimeOffset.UtcNow);

		string line = ZzzLogDisplayCard.FormatOperationTrace(trace);

		Assert.Contains("重试 2", line, StringComparison.Ordinal);
		Assert.Contains("异常 InvalidOperationException: 识别失败", line, StringComparison.Ordinal);
	}
}

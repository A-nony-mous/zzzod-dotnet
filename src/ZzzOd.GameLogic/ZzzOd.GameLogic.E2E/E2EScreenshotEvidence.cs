using System;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 截图 evidence。
/// </summary>
public sealed class E2EScreenshotEvidence
{
	public string Label { get; set; } = string.Empty;

	public string? Path { get; set; }

	public string? Summary { get; set; }

	public DateTimeOffset CapturedAtUtc { get; set; }
}

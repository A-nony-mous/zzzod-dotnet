using System;
using System.Collections.Generic;

namespace ZzzOd.GameLogic.Application.SuibianTemple.Operations;

internal sealed class SuibianTempleAutoManageRecognitionSummary
{
	public string? ActiveScreenName { get; set; }

	public string? AreaRect { get; set; }

	public IReadOnlyList<string> OcrTexts { get; set; } = Array.Empty<string>();

	public IReadOnlyList<SuibianTempleOcrTargetMatch> IgnoredMatches { get; set; } = Array.Empty<SuibianTempleOcrTargetMatch>();

	public IReadOnlyList<SuibianTempleOcrTargetMatch> TargetMatches { get; set; } = Array.Empty<SuibianTempleOcrTargetMatch>();

	public SuibianTempleOcrTargetMatch? SelectedTarget { get; set; }

	public bool StopHostingVisible { get; set; }

	public bool StartHostingVisible { get; set; }

	public bool ConfirmVisible { get; set; }

	public string? FailureReason { get; set; }
}

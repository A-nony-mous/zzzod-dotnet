using System;
using System.Collections.Generic;
using System.Linq;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E evidence 记录。
/// </summary>
public sealed class E2EEvidenceRecord
{
	public string Command { get; set; } = string.Empty;

	public E2EEvidenceProfileSnapshot Profile { get; set; } = new E2EEvidenceProfileSnapshot();

	public List<string> ApplicationIds { get; set; } = new List<string>();

	public List<E2EEvidenceResourceSnapshot> Resources { get; set; } = new List<E2EEvidenceResourceSnapshot>();

	public string? LogPath { get; set; }

	public DateTimeOffset StartedAtUtc { get; set; }

	public DateTimeOffset? FinishedAtUtc { get; set; }

	public E2EEvidenceStatus Status { get; set; } = E2EEvidenceStatus.Blocked;

	public string? FailureReason { get; set; }

	public List<E2EScreenshotEvidence> Screenshots { get; set; } = new List<E2EScreenshotEvidence>();

	public E2EAudioEvidence? Audio { get; set; }

	public E2ECaptureReadinessEvidence? CaptureReadiness { get; set; }

	/// <summary>
	/// 从 E2E profile 和资源校验结果创建 evidence。
	/// </summary>
	/// <param name="command">运行命令。</param>
	/// <param name="environment">运行环境。</param>
	/// <param name="profile">E2E profile。</param>
	/// <param name="resourceValidation">资源校验结果。</param>
	/// <param name="logPath">日志路径。</param>
	/// <param name="startedAtUtc">开始时间。</param>
	/// <returns>evidence 记录。</returns>
	public static E2EEvidenceRecord Create(string command, OneDragonEnvironment environment, E2EAutomationProfile profile, E2EResourceValidationResult resourceValidation, string? logPath, DateTimeOffset startedAtUtc)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		ArgumentNullException.ThrowIfNull(profile, "profile");
		ArgumentNullException.ThrowIfNull(resourceValidation, "resourceValidation");
		E2EEvidenceRecord e2EEvidenceRecord = new E2EEvidenceRecord();
		e2EEvidenceRecord.Command = command;
		e2EEvidenceRecord.Profile = E2EEvidenceProfileSnapshot.From(environment, profile);
		e2EEvidenceRecord.ApplicationIds = profile.ApplicationIds.ToList();
		e2EEvidenceRecord.Resources = resourceValidation.Items.Select(E2EEvidenceResourceSnapshot.From).ToList();
		e2EEvidenceRecord.LogPath = logPath;
		e2EEvidenceRecord.StartedAtUtc = startedAtUtc;
		return e2EEvidenceRecord;
	}

	/// <summary>
	/// 记录结束状态。
	/// </summary>
	/// <param name="status">结束状态。</param>
	/// <param name="finishedAtUtc">结束时间。</param>
	/// <param name="failureReason">失败或阻塞原因。</param>
	public void Finish(E2EEvidenceStatus status, DateTimeOffset finishedAtUtc, string? failureReason = null)
	{
		Status = status;
		FinishedAtUtc = finishedAtUtc;
		FailureReason = failureReason;
	}
}

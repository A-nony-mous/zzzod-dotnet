using System;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 资源 evidence 快照。
/// </summary>
public sealed class E2EEvidenceResourceSnapshot
{
	public string Id { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string LocalPath { get; set; } = string.Empty;

	public string PythonSourcePath { get; set; } = string.Empty;

	public E2EResourceStatus Status { get; set; }

	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// 从资源校验项创建快照。
	/// </summary>
	/// <param name="item">资源校验项。</param>
	/// <returns>资源快照。</returns>
	public static E2EEvidenceResourceSnapshot From(E2EResourceValidationItem item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		return new E2EEvidenceResourceSnapshot
		{
			Id = item.Id,
			DisplayName = item.DisplayName,
			LocalPath = item.LocalPath,
			PythonSourcePath = item.PythonSourcePath,
			Status = item.Status,
			Message = item.Message
		};
	}
}

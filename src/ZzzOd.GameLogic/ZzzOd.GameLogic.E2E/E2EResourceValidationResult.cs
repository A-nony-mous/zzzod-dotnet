using System;
using System.Collections.Generic;
using System.Linq;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 资源校验结果。
/// </summary>
public sealed class E2EResourceValidationResult
{
	/// <summary>
	/// 全部校验项。
	/// </summary>
	public IReadOnlyList<E2EResourceValidationItem> Items { get; }

	/// <summary>
	/// 解析运行根的来源。
	/// </summary>
	public string RunRootSource { get; set; } = string.Empty;

	/// <summary>
	/// 已校验的规范化运行根。
	/// </summary>
	public string RunRoot { get; set; } = string.Empty;

	/// <summary>
	/// 资源清单 schema 版本。
	/// </summary>
	public int? ManifestSchemaVersion { get; set; }

	/// <summary>
	/// 资源清单 RID。
	/// </summary>
	public string ManifestRid { get; set; } = string.Empty;

	/// <summary>
	/// 资源清单来源摘要。
	/// </summary>
	public string ManifestSourceSummary { get; set; } = string.Empty;

	/// <summary>
	/// 缺失项。
	/// </summary>
	public IReadOnlyList<E2EResourceValidationItem> MissingItems => Items.Where((E2EResourceValidationItem item) => item.Status == E2EResourceStatus.Missing).ToArray();

	/// <summary>
	/// 是否通过校验。
	/// </summary>
	public bool IsValid => MissingItems.Count == 0;

	/// <summary>
	/// 缺失资源摘要。
	/// </summary>
	public string FailureSummary => IsValid ? string.Empty : string.Join(Environment.NewLine, MissingItems.Select((E2EResourceValidationItem item) => $"{item.DisplayName} 缺失：{item.LocalPath}。请从 {item.PythonSourcePath} 复制。"));

	/// <summary>
	/// 初始化 E2E 资源校验结果。
	/// </summary>
	/// <param name="items">校验项。</param>
	public E2EResourceValidationResult(IReadOnlyList<E2EResourceValidationItem> items)
	{
		Items = items;
	}

	/// <summary>
	/// 确认全部资源存在。
	/// </summary>
	/// <exception cref="T:System.InvalidOperationException">存在缺失资源时抛出。</exception>
	public void EnsureValid()
	{
		if (!IsValid)
		{
			throw new InvalidOperationException(FailureSummary);
		}
	}
}

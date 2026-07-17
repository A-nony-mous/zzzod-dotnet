using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// 写入 E2E evidence。
/// </summary>
public sealed class E2EEvidenceWriter
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private readonly string _evidenceDirectory;

	/// <summary>
	/// 初始化 evidence writer。
	/// </summary>
	/// <param name="evidenceDirectory">evidence 输出目录。</param>
	public E2EEvidenceWriter(string evidenceDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(evidenceDirectory, "evidenceDirectory");
		_evidenceDirectory = Path.GetFullPath(evidenceDirectory);
	}

	/// <summary>
	/// 写入 evidence JSON。
	/// </summary>
	/// <param name="record">evidence 记录。</param>
	/// <param name="fileName">文件名。</param>
	/// <returns>写入后的绝对路径。</returns>
	public string Write(E2EEvidenceRecord record, string fileName = "evidence.json")
	{
		ArgumentNullException.ThrowIfNull(record, "record");
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName, "fileName");
		Directory.CreateDirectory(_evidenceDirectory);
		string text = Path.Combine(_evidenceDirectory, fileName);
		File.WriteAllText(text, JsonSerializer.Serialize(record, JsonOptions));
		return text;
	}
}

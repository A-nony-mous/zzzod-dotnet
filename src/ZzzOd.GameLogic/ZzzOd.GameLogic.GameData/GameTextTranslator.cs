using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OneDragon.Core.Runtime;

namespace ZzzOd.GameLogic.GameData;

/// <summary>
/// 读取与 BaselineParity <c>gt(message, "game")</c> 同源的游戏文本目录。
/// </summary>
public static class GameTextTranslator
{
	private enum CatalogField
	{
		None,
		MessageId,
		MessageText
	}

	private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> CatalogCache = new ConcurrentDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// 使用当前游戏语言翻译游戏画面中识别到的文本。
	/// </summary>
	/// <param name="environment">运行环境。</param>
	/// <param name="gameLanguage">当前实例的游戏语言。</param>
	/// <param name="message">BaselineParity 资源中的中文源文本。</param>
	/// <returns>游戏画面使用的对应文本。</returns>
	public static string Translate(OneDragonEnvironment environment, string? gameLanguage, string? message)
	{
		ArgumentNullException.ThrowIfNull(environment, "environment");
		if (string.IsNullOrEmpty(message))
		{
			return string.Empty;
		}
		string text = NormalizeLanguage(gameLanguage);
		string resourcePath = environment.GetResourcePath("assets", "text", "game", text + ".po");
		IReadOnlyDictionary<string, string> orAdd = CatalogCache.GetOrAdd(resourcePath, LoadCatalog);
		string value;
		return orAdd.TryGetValue(message, out value) ? value : message;
	}

	private static string NormalizeLanguage(string? language)
	{
		return string.Equals(language?.Trim(), "cn", StringComparison.OrdinalIgnoreCase) ? "zh" : (language?.Trim() ?? "zh");
	}

	private static IReadOnlyDictionary<string, string> LoadCatalog(string path)
	{
		if (!File.Exists(path))
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		string text = null;
		string text2 = null;
		CatalogField catalogField = CatalogField.None;
		foreach (string item in File.ReadLines(path))
		{
			string text3 = item.Trim();
			if (text3.StartsWith("msgid ", StringComparison.Ordinal))
			{
				Commit(dictionary, text, text2);
				text = ReadQuotedValue(text3.Substring(6));
				text2 = null;
				catalogField = CatalogField.MessageId;
			}
			else if (text3.StartsWith("msgstr ", StringComparison.Ordinal))
			{
				text2 = ReadQuotedValue(text3.Substring(7));
				catalogField = CatalogField.MessageText;
			}
			else if (text3.StartsWith('"'))
			{
				string text4 = ReadQuotedValue(text3);
				switch (catalogField)
				{
				case CatalogField.MessageId:
					text += text4;
					break;
				case CatalogField.MessageText:
					text2 += text4;
					break;
				}
			}
		}
		Commit(dictionary, text, text2);
		return dictionary;
	}

	private static void Commit(IDictionary<string, string> translations, string? messageId, string? messageText)
	{
		if (!string.IsNullOrEmpty(messageId) && messageText != null)
		{
			translations[messageId] = messageText;
		}
	}

	private static string ReadQuotedValue(string value)
	{
		return JsonSerializer.Deserialize<string>(value) ?? string.Empty;
	}
}

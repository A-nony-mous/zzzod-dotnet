using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ API 配置。
/// </summary>
public sealed class ZzzApiOptions
{
	/// <summary>
	/// 是否启用 API。
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// 监听地址。
	/// </summary>
	public string ListenAddress { get; set; } = "127.0.0.1";

	/// <summary>
	/// 监听端口。
	/// </summary>
	public int Port { get; set; } = 18990;

	/// <summary>
	/// Bearer token。
	/// </summary>
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// CORS 允许来源。
	/// </summary>
	public List<string> CorsOrigins { get; set; } = new List<string>();

	/// <summary>
	/// 是否随 GUI 启动 API。
	/// </summary>
	public bool StartWithGui { get; set; }

	/// <summary>
	/// 根据运行根目录加载配置。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <returns>API 配置。</returns>
	public static ZzzApiOptions LoadOrCreate(string runRoot)
	{
		string configPath = GetConfigPath(runRoot);
		if (File.Exists(configPath))
		{
			string json = File.ReadAllText(configPath);
			ZzzApiOptions zzzApiOptions = JsonSerializer.Deserialize<ZzzApiOptions>(json);
			if (zzzApiOptions != null)
			{
				zzzApiOptions.EnsureToken();
				zzzApiOptions.Save(runRoot);
				return zzzApiOptions;
			}
		}
		ZzzApiOptions zzzApiOptions2 = new ZzzApiOptions();
		zzzApiOptions2.EnsureToken();
		zzzApiOptions2.Save(runRoot);
		return zzzApiOptions2;
	}

	/// <summary>
	/// 保存配置。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	public void Save(string runRoot)
	{
		string configPath = GetConfigPath(runRoot);
		Directory.CreateDirectory(Path.GetDirectoryName(configPath));
		JsonSerializerOptions options = new JsonSerializerOptions
		{
			WriteIndented = true
		};
		File.WriteAllText(configPath, JsonSerializer.Serialize(this, options));
	}

	/// <summary>
	/// 校验 token。
	/// </summary>
	/// <param name="token">请求 token。</param>
	/// <returns>是否有效。</returns>
	public bool IsTokenValid(string? token)
	{
		EnsureToken();
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(Token);
		byte[] bytes2 = Encoding.UTF8.GetBytes(token);
		return bytes.Length == bytes2.Length && CryptographicOperations.FixedTimeEquals(bytes, bytes2);
	}

	/// <summary>
	/// 确保 token 已生成。
	/// </summary>
	public void EnsureToken()
	{
		if (string.IsNullOrWhiteSpace(Token))
		{
			Span<byte> span = stackalloc byte[32];
			RandomNumberGenerator.Fill(span);
			Token = Convert.ToHexString(span).ToLowerInvariant();
		}
	}

	/// <summary>
	/// 重置 token 并保存。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <returns>新 token。</returns>
	public string ResetToken(string runRoot)
	{
		Token = string.Empty;
		EnsureToken();
		Save(runRoot);
		return Token;
	}

	/// <summary>
	/// 获取配置路径。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <returns>配置路径。</returns>
	public static string GetConfigPath(string runRoot)
	{
		return Path.Combine(runRoot, "config", "api_host.json");
	}
}

using System;
using System.Collections.Generic;
using System.IO;

namespace ZzzOd.AppHost;

/// <summary>
/// 为 GUI、AppHost 和验收工具统一解析运行根目录。
/// </summary>
public static class ZzzRunRootResolver
{
	/// <summary>
	/// 显式运行根目录参数。
	/// </summary>
	public const string ArgumentName = "--run-root";

	/// <summary>
	/// 外部运行根目录环境变量。
	/// </summary>
	public const string EnvironmentVariableName = "ZZZOD_RUN_ROOT";

	/// <summary>
	/// 使用进程参数、环境变量和应用目录解析运行根目录。
	/// </summary>
	/// <param name="args">启动参数。</param>
	/// <returns>解析结果。</returns>
	public static ZzzRunRootResolution Resolve(IReadOnlyList<string> args)
	{
		return Resolve(args, Environment.GetEnvironmentVariable("ZZZOD_RUN_ROOT"), AppContext.BaseDirectory);
	}

	/// <summary>
	/// 使用可注入的外部值解析运行根目录。
	/// </summary>
	/// <param name="args">启动参数。</param>
	/// <param name="environmentRunRoot">环境变量提供的路径。</param>
	/// <param name="applicationBaseDirectory">应用程序所在目录。</param>
	/// <returns>解析结果。</returns>
	public static ZzzRunRootResolution Resolve(IReadOnlyList<string> args, string? environmentRunRoot, string applicationBaseDirectory)
	{
		ArgumentNullException.ThrowIfNull(args, "args");
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory, "applicationBaseDirectory");
		string fullPath = Path.GetFullPath(applicationBaseDirectory);
		string text = FindCommandLineRunRoot(args);
		if (text != null)
		{
			return Create(text, fullPath, ZzzRunRootSource.CommandLine);
		}
		if (!string.IsNullOrWhiteSpace(environmentRunRoot))
		{
			return Create(environmentRunRoot, fullPath, ZzzRunRootSource.Environment);
		}
		return Create(fullPath, fullPath, ZzzRunRootSource.ApplicationBaseDirectory);
	}

	private static ZzzRunRootResolution Create(string path, string applicationBaseDirectory, ZzzRunRootSource source)
	{
		string path2 = (Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, applicationBaseDirectory));
		return new ZzzRunRootResolution(new ZzzRunRoot(path2), source);
	}

	private static string? FindCommandLineRunRoot(IReadOnlyList<string> args)
	{
		string text = "--run-root=";
		for (int i = 0; i < args.Count; i++)
		{
			string text2 = args[i];
			if (text2.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				string text3 = text2.Substring(text.Length);
				if (string.IsNullOrWhiteSpace(text3))
				{
					throw new ArgumentException("--run-root 需要非空路径。", "args");
				}
				return text3;
			}
			if (string.Equals(text2, "--run-root", StringComparison.OrdinalIgnoreCase))
			{
				if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
				{
					throw new ArgumentException("--run-root 需要路径参数。", "args");
				}
				return args[i + 1];
			}
		}
		return null;
	}
}

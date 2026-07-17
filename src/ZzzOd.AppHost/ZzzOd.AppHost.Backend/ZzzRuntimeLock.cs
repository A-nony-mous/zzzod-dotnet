using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ZzzOd.AppHost.Backend;

/// <summary>
/// ZZZ 运行根目录互斥锁。
/// </summary>
public sealed class ZzzRuntimeLock : IDisposable
{
	private readonly Semaphore _semaphore;

	private bool _disposed;

	/// <summary>
	/// 运行根目录。
	/// </summary>
	public string RunRoot { get; }

	private ZzzRuntimeLock(string runRoot, Semaphore semaphore)
	{
		RunRoot = runRoot;
		_semaphore = semaphore;
	}

	/// <summary>
	/// 尝试获取运行根目录锁。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <returns>运行根目录锁；失败时返回 null。</returns>
	public static ZzzRuntimeLock? TryAcquire(string runRoot)
	{
		string fullPath = Path.GetFullPath(runRoot);
		string runRootKey = GetRunRootKey(fullPath);
		Semaphore semaphore = new Semaphore(1, 1, "Local\\ZzzOd-" + runRootKey);
		if (!semaphore.WaitOne(0))
		{
			semaphore.Dispose();
			return null;
		}
		return new ZzzRuntimeLock(fullPath, semaphore);
	}

	/// <summary>
	/// 获取运行根目录标识。
	/// </summary>
	/// <param name="runRoot">运行根目录。</param>
	/// <returns>运行根目录标识。</returns>
	public static string GetRunRootKey(string runRoot)
	{
		string fullPath = Path.GetFullPath(runRoot);
		using SHA256 sHA = SHA256.Create();
		return Convert.ToHexString(sHA.ComputeHash(Encoding.UTF8.GetBytes(fullPath))).ToLowerInvariant();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_semaphore.Release();
			_semaphore.Dispose();
		}
	}
}

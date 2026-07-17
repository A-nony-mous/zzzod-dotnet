using System;
using Microsoft.Win32;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Windows registry-backed Auto HDR preference store.
/// </summary>
public sealed class WindowsAutoHdrPreferenceStore : IAutoHdrPreferenceStore
{
	private const string KeyPath = "Software\\Microsoft\\DirectX\\UserGpuPreferences";

	/// <inheritdoc />
	public string? ReadValue(string gamePath)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\DirectX\\UserGpuPreferences", writable: false);
		return registryKey?.GetValue(gamePath) as string;
	}

	/// <inheritdoc />
	public void WriteValue(string gamePath, string value)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\DirectX\\UserGpuPreferences", writable: true) ?? throw new InvalidOperationException("无法打开 Auto HDR 注册表键。");
		registryKey.SetValue(gamePath, value, RegistryValueKind.String);
	}

	/// <inheritdoc />
	public void DeleteValue(string gamePath)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\DirectX\\UserGpuPreferences", writable: true);
		registryKey?.DeleteValue(gamePath, throwOnMissingValue: false);
	}
}

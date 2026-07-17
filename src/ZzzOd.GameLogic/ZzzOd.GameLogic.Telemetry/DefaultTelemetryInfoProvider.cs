using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Telemetry;

internal sealed class DefaultTelemetryInfoProvider : ITelemetryInfoProvider
{
	private readonly ZContext _context;

	public DefaultTelemetryInfoProvider(ZContext context)
	{
		_context = context;
	}

	public TelemetryStaticInfo GetInfo()
	{
		string text = $"{Environment.MachineName}-{RuntimeInformation.OSArchitecture}";
		return new TelemetryStaticInfo(CreateStableGuid(text).ToString(), GetVersion(), GetCommitVersion(), GetLauncherVersion(), GetPlatformName(), text);
	}

	private string GetVersion()
	{
		return GetAssemblyVersion(_context.GetType().Assembly);
	}

	private static string GetCommitVersion()
	{
		return FirstNonEmpty(Environment.GetEnvironmentVariable("GIT_COMMIT"), Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION"), "unknown");
	}

	private static string GetLauncherVersion()
	{
		return FirstNonEmpty(Environment.GetEnvironmentVariable("OD_LAUNCHER_VERSION"), GetAssemblyVersion(Assembly.GetEntryAssembly()), "unknown");
	}

	private static string GetPlatformName()
	{
		if (OperatingSystem.IsWindows())
		{
			return "Windows";
		}
		if (OperatingSystem.IsMacOS())
		{
			return "Darwin";
		}
		if (OperatingSystem.IsLinux())
		{
			return "Linux";
		}
		return RuntimeInformation.OSDescription;
	}

	private static string GetAssemblyVersion(Assembly? assembly)
	{
		return FirstNonEmpty(assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, assembly?.GetName().Version?.ToString(), "unknown");
	}

	private static Guid CreateStableGuid(string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		byte[] array = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8").ToByteArray();
		SwapGuidByteOrder(array);
		byte[] array2 = new byte[array.Length + bytes.Length];
		Buffer.BlockCopy(array, 0, array2, 0, array.Length);
		Buffer.BlockCopy(bytes, 0, array2, array.Length, bytes.Length);
		byte[] array3 = SHA1.HashData(array2);
		Span<byte> destination = stackalloc byte[16];
		array3.AsSpan(0, 16).CopyTo(destination);
		destination[6] = (byte)((destination[6] & 0xF) | 0x50);
		destination[8] = (byte)((destination[8] & 0x3F) | 0x80);
		byte[] array4 = destination.ToArray();
		SwapGuidByteOrder(array4);
		return new Guid(array4);
	}

	private static void SwapGuidByteOrder(byte[] bytes)
	{
		ref byte reference = ref bytes[0];
		ref byte reference2 = ref bytes[3];
		byte b = bytes[3];
		byte b2 = bytes[0];
		reference = b;
		reference2 = b2;
		reference = ref bytes[1];
		ref byte reference3 = ref bytes[2];
		b2 = bytes[2];
		b = bytes[1];
		reference = b2;
		reference3 = b;
		reference = ref bytes[4];
		ref byte reference4 = ref bytes[5];
		b = bytes[5];
		b2 = bytes[4];
		reference = b;
		reference4 = b2;
		reference = ref bytes[6];
		ref byte reference5 = ref bytes[7];
		b2 = bytes[7];
		b = bytes[6];
		reference = b2;
		reference5 = b;
	}

	private static string FirstNonEmpty(params string?[] values)
	{
		foreach (string text in values)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		return "unknown";
	}
}

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// 按 BaselineParity <c>subprocess.Popen(..., creationflags=CREATE_BREAKAWAY_FROM_JOB)</c> 语义启动游戏命令。
/// </summary>
internal static class OpenGameProcessLauncher
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct StartupInfo
	{
		public uint Size;

		public string? Reserved;

		public string? Desktop;

		public string? Title;

		public uint X;

		public uint Y;

		public uint XSize;

		public uint YSize;

		public uint XCountChars;

		public uint YCountChars;

		public uint FillAttribute;

		public uint Flags;

		public ushort ShowWindow;

		public ushort Reserved2;

		public nint Reserved2Pointer;

		public nint StandardInput;

		public nint StandardOutput;

		public nint StandardError;
	}

	private struct ProcessInformation
	{
		public nint ProcessHandle;

		public nint ThreadHandle;

		public uint ProcessId;

		public uint ThreadId;
	}

	/// <summary>
	/// Windows <c>CREATE_BREAKAWAY_FROM_JOB</c> 标志。
	/// </summary>
	internal const uint CreationFlags = 16777216u;

	/// <summary>
	/// 启动完整的 BaselineParity 等价命令行，并立即释放本进程持有的句柄。
	/// </summary>
	/// <param name="command">包含 <c>cmd /c start</c> 的完整命令行。</param>
	/// <returns>创建成功时返回 <see langword="true" />。</returns>
	/// <exception cref="T:System.ComponentModel.Win32Exception">Windows 拒绝创建进程时抛出。</exception>
	internal static bool Start(string command)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(command, "command");
		StringBuilder commandLine = new StringBuilder(command);
		StartupInfo startupInfo = new StartupInfo
		{
			Size = (uint)Marshal.SizeOf<StartupInfo>()
		};
		if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, inheritHandles: false, 16777216u, IntPtr.Zero, null, ref startupInfo, out var processInformation))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error(), "无法启动游戏命令: " + command);
		}
		try
		{
			return true;
		}
		finally
		{
			CloseHandle(processInformation.ProcessHandle);
			CloseHandle(processInformation.ThreadHandle);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateProcessW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreateProcess(string? applicationName, StringBuilder commandLine, nint processAttributes, nint threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, nint environment, string? currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint handle);
}

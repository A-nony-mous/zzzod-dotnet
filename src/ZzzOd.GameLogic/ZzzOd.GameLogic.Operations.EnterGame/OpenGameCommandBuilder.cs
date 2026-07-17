using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Builds the Windows command used by the BaselineParity OpenGame operation.
/// </summary>
public static class OpenGameCommandBuilder
{
	/// <summary>
	/// Build a cmd/start command for launching the game executable.
	/// </summary>
	public static string Build(string gamePath, bool launchArgument, string screenSize, string fullScreen, bool popupWindow, string monitor, string? launchArgumentAdvance)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(gamePath, "gamePath");
		string directoryName = Path.GetDirectoryName(gamePath);
		string fileName = Path.GetFileName(gamePath);
		IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
		IFormatProvider provider = invariantCulture;
		DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(25, 2, invariantCulture);
		handler.AppendLiteral("cmd /c \"start \"\" /d \"");
		handler.AppendFormatted(directoryName);
		handler.AppendLiteral("\" \"");
		handler.AppendFormatted(fileName);
		handler.AppendLiteral("\"");
		string text = string.Create(provider, ref handler);
		if (launchArgument)
		{
			(string Width, string Height) tuple = SplitScreenSize(screenSize);
			string item = tuple.Width;
			string item2 = tuple.Height;
			string value = (popupWindow ? " -popupwindow" : "");
			string value2 = (string.IsNullOrWhiteSpace(launchArgumentAdvance) ? string.Empty : (launchArgumentAdvance.Trim() + " "));
			string text2 = text;
			invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider2 = invariantCulture;
			DefaultInterpolatedStringHandler handler2 = new DefaultInterpolatedStringHandler(61, 6, invariantCulture);
			handler2.AppendLiteral(" ");
			handler2.AppendFormatted(value2);
			handler2.AppendLiteral("-screen-width ");
			handler2.AppendFormatted(item);
			handler2.AppendLiteral(" -screen-height ");
			handler2.AppendFormatted(item2);
			handler2.AppendLiteral(" -screen-fullscreen ");
			handler2.AppendFormatted(fullScreen);
			handler2.AppendFormatted(value);
			handler2.AppendLiteral(" -monitor ");
			handler2.AppendFormatted(monitor);
			text = text2 + string.Create(provider2, ref handler2);
		}
		return text + " & exit\"";
	}

	private static (string Width, string Height) SplitScreenSize(string screenSize)
	{
		if (string.IsNullOrWhiteSpace(screenSize))
		{
			return (Width: "1920", Height: "1080");
		}
		string[] array = screenSize.Split('x', 2, StringSplitOptions.TrimEntries);
		return (array.Length == 2 && !string.IsNullOrWhiteSpace(array[0]) && !string.IsNullOrWhiteSpace(array[1])) ? (Width: array[0], Height: array[1]) : (Width: "1920", Height: "1080");
	}
}

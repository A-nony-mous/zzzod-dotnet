namespace ZzzOd.GameLogic.Operations.EnterGame;

/// <summary>
/// Reads and writes the Windows Auto HDR preference for the game executable.
/// </summary>
public interface IAutoHdrPreferenceStore
{
	/// <summary>Read the current preference value.</summary>
	string? ReadValue(string gamePath);

	/// <summary>Write a preference value.</summary>
	void WriteValue(string gamePath, string value);

	/// <summary>Delete a preference value.</summary>
	void DeleteValue(string gamePath);
}

namespace ZzzOd.GameLogic.E2E;

/// <summary>
/// E2E 资源校验项。
/// </summary>
public sealed record E2EResourceValidationItem(string Id, string DisplayName, string LocalPath, string PythonSourcePath, E2EResourceStatus Status, string Message);

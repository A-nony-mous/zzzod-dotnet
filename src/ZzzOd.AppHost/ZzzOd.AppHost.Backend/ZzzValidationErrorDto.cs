namespace ZzzOd.AppHost.Backend;

/// <summary>
/// 结构化校验错误。
/// </summary>
/// <param name="Scope">scope 名称。</param>
/// <param name="Key">配置 key。</param>
/// <param name="Message">错误文本。</param>
public sealed record ZzzValidationErrorDto(string? Scope, string? Key, string Message);

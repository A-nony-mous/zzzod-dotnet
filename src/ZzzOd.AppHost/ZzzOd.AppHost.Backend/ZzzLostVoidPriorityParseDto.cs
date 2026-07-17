using System.Collections.Generic;

namespace ZzzOd.AppHost.Backend;

/// <summary>优先级文本解析结果。</summary>
/// <param name="Items">有效条目。</param>
/// <param name="ErrorMessage">校验错误。</param>
public sealed record ZzzLostVoidPriorityParseDto(IReadOnlyList<string> Items, string ErrorMessage);

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

/// <summary>
/// 前卫 GUI 设计体系静态审计(gui-design-system 能力):
/// R1 颜色令牌化、R2 无页面级 /template/、R3 无 FATabView、R4 圆角/边框/字号令牌化、
/// R5 间距刻度、R6 容器无固定尺寸、R7 页面代码后置无自建消息窗口。
/// 白名单:src/ZzzOd.Gui/design-system-whitelist.md,每行「规则 | 文件相对路径 | 行内定位子串 | 理由」。
/// 判据来源:D:\FluentAvalonia(3.0.2)Fluentv2.axaml 令牌与 FAControlsGallery。
/// </summary>
[Trait("Category", "Audit")]
public sealed class DesignSystemAuditTests
{
    private static readonly IReadOnlySet<double> SpacingScale =
        new HashSet<double> { 0, 1, 2, 4, 8, 12, 16, 20, 24, 32 };

    private static readonly string[] ColorAttributeNames =
    [
        "Background", "Foreground", "BorderBrush", "Fill", "Stroke", "Color",
        "SelectionBrush", "CaretBrush", "TintColor", "FallbackColor", "GlyphBrush",
    ];

    private static readonly string[] ContainerElementNames =
    [
        "Grid", "StackPanel", "Border", "ScrollViewer", "ItemsControl",
        "TabControl", "ContentControl", "DockPanel", "WrapPanel", "ListBox", "ItemsRepeater", "Panel",
    ];

    private sealed record Violation(string Rule, string File, int Line, string Content)
    {
        public override string ToString() => $"{Rule} {File}:{Line} {Content.Trim()}";
    }

    private sealed record WhitelistEntry(string Rule, string File, string Locator, string Reason)
    {
        public bool Matches(Violation violation) =>
            string.Equals(Rule, violation.Rule, StringComparison.Ordinal)
            && violation.File.Replace('\\', '/').EndsWith(File, StringComparison.OrdinalIgnoreCase)
            && violation.Content.Contains(Locator, StringComparison.Ordinal);
    }

    [Fact]
    public void R1_ColorsMustComeFromThemeBrushTokens()
    {
        List<Violation> violations = [];
        CollectR1(violations);
        AssertClean(violations);
    }

    [Fact]
    public void R2_FrontierPagesMustNotUseTemplateSelectors()
    {
        List<Violation> violations = [];
        CollectSimpleLineRule(violations, "R2", EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.axaml"), line => line.Contains("/template/", StringComparison.Ordinal));
        AssertClean(violations);
    }

    [Fact]
    public void R3_FrontierPagesAndSharedControlsMustNotReferenceFATabView()
    {
        List<Violation> violations = [];
        CollectSimpleLineRule(
            violations,
            "R3",
            EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.axaml")
                .Concat(EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.cs"))
                .Concat(EnumerateFiles("Controls", "*.axaml"))
                .Concat(EnumerateFiles("Controls", "*.cs")),
            line => Regex.IsMatch(line, @"\bFATabView(Item)?\b"));
        AssertClean(violations);
    }

    [Fact]
    public void R4_CornerRadiusBorderThicknessFontSizeMustUseTokens()
    {
        List<Violation> violations = [];
        CollectR4(violations);
        AssertClean(violations);
    }

    [Fact]
    public void R5_SpacingValuesMustStayOnScale()
    {
        List<Violation> violations = [];
        CollectR5(violations);
        AssertClean(violations);
    }

    [Fact]
    public void R6_LayoutContainersMustNotUseFixedDimensions()
    {
        List<Violation> violations = [];
        CollectR6(violations);
        AssertClean(violations);
    }

    [Fact]
    public void R7_PageCodeBehindMustNotBuildAdHocMessageWindows()
    {
        List<Violation> violations = [];
        CollectSimpleLineRule(violations, "R7", EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.cs"), line => Regex.IsMatch(line, @"\bnew Window\b"));
        AssertClean(violations);
    }

    [Fact]
    public void Whitelist_EntriesMustAllStayInUse()
    {
        IReadOnlyList<WhitelistEntry> whitelist = LoadWhitelist();
        if (whitelist.Count == 0)
        {
            return;
        }

        List<Violation> everything = CollectAllRawViolations();
        string[] unused = whitelist
            .Where(entry => !everything.Any(entry.Matches))
            .Select(entry => $"{entry.Rule} | {entry.File} | {entry.Locator}")
            .ToArray();
        Assert.True(unused.Length == 0, "白名单存在失效条目(对应代码已修复,请删除):\n" + string.Join('\n', unused));
    }

    private static void AssertClean(List<Violation> violations)
    {
        IReadOnlyList<WhitelistEntry> whitelist = LoadWhitelist();
        Violation[] active = violations
            .Where(violation => !whitelist.Any(entry => entry.Matches(violation)))
            .ToArray();
        Assert.True(
            active.Length == 0,
            $"设计体系审计发现 {active.Length} 处违规:\n" + string.Join('\n', active.Select(v => v.ToString())));
    }

    private List<Violation> CollectAllRawViolations()
    {
        List<Violation> all = [];
        void Collect(Action<List<Violation>> rule)
        {
            List<Violation> bucket = [];
            rule(bucket);
            all.AddRange(bucket);
        }

        // 与各规则同一实现路径:直接复跑收集(白名单匹配用)。
        Collect(bucket => CollectR1(bucket));
        Collect(bucket => CollectSimpleLineRule(bucket, "R2", EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.axaml"), line => line.Contains("/template/", StringComparison.Ordinal)));
        Collect(bucket => CollectSimpleLineRule(
            bucket,
            "R3",
            EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.axaml")
                .Concat(EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.cs"))
                .Concat(EnumerateFiles("Controls", "*.axaml"))
                .Concat(EnumerateFiles("Controls", "*.cs")),
            line => Regex.IsMatch(line, @"\bFATabView(Item)?\b")));
        Collect(bucket => CollectR4(bucket));
        Collect(bucket => CollectR5(bucket));
        Collect(bucket => CollectR6(bucket));
        Collect(bucket => CollectSimpleLineRule(bucket, "R7", EnumerateFiles(Path.Combine("Views", "FrontierPages"), "*.cs"), line => Regex.IsMatch(line, @"\bnew Window\b")));
        return all;
    }

    private void CollectR1(List<Violation> violations)
    {
        foreach ((string file, XDocument document) in LoadValueScopeAxaml())
        {
            foreach (XElement element in document.Descendants())
            {
                if (element.Name.LocalName == "SolidColorBrush"
                    && element.Attribute("Color") is { } brushColor
                    && brushColor.Value.StartsWith('#'))
                {
                    violations.Add(new Violation("R1", file, LineOf(element), element.ToString()));
                    continue;
                }

                foreach (XAttribute attribute in element.Attributes())
                {
                    if (ColorAttributeNames.Contains(attribute.Name.LocalName, StringComparer.Ordinal)
                        && attribute.Value.StartsWith('#'))
                    {
                        violations.Add(new Violation("R1", file, LineOf(element), $"{element.Name.LocalName} {attribute.Name.LocalName}=\"{attribute.Value}\""));
                    }
                }

                if (ReadSetter(element) is { } setter
                    && ColorAttributeNames.Contains(setter.Property, StringComparer.Ordinal)
                    && setter.Value.StartsWith('#'))
                {
                    violations.Add(new Violation("R1", file, LineOf(element), $"Setter Property=\"{setter.Property}\" Value=\"{setter.Value}\""));
                }
            }
        }

        foreach ((string file, string[] lines) in LoadValueScopeCodeBehind())
        {
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (line.Contains("Color.Parse(\"#", StringComparison.Ordinal)
                    || line.Contains("Color.FromArgb(", StringComparison.Ordinal)
                    || line.Contains("Color.FromRgb(", StringComparison.Ordinal)
                    || line.Contains("Color.FromUInt32(", StringComparison.Ordinal)
                    || Regex.IsMatch(line, @"\bColors\.[A-Z]"))
                {
                    violations.Add(new Violation("R1", file, index + 1, line));
                }
            }
        }
    }

    private void CollectR4(List<Violation> violations)
    {
        foreach ((string file, XDocument document) in LoadValueScopeAxaml())
        {
            foreach (XElement element in document.Descendants())
            {
                if (ReadSetter(element) is { } setter
                    && setter.Property is "CornerRadius" or "BorderThickness" or "FontSize"
                    && !IsResourceReference(setter.Value)
                    && !IsAllZero(setter.Value)
                    && ParsesAsNumbers(setter.Value))
                {
                    violations.Add(new Violation("R4", file, LineOf(element), $"Setter Property=\"{setter.Property}\" Value=\"{setter.Value}\""));
                }

                foreach (XAttribute attribute in element.Attributes())
                {
                    string name = attribute.Name.LocalName;
                    if (name is not ("CornerRadius" or "BorderThickness" or "FontSize"))
                    {
                        continue;
                    }

                    if (IsResourceReference(attribute.Value) || IsAllZero(attribute.Value))
                    {
                        continue;
                    }

                    if (ParsesAsNumbers(attribute.Value))
                    {
                        violations.Add(new Violation("R4", file, LineOf(element), $"{element.Name.LocalName} {name}=\"{attribute.Value}\""));
                    }
                }
            }
        }
    }

    private void CollectR5(List<Violation> violations)
    {
        foreach ((string file, XDocument document) in LoadValueScopeAxaml())
        {
            foreach (XElement element in document.Descendants())
            {
                if (ReadSetter(element) is { } setter
                    && setter.Property is "Spacing" or "Margin" or "Padding" or "RowSpacing" or "ColumnSpacing"
                    && !IsResourceReference(setter.Value)
                    && ParsesAsNumbers(setter.Value)
                    && ParseNumbers(setter.Value).Any(value => !SpacingScale.Contains(Math.Abs(value))))
                {
                    violations.Add(new Violation("R5", file, LineOf(element), $"Setter Property=\"{setter.Property}\" Value=\"{setter.Value}\""));
                }

                foreach (XAttribute attribute in element.Attributes())
                {
                    string name = attribute.Name.LocalName;
                    if (name is not ("Spacing" or "Margin" or "Padding" or "RowSpacing" or "ColumnSpacing"))
                    {
                        continue;
                    }

                    if (IsResourceReference(attribute.Value) || !ParsesAsNumbers(attribute.Value))
                    {
                        continue;
                    }

                    double[] numbers = ParseNumbers(attribute.Value);
                    if (numbers.Any(value => !SpacingScale.Contains(Math.Abs(value))))
                    {
                        violations.Add(new Violation("R5", file, LineOf(element), $"{element.Name.LocalName} {name}=\"{attribute.Value}\""));
                    }
                }
            }
        }
    }

    private void CollectR6(List<Violation> violations)
    {
        foreach ((string file, XDocument document) in LoadValueScopeAxaml())
        {
            foreach (XElement element in document.Descendants())
            {
                if (ReadSetter(element) is { } setter
                    && setter.Property is "Width" or "Height"
                    && double.TryParse(setter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double fixedSize)
                    && fixedSize > 48
                    && GetStyledElementName(element) is { } styledElement
                    && ContainerElementNames.Contains(styledElement, StringComparer.Ordinal))
                {
                    violations.Add(new Violation("R6", file, LineOf(element), $"{styledElement} Setter {setter.Property}=\"{setter.Value}\""));
                }

                if (!ContainerElementNames.Contains(element.Name.LocalName, StringComparer.Ordinal))
                {
                    continue;
                }

                double? width = ReadDimension(element, "Width");
                double? height = ReadDimension(element, "Height");
                if (width is null && height is null)
                {
                    continue;
                }

                if ((width ?? 0) <= 48 && (height ?? 0) <= 48)
                {
                    continue;
                }

                violations.Add(new Violation("R6", file, LineOf(element), $"{element.Name.LocalName} Width=\"{width}\" Height=\"{height}\""));
            }
        }
    }

    private static void CollectSimpleLineRule(List<Violation> violations, string rule, IEnumerable<string> files, Func<string, bool> predicate)
    {
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                if (predicate(lines[index]))
                {
                    violations.Add(new Violation(rule, file, index + 1, lines[index]));
                }
            }
        }
    }

    // 取值规则(R1/R4/R5/R6)的扫描范围:Views/**(shell + 前卫页面树)+ Controls/**。
    // Theme/ 是令牌定义地、Overlay/ 属并行变更,均不在取值规则范围内。
    private static IEnumerable<(string File, XDocument Document)> LoadValueScopeAxaml()
    {
        foreach (string file in EnumerateFiles("Views", "*.axaml").Concat(EnumerateFiles("Controls", "*.axaml")))
        {
            yield return (file, XDocument.Load(file, LoadOptions.SetLineInfo));
        }
    }

    private static IEnumerable<(string File, string[] Lines)> LoadValueScopeCodeBehind()
    {
        foreach (string file in EnumerateFiles("Views", "*.cs").Concat(EnumerateFiles("Controls", "*.cs")))
        {
            yield return (file, File.ReadAllLines(file));
        }
    }

    private static IEnumerable<string> EnumerateFiles(string relativeRoot, string pattern)
    {
        string root = Path.Combine(FindGuiRoot(), relativeRoot);
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            : [];
    }

    private static bool IsResourceReference(string value) =>
        value.StartsWith('{') || value.Contains("Resource", StringComparison.Ordinal);

    private static bool IsAllZero(string value) =>
        ParsesAsNumbers(value) && ParseNumbers(value).All(number => number == 0);

    private static bool ParsesAsNumbers(string value)
    {
        string[] parts = value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 && parts.All(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
    }

    private static double[] ParseNumbers(string value) =>
        value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => double.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

    private static double? ReadDimension(XElement element, string name) =>
        element.Attribute(name) is { } attribute
        && double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;

    private static (string Property, string Value)? ReadSetter(XElement element) =>
        element.Name.LocalName == "Setter"
        && element.Attribute("Property") is { } property
        && element.Attribute("Value") is { } value
            ? (property.Value, value.Value)
            : null;

    private static string? GetStyledElementName(XElement setter)
    {
        string? selector = setter.Ancestors().FirstOrDefault(element => element.Name.LocalName == "Style")?.Attribute("Selector")?.Value;
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        Match match = Regex.Match(selector, @"(?:^|\s|>)(?:[A-Za-z_][\w-]*\|)?(?<type>[A-Za-z_][\w-]*)");
        return match.Success ? match.Groups["type"].Value : null;
    }

    private static int LineOf(XElement element) =>
        element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;

    private static IReadOnlyList<WhitelistEntry> LoadWhitelist()
    {
        string path = Path.Combine(FindGuiRoot(), "design-system-whitelist.md");
        if (!File.Exists(path))
        {
            return [];
        }

        List<WhitelistEntry> entries = [];
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('|'))
            {
                continue;
            }

            string[] parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length >= 4 && parts[0].StartsWith('R'))
            {
                Assert.False(string.IsNullOrWhiteSpace(parts[3]), $"白名单条目缺理由: {line}");
                entries.Add(new WhitelistEntry(parts[0], parts[1].Replace('\\', '/'), parts[2], parts[3]));
            }
        }

        return entries;
    }

    private static string FindGuiRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "zzzod-dotnet", "src", "ZzzOd.Gui");
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        throw new DirectoryNotFoundException("未找到 ZzzOd.Gui 源码目录。");
    }
}

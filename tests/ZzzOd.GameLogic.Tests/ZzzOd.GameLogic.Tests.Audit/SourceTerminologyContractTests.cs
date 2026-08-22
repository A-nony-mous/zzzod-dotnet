using System.Text.RegularExpressions;
using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

public sealed class SourceTerminologyContractTests
{
    private static readonly Regex ForbiddenTerm = new(
        @"(?i)(\bpython\b|\bbettergi\b|\bbgi\b|[A-Za-z0-9_./\\-]+\.py\b|参考实现|上游实现|迁移来源)",
        RegexOptions.CultureInvariant);

    [Fact]
    public void SourceComments_DoNotContainMigrationProvenance()
    {
        string repositoryRoot = FindRepositoryRoot();
        List<string> violations = [];
        foreach (string path in Directory.GetFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            violations.AddRange(FindViolations(File.ReadAllLines(path), Path.GetRelativePath(repositoryRoot, path)));
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Scanner_IgnoresRuntimeIdentifiersAndChecksCommentForms()
    {
        string[] source =
        [
            "string identifier = \"Python\";",
            "string path = \"module.py\";",
            "// neutral comment",
            "/* block",
            " * BGI provenance",
            " */",
            "/// Python provenance",
        ];

        string[] violations = FindViolations(source, "fixture.cs").ToArray();

        Assert.Equal(2, violations.Length);
        Assert.Contains("BGI", violations[0], StringComparison.Ordinal);
        Assert.Contains("Python", violations[1], StringComparison.Ordinal);
    }

    private static IEnumerable<string> FindViolations(IReadOnlyList<string> lines, string relativePath)
    {
        bool inBlockComment = false;
        for (int index = 0; index < lines.Count; index++)
        {
            string trimmed = lines[index].TrimStart();
            bool isComment = inBlockComment || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal);
            if (!isComment)
            {
                continue;
            }

            Match match = ForbiddenTerm.Match(trimmed);
            if (match.Success)
            {
                yield return $"{relativePath}:{index + 1}: {match.Value}";
            }

            if (!inBlockComment && trimmed.StartsWith("/*", StringComparison.Ordinal) && !trimmed.Contains("*/", StringComparison.Ordinal))
            {
                inBlockComment = true;
            }
            else if (inBlockComment && trimmed.Contains("*/", StringComparison.Ordinal))
            {
                inBlockComment = false;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "ZzzOd.GameLogic")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "ZzzOd.GameLogic.Tests")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("未找到 zzzod-dotnet 仓库根目录。");
    }
}

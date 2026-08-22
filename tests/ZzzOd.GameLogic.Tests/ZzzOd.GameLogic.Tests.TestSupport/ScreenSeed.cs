using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ZzzOd.GameLogic.Tests.TestSupport;

/// <summary>
/// 测试画面种子工具：把 merged 风格的画面列表 YAML 拆写为每画面一个独立文件，
/// 与生产 ScreenContext「只枚举独立 *.yml」的运行合同保持一致。
/// </summary>
public static class ScreenSeed
{
    private static readonly Regex ScreenIdLine = new("^screen_id:\\s*(?<id>\\S+)", RegexOptions.Multiline);

    /// <summary>
    /// 将 merged 风格列表 YAML（顶层为 "- screen_id: ..." 条目）拆分为独立画面文件。
    /// </summary>
    public static void WriteScreens(string screenInfoDirectory, string mergedStyleYaml)
    {
        Directory.CreateDirectory(screenInfoDirectory);
        List<string> documents = [];
        StringBuilder? current = null;
        foreach (string line in mergedStyleYaml.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("- ", System.StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    documents.Add(current.ToString());
                }
                current = new StringBuilder(line[2..]);
            }
            else if (current is not null)
            {
                string contentLine = line.TrimEnd();
                current.Append('\n');
                // 条目内容行统一去掉列表项的 2 空格缩进，保持映射内部相对层级不变。
                current.Append(contentLine.Length >= 2 && contentLine[0] == ' ' && contentLine[1] == ' '
                    ? contentLine[2..]
                    : contentLine);
            }
        }
        if (current is not null)
        {
            documents.Add(current.ToString());
        }

        foreach (string document in documents)
        {
            Match match = ScreenIdLine.Match(document);
            if (!match.Success)
            {
                throw new System.InvalidOperationException("画面种子缺少 screen_id。");
            }
            File.WriteAllText(
                Path.Combine(screenInfoDirectory, match.Groups["id"].Value + ".yml"),
                document.TrimEnd() + "\n");
        }
    }
}

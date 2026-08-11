using OneDragon.Core.Runtime;
using OneDragon.Core.Screen;
using ZzzOd.GameLogic.AutoBattle;

namespace ZzzOd.GameLogic.Config;

/// <summary>
/// 校验运行根中的独立画面和自动战斗配置。
/// </summary>
public sealed class ZzzGameConfigValidator
{
    /// <summary>
    /// 校验独立画面索引和自动战斗引用闭包。
    /// </summary>
    /// <param name="environment">待校验的运行环境。</param>
    /// <param name="validateScreenInfo">是否校验画面索引。</param>
    /// <param name="autoBattleTemplateNames">需要校验的自动战斗主策略名称。</param>
    /// <returns>全部配置校验问题。</returns>
    public IReadOnlyList<ZzzGameConfigValidationIssue> Validate(
        OneDragonEnvironment environment,
        bool validateScreenInfo,
        IEnumerable<string> autoBattleTemplateNames)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(autoBattleTemplateNames);
        List<ZzzGameConfigValidationIssue> issues = [];
        if (validateScreenInfo)
        {
            try
            {
                new ScreenContext(environment).Reload();
            }
            catch (Exception exception)
            {
                issues.Add(new ZzzGameConfigValidationIssue(
                    "assets/game_data/screen_info",
                    $"独立画面配置校验失败: {exception.Message}"));
            }
        }

        foreach (string templateName in autoBattleTemplateNames
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            try
            {
                new AutoBattleReferenceGraphLoader(environment).Load("auto_battle", templateName);
            }
            catch (Exception exception)
            {
                issues.Add(new ZzzGameConfigValidationIssue(
                    $"config/auto_battle/{templateName}.yml",
                    $"自动战斗独立引用图校验失败: {exception.Message}"));
            }
        }

        return issues;
    }
}

/// <summary>
/// 独立游戏配置校验问题。
/// </summary>
/// <param name="Path">配置相对路径。</param>
/// <param name="Message">中文问题说明。</param>
public sealed record ZzzGameConfigValidationIssue(string Path, string Message);

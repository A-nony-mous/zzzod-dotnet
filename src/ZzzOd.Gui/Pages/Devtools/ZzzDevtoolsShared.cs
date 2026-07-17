using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OpenCvSharp;
using ZzzOd.AppHost.Backend;
using GeometryPoint = OneDragon.Core.Abstractions.Geometry.Point;

namespace ZzzOd.Gui.Pages.Devtools;

internal static class ZzzDevtoolsImageLoader
{
    public static bool TryLoadBitmap(Image image, byte[] bytes)
    {
        try
        {
            image.Source = new Bitmap(new MemoryStream(bytes));
            return true;
        }
        catch (Exception)
        {
            image.Source = null;
            return false;
        }
    }

    public static async Task<string?> PickLocalFileAsync(Control owner, string title, params string[] patterns)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(title)
                {
                    Patterns = patterns.Length == 0 ? ["*.*"] : patterns,
                },
            ],
        }).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}

internal sealed record ZzzAgentTemplateCardState(string Name, string SubDir, string TemplatePattern, string TemplateRef)
{
    public byte[]? ScreenBytes { get; set; }

    public byte[]? PreviewBytes { get; set; }

    public bool Saved { get; set; }

    public string ResolveTemplateId(string agentId) => TemplatePattern.Replace("{agent_id}", agentId, StringComparison.Ordinal);
}

internal sealed class ZzzAgentTemplateGeneratorState
{
    private static readonly Regex AgentIdPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private readonly IZzzAppBackend _backend;
    private readonly OneDragonEnvironment? _environment;

    public ZzzAgentTemplateGeneratorState(IZzzAppBackend backend)
    {
        _backend = backend;
        ZzzBackendResult<ZzzHealthDto> health = backend.GetHealth();
        if (health.Success && health.Value is not null && !string.IsNullOrWhiteSpace(health.Value.RunRoot))
        {
            _environment = new OneDragonEnvironment(Path.GetFullPath(health.Value.RunRoot));
        }

        Cards =
        [
            new("1号位大头像", "battle", "avatar_1_{agent_id}", "avatar_1_template"),
            new("2号位小头像", "battle", "avatar_2_{agent_id}", "avatar_2_template"),
            new("连携头像", "battle", "avatar_chain_{agent_id}", "avatar_chain_template"),
            new("快速支援头像", "battle", "avatar_quick_{agent_id}", "avatar_quick_template"),
            new("零号空洞头像", "hollow", "avatar_{agent_id}", "avatar_hollow_template"),
            new("组队预设头像", "predefined_team", "avatar_{agent_id}", "avatar_template_team"),
        ];
    }

    public IReadOnlyList<ZzzAgentTemplateCardState> Cards { get; }

    public string? AgentId { get; private set; }

    public string? LastSavedPath { get; private set; }

    public string LastStatusText { get; private set; } = string.Empty;

    public bool SetAgentId(string value)
    {
        string agentId = value.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            AgentId = null;
            LastStatusText = string.Empty;
            ResetCards();
            return false;
        }

        if (!AgentIdPattern.IsMatch(agentId))
        {
            AgentId = null;
            LastStatusText = "输入格式不正确";
            ResetCards();
            return false;
        }

        bool changed = !string.Equals(AgentId, agentId, StringComparison.Ordinal);
        AgentId = agentId;
        LastStatusText = string.Empty;
        if (changed)
        {
            ResetCards();
        }

        return true;
    }

    public async Task ChooseScreenshotWithPickerAsync(Control owner, int index)
    {
        string? path = await ZzzDevtoolsImageLoader.PickLocalFileAsync(owner, "选择截图", "*.png", "*.jpg", "*.jpeg", "*.bmp").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            ChooseScreenshot(index, path);
        }
    }

    public void ChooseScreenshot(int index, string filePath)
    {
        if (!IsValidCard(index))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            LastStatusText = $"截图不存在：{filePath}";
            return;
        }

        PreviewAndUpdate(index, File.ReadAllBytes(filePath));
    }

    public void CaptureGameScreenshot(int index)
    {
        if (!IsValidCard(index))
        {
            return;
        }

        ZzzBackendResult<ZzzScreenshotDto> screenshot = _backend.GetScreenshot();
        if (!screenshot.Success || screenshot.Value is null)
        {
            LastStatusText = screenshot.Error ?? "游戏截图不可用。";
            return;
        }

        PreviewAndUpdate(index, screenshot.Value.Bytes);
    }

    public string? SaveTemplate(int index)
    {
        if (!IsValidCard(index) || AgentId is null)
        {
            LastStatusText = "请先输入代理人 ID 并选择模板。";
            return null;
        }

        if (_environment is null)
        {
            LastStatusText = "运行根目录不可用";
            return null;
        }

        ZzzAgentTemplateCardState card = Cards[index];
        if (card.ScreenBytes is null)
        {
            LastStatusText = "请先选择截图或游戏截图";
            return null;
        }

        string templateId = card.ResolveTemplateId(AgentId);
        try
        {
            using Mat screen = Cv2.ImDecode(card.ScreenBytes, ImreadModes.Color);
            using TemplateInfo reference = new(_environment, "template", card.TemplateRef);
            if (screen.Empty() || reference.PointList.Count == 0)
            {
                LastStatusText = "模板保存失败";
                return null;
            }

            using TemplateInfo template = new(_environment, card.SubDir, templateId)
            {
                ScreenImage = screen.Clone(),
                TemplateName = card.Name,
                TemplateShape = reference.TemplateShape,
                AutoMask = reference.AutoMask,
            };
            template.SetPointList(reference.PointList.Select(point => new GeometryPoint(point.X, point.Y)));
            template.SaveConfig();
            template.SaveRaw();
            template.SaveMask();
            if (!File.Exists(template.RawPath))
            {
                LastStatusText = "模板保存失败";
                return null;
            }

            LastSavedPath = template.RawPath;
            card.Saved = true;
            LastStatusText = string.Empty;
            return LastSavedPath;
        }
        catch (Exception)
        {
            LastStatusText = "模板保存失败";
            return null;
        }
    }

    public int GenerateAll()
    {
        if (AgentId is null)
        {
            LastStatusText = "请先输入代理人 ID?";
            return 0;
        }

        int saved = 0;
        for (int i = 0; i < Cards.Count; i++)
        {
            if (Cards[i].ScreenBytes is null)
            {
                ZzzBackendResult<ZzzScreenshotDto> screenshot = _backend.GetScreenshot();
                if (!screenshot.Success || screenshot.Value is null)
                {
                    continue;
                }

                PreviewAndUpdate(i, screenshot.Value.Bytes);
            }

            if (SaveTemplate(i) is not null)
            {
                saved++;
            }
        }

        LastStatusText = saved == Cards.Count
            ? "全部模板已生成"
            : $"失败模板: {string.Join(", ", Cards.Where(card => !card.Saved).Select(card => card.Name))}";
        return saved;
    }

    private void PreviewAndUpdate(int index, byte[] bytes)
    {
        if (_environment is null)
        {
            LastStatusText = "运行根目录不可用";
            return;
        }

        ZzzAgentTemplateCardState card = Cards[index];
        try
        {
            using Mat screen = Cv2.ImDecode(bytes, ImreadModes.Color);
            using TemplateInfo reference = new(_environment, "template", card.TemplateRef)
            {
                ScreenImage = screen.Clone(),
            };
            using Mat? preview = reference.GetTemplateRawByScreenPoint();
            if (screen.Empty() || preview is null || preview.Empty())
            {
                LastStatusText = "裁剪失败，请检查模板配置";
                return;
            }

            Cv2.ImEncode(".png", preview, out byte[] previewBytes);
            card.ScreenBytes = bytes.ToArray();
            card.PreviewBytes = previewBytes;
            card.Saved = false;
            LastStatusText = string.Empty;
        }
        catch (Exception)
        {
            LastStatusText = "截图读取失败";
        }
    }

    private void ResetCards()
    {
        foreach (ZzzAgentTemplateCardState card in Cards)
        {
            card.ScreenBytes = null;
            card.PreviewBytes = null;
            card.Saved = false;
        }
    }

    private bool IsValidCard(int index) => index >= 0 && index < Cards.Count;
}

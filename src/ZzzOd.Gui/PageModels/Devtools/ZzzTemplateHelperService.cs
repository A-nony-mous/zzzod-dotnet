using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using FluentAvalonia.UI.Controls;
using OneDragon.Core.Runtime;
using OneDragon.Core.Template;
using OneDragon.Core.Utils;
using OpenCvSharp;
using ZzzOd.AppHost.Backend;
using ZzzOd.Gui.Shell;
using AvaloniaPoint = Avalonia.Point;
using GeometryPoint = OneDragon.Core.Abstractions.Geometry.Point;

namespace ZzzOd.Gui.PageModels.Devtools;
internal sealed record TemplatePointRow(string Text);

internal sealed class ZzzTemplateHelperService
{
    private readonly string _templateRoot;
    private readonly OneDragonEnvironment _environment;
    private readonly TemplateLoader _loader;

    public ZzzTemplateHelperService(string runRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRoot);
        _environment = new OneDragonEnvironment(runRoot);
        _loader = new TemplateLoader(_environment);
        _templateRoot = Path.GetFullPath(_environment.GetResourcePath("assets", "template"));
    }

    public IReadOnlyList<TemplateOption> GetTemplates() =>
        _loader.GetAllTemplateInfoFromDisk(needRaw: false, needConfig: true)
            .Select(template =>
            {
                TemplateOption option = new(
                    string.IsNullOrWhiteSpace(template.TemplateName) ? $"{template.SubDir}/{template.TemplateId}" : template.TemplateName,
                    template.SubDir,
                    template.TemplateId);
                template.Dispose();
                return option;
            })
            .ToArray();

    public TemplateInfo Load(string subDir, string templateId) =>
        new(_environment, subDir, templateId);

    public TemplateInfo Create(string subDir, string templateId) =>
        new(_environment, subDir, templateId);

    public TemplateInfo Copy(TemplateInfo source)
    {
        ValidateIdentity(source.SubDir, source.TemplateId);
        string targetId = NextCopyId(source.SubDir, source.TemplateId);
        string targetDirectory = ResolveDirectory(source.SubDir, targetId);
        bool copiedDirectory = Directory.Exists(source.DirectoryPath);
        if (copiedDirectory)
        {
            CopyDirectory(source.DirectoryPath, targetDirectory);
        }

        TemplateInfo copy = new(_environment, source.SubDir, targetId)
        {
            TemplateName = source.TemplateName,
            TemplateShape = source.TemplateShape,
            AutoMask = source.AutoMask,
        };
        copy.SetPointList(source.PointList);
        if (source.ScreenImage is not null)
        {
            copy.ScreenImage = source.ScreenImage.Clone();
        }

        return copy;
    }

    public void SetScreenImage(TemplateInfo template, byte[] encodedImage)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(encodedImage);
        Mat image = Cv2.ImDecode(encodedImage, ImreadModes.Color);
        if (image.Empty())
        {
            image.Dispose();
            throw new InvalidDataException("图片无法读取。");
        }

        template.ScreenImage?.Dispose();
        template.ScreenImage = image;
        template.SetPointList(template.PointList.ToArray());
    }

    public void SaveConfig(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        template.SaveConfig();
    }

    public void SaveRaw(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        using Mat? raw = template.GetTemplateRawByScreenPoint();
        if (raw is null || raw.Empty())
        {
            throw new InvalidOperationException("当前图片和点位无法生成模板原图。");
        }

        template.SaveRaw();
    }

    public void SaveMask(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        using Mat? mask = template.AutoMask ? template.GetTemplateMaskByScreenPoint() : null;
        if (mask is null || mask.Empty())
        {
            throw new InvalidOperationException("当前形状和点位无法生成模板掩码。");
        }

        template.SaveMask();
    }

    public bool Delete(TemplateInfo template)
    {
        ValidateIdentity(template.SubDir, template.TemplateId);
        string directory = ResolveDirectory(template.SubDir, template.TemplateId);
        if (!Directory.Exists(directory))
        {
            return false;
        }

        Directory.Delete(directory, recursive: true);
        _loader.ClearCache();
        return true;
    }

    public void ValidateIdentity(string subDir, string templateId)
    {
        ValidateSegment(subDir, "画面");
        ValidateSegment(templateId, "模板ID");
        _ = ResolveDirectory(subDir, templateId);
    }

    private string NextCopyId(string subDir, string templateId)
    {
        string candidate = templateId + "_copy";
        int suffix = 2;
        while (Directory.Exists(ResolveDirectory(subDir, candidate)))
        {
            candidate = templateId + $"_copy{suffix++}";
        }

        return candidate;
    }

    private string ResolveDirectory(string subDir, string templateId)
    {
        string directory = Path.GetFullPath(Path.Combine(_templateRoot, subDir, templateId));
        string rootPrefix = _templateRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("模板路径超出 assets/template? ");
        }

        return directory;
    }

    private static void ValidateSegment(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"{label}不是有效的目录名。");
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        }
    }
}

internal sealed record TemplateOption(string Label, string SubDir, string TemplateId)
{
    public override string ToString() => Label;
}

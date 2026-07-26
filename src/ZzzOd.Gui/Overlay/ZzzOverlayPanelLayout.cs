using ZzzOd.AppHost.Backend;

namespace ZzzOd.Gui.Overlay;

internal readonly record struct ZzzOverlayPhysicalRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

internal readonly record struct ZzzOverlayDisplayArea(
    string? DisplayName,
    ZzzOverlayPhysicalRect WorkingArea,
    double Scaling)
{
    public double EffectiveScaling => Math.Max(0.5d, Scaling);
}

internal static class ZzzOverlayPanelLayout
{
    private const double Margin = 4d;

    /// <summary>
    /// 出厂停靠布局与游戏客户区之间的逻辑边距。
    /// </summary>
    private const double DockMargin = 16d;

    /// <summary>
    /// 右列面板的出厂停靠顺序。log 面板停靠左上，不参与右列累计。
    /// </summary>
    private static readonly string[] RightColumnDockOrder = ["state", "battle", "decision", "timeline", "performance"];

    /// <summary>
    /// 解析锁定模式面板的物理矩形。
    /// </summary>
    /// <param name="panel">面板配置。</param>
    /// <param name="gameWindow">游戏窗口状态。</param>
    /// <param name="dockPanels">用于计算出厂停靠位的面板集合，通常直接传入全部面板配置；为空时视为没有前序面板。</param>
    /// <returns>面板物理矩形。</returns>
    public static ZzzOverlayPhysicalRect ResolveLocked(
        ZzzOverlayPanelSettings panel,
        ZzzWindowStatusDto gameWindow,
        IReadOnlyList<ZzzOverlayPanelSettings>? dockPanels = null)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(gameWindow);
        ZzzOverlayPhysicalRect game = GameBounds(gameWindow);
        double legacyScaling = ResolveLegacyScaling(panel, gameWindow);
        ZzzOverlayPhysicalRect candidate = HasNormalizedBounds(panel)
            ? new ZzzOverlayPhysicalRect(
                game.X + game.Width * panel.LockedX!.Value,
                game.Y + game.Height * panel.LockedY!.Value,
                game.Width * panel.LockedWidth!.Value,
                game.Height * panel.LockedHeight!.Value)
            : IsFactoryGeometry(panel)
                ? ResolveDefaultDock(panel, game, legacyScaling, dockPanels)
                : panel.LayoutVersion < 2
                    ? ScaleLegacyBounds(panel, legacyScaling)
                    : new ZzzOverlayPhysicalRect(panel.X, panel.Y, panel.Width, panel.Height);
        return ClampToGame(candidate, game);
    }

    public static ZzzOverlayPhysicalRect ResolveFree(ZzzOverlayPanelSettings panel, double desktopScaling)
    {
        ArgumentNullException.ThrowIfNull(panel);
        double scale = Math.Max(0.5d, desktopScaling);
        if (panel.FreeX.HasValue && panel.FreeY.HasValue && panel.FreeWidth.HasValue && panel.FreeHeight.HasValue)
        {
            double sourceScaling = panel.LayoutVersion < 2
                ? Math.Max(0.5d, panel.FreeDpi / 96d)
                : 1d;
            return new ZzzOverlayPhysicalRect(
                panel.FreeX.Value / sourceScaling * scale,
                panel.FreeY.Value / sourceScaling * scale,
                panel.FreeWidth.Value / sourceScaling * scale,
                panel.FreeHeight.Value / sourceScaling * scale);
        }

        return new ZzzOverlayPhysicalRect(panel.X, panel.Y, panel.Width, panel.Height);
    }

    public static ZzzOverlayPhysicalRect ResolveFree(ZzzOverlayPanelSettings panel, ZzzOverlayDisplayArea displayArea)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (HasV3FreeLayout(panel))
        {
            double scale = displayArea.EffectiveScaling;
            return new ZzzOverlayPhysicalRect(
                displayArea.WorkingArea.X + panel.FreeX.Value * scale,
                displayArea.WorkingArea.Y + panel.FreeY.Value * scale,
                panel.FreeWidth.Value * scale,
                panel.FreeHeight.Value * scale);
        }

        return ResolveFree(panel, displayArea.EffectiveScaling);
    }

    /// <summary>
    /// v3 自由布局以显示器工作区为原点保存逻辑坐标。显示器名称在部分平台可能为空，完整工作区锚点才是可恢复性的依据。
    /// </summary>
    public static bool HasV3FreeLayout(ZzzOverlayPanelSettings panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return panel.LayoutVersion >= 3 &&
            HasFreeBounds(panel) &&
            TryGetFreeWorkAreaAnchor(panel, out _);
    }

    /// <summary>
    /// 判断锁定模式是否已经具备 v3 的游戏客户区归一化布局。
    /// </summary>
    public static bool HasV3LockedLayout(ZzzOverlayPanelSettings panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return panel.LayoutVersion >= 3 && HasNormalizedBounds(panel);
    }

    public static bool NeedsLayoutMigration(ZzzOverlayPanelSettings panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return panel.IsFreeMode
            ? !HasV3FreeLayout(panel)
            : !HasV3LockedLayout(panel);
    }

    public static bool TryGetFreeWorkAreaAnchor(
        ZzzOverlayPanelSettings panel,
        out ZzzOverlayPhysicalRect workingArea)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (panel.FreeWorkAreaX is not double x ||
            panel.FreeWorkAreaY is not double y ||
            panel.FreeWorkAreaWidth is not double width ||
            panel.FreeWorkAreaHeight is not double height ||
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(width) ||
            !double.IsFinite(height) ||
            width <= 0d ||
            height <= 0d)
        {
            workingArea = default;
            return false;
        }

        workingArea = new ZzzOverlayPhysicalRect(x, y, width, height);
        return true;
    }

    public static bool MatchesFreeWorkAreaAnchor(
        ZzzOverlayPanelSettings panel,
        ZzzOverlayPhysicalRect workingArea)
    {
        if (!TryGetFreeWorkAreaAnchor(panel, out ZzzOverlayPhysicalRect saved))
        {
            return false;
        }

        const double tolerance = 0.5d;
        return Math.Abs(saved.X - workingArea.X) <= tolerance &&
            Math.Abs(saved.Y - workingArea.Y) <= tolerance &&
            Math.Abs(saved.Width - workingArea.Width) <= tolerance &&
            Math.Abs(saved.Height - workingArea.Height) <= tolerance;
    }

    /// <summary>
    /// 将旧自由布局恢复为其保存时的物理位置，供迁移为显示器锚定的 v3 格式使用。
    /// </summary>
    public static ZzzOverlayPhysicalRect ResolveLegacyFreeForMigration(
        ZzzOverlayPanelSettings panel,
        double fallbackScaling)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (panel.LayoutVersion == 2 && HasFreeBounds(panel))
        {
            double savedScaling = Math.Max(0.5d, panel.FreeDpi / 96d);
            return new ZzzOverlayPhysicalRect(
                panel.FreeX!.Value * savedScaling,
                panel.FreeY!.Value * savedScaling,
                panel.FreeWidth!.Value * savedScaling,
                panel.FreeHeight!.Value * savedScaling);
        }

        return ResolveFree(panel, fallbackScaling);
    }

    public static bool HasUsableGameBounds(ZzzWindowStatusDto gameWindow) =>
        gameWindow.IsWinValid &&
        !gameWindow.IsWinMinimized &&
        gameWindow.X.HasValue &&
        gameWindow.Y.HasValue &&
        gameWindow.Width is > 0 &&
        gameWindow.Height is > 0;

    /// <summary>
    /// 在已有有效客户区时，将一个面板的当前可解析位置转换成目标模式。
    /// </summary>
    public static void ConvertMode(
        ZzzOverlayPanelSettings panel,
        bool sourceIsFreeMode,
        bool targetIsFreeMode,
        ZzzWindowStatusDto gameWindow,
        double desktopScaling,
        uint displayDpi,
        IReadOnlyList<ZzzOverlayPanelSettings>? dockPanels = null)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(gameWindow);
        if (!HasUsableGameBounds(gameWindow))
        {
            throw new ArgumentException("游戏客户区不可用于 Overlay 面板布局转换。", nameof(gameWindow));
        }

        if (sourceIsFreeMode == targetIsFreeMode)
        {
            panel.IsFreeMode = targetIsFreeMode;
            panel.PendingSourceIsFreeMode = null;
            return;
        }

        ZzzOverlayPhysicalRect physicalBounds = sourceIsFreeMode
            ? ResolveFree(panel, desktopScaling)
            : ResolveLocked(panel, gameWindow, dockPanels);
        StoreMode(panel, targetIsFreeMode, physicalBounds, gameWindow, desktopScaling, displayDpi);
    }

    public static void StoreMode(
        ZzzOverlayPanelSettings panel,
        bool isFreeMode,
        ZzzOverlayPhysicalRect physicalBounds,
        ZzzWindowStatusDto gameWindow,
        double desktopScaling,
        uint displayDpi)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(gameWindow);
        if (!HasUsableGameBounds(gameWindow))
        {
            throw new ArgumentException("游戏客户区不可用于 Overlay 面板布局保存。", nameof(gameWindow));
        }

        if (isFreeMode)
        {
            StoreFree(panel, physicalBounds, desktopScaling, displayDpi);
        }
        else
        {
            StoreLocked(panel, physicalBounds, gameWindow);
        }

        panel.IsFreeMode = isFreeMode;
        panel.PendingSourceIsFreeMode = null;
    }

    public static void StoreMode(
        ZzzOverlayPanelSettings panel,
        bool isFreeMode,
        ZzzOverlayPhysicalRect physicalBounds,
        ZzzWindowStatusDto gameWindow,
        ZzzOverlayDisplayArea displayArea,
        uint displayDpi)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(gameWindow);
        if (!HasUsableGameBounds(gameWindow))
        {
            throw new ArgumentException("游戏客户区不可用于 Overlay 面板布局保存。", nameof(gameWindow));
        }

        if (isFreeMode)
        {
            StoreFree(panel, physicalBounds, displayArea, displayDpi);
        }
        else
        {
            StoreLocked(panel, physicalBounds, gameWindow);
        }

        panel.IsFreeMode = isFreeMode;
        panel.PendingSourceIsFreeMode = null;
    }

    public static ZzzOverlayPhysicalRect ClampToGame(ZzzOverlayPhysicalRect candidate, ZzzOverlayPhysicalRect game)
    {
        double width = Math.Min(Math.Max(120d, candidate.Width), Math.Max(120d, game.Width - Margin * 2d));
        double height = Math.Min(Math.Max(80d, candidate.Height), Math.Max(80d, game.Height - Margin * 2d));
        double minX = game.X + Margin;
        double minY = game.Y + Margin;
        double maxX = Math.Max(minX, game.Right - Margin - width);
        double maxY = Math.Max(minY, game.Bottom - Margin - height);
        return new ZzzOverlayPhysicalRect(
            Math.Clamp(candidate.X, minX, maxX),
            Math.Clamp(candidate.Y, minY, maxY),
            width,
            height);
    }

    public static ZzzOverlayPhysicalRect ClampToWorkArea(ZzzOverlayPhysicalRect candidate, ZzzOverlayPhysicalRect workingArea)
    {
        double width = Math.Min(Math.Max(120d, candidate.Width), Math.Max(120d, workingArea.Width));
        double height = Math.Min(Math.Max(80d, candidate.Height), Math.Max(80d, workingArea.Height));
        double maxX = Math.Max(workingArea.X, workingArea.Right - width);
        double maxY = Math.Max(workingArea.Y, workingArea.Bottom - height);
        return new ZzzOverlayPhysicalRect(
            Math.Clamp(candidate.X, workingArea.X, maxX),
            Math.Clamp(candidate.Y, workingArea.Y, maxY),
            width,
            height);
    }

    public static void StoreLocked(ZzzOverlayPanelSettings panel, ZzzOverlayPhysicalRect physicalBounds, ZzzWindowStatusDto gameWindow)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ZzzOverlayPhysicalRect game = GameBounds(gameWindow);
        ZzzOverlayPhysicalRect clamped = ClampToGame(physicalBounds, game);
        double scaling = ResolveGameScaling(gameWindow);
        panel.X = clamped.X / scaling;
        panel.Y = clamped.Y / scaling;
        panel.Width = clamped.Width / scaling;
        panel.Height = clamped.Height / scaling;
        panel.LockedX = (clamped.X - game.X) / game.Width;
        panel.LockedY = (clamped.Y - game.Y) / game.Height;
        panel.LockedWidth = clamped.Width / game.Width;
        panel.LockedHeight = clamped.Height / game.Height;
        panel.LayoutVersion = 3;
    }

    public static void StoreFree(ZzzOverlayPanelSettings panel, ZzzOverlayPhysicalRect physicalBounds, double desktopScaling, uint dpi)
    {
        ArgumentNullException.ThrowIfNull(panel);
        double scale = Math.Max(0.5d, desktopScaling);
        panel.X = physicalBounds.X / scale;
        panel.Y = physicalBounds.Y / scale;
        panel.Width = physicalBounds.Width / scale;
        panel.Height = physicalBounds.Height / scale;
        panel.FreeX = physicalBounds.X / scale;
        panel.FreeY = physicalBounds.Y / scale;
        panel.FreeWidth = physicalBounds.Width / scale;
        panel.FreeHeight = physicalBounds.Height / scale;
        panel.FreeDpi = Math.Max(1u, dpi);
        panel.FreeDisplayName = null;
        panel.FreeWorkAreaX = null;
        panel.FreeWorkAreaY = null;
        panel.FreeWorkAreaWidth = null;
        panel.FreeWorkAreaHeight = null;
        panel.LayoutVersion = 2;
    }

    public static void StoreFree(
        ZzzOverlayPanelSettings panel,
        ZzzOverlayPhysicalRect physicalBounds,
        ZzzOverlayDisplayArea displayArea,
        uint dpi)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ZzzOverlayPhysicalRect clamped = ClampToWorkArea(physicalBounds, displayArea.WorkingArea);
        double scale = displayArea.EffectiveScaling;
        panel.X = clamped.X / scale;
        panel.Y = clamped.Y / scale;
        panel.Width = clamped.Width / scale;
        panel.Height = clamped.Height / scale;
        panel.FreeX = (clamped.X - displayArea.WorkingArea.X) / scale;
        panel.FreeY = (clamped.Y - displayArea.WorkingArea.Y) / scale;
        panel.FreeWidth = clamped.Width / scale;
        panel.FreeHeight = clamped.Height / scale;
        panel.FreeDpi = Math.Max(1u, dpi);
        panel.FreeDisplayName = displayArea.DisplayName;
        panel.FreeWorkAreaX = displayArea.WorkingArea.X;
        panel.FreeWorkAreaY = displayArea.WorkingArea.Y;
        panel.FreeWorkAreaWidth = displayArea.WorkingArea.Width;
        panel.FreeWorkAreaHeight = displayArea.WorkingArea.Height;
        panel.LayoutVersion = 3;
    }

    private static ZzzOverlayPhysicalRect GameBounds(ZzzWindowStatusDto gameWindow) =>
        new(gameWindow.X ?? 0, gameWindow.Y ?? 0, Math.Max(1, gameWindow.Width ?? 1), Math.Max(1, gameWindow.Height ?? 1));

    private static double ResolveLegacyScaling(ZzzOverlayPanelSettings panel, ZzzWindowStatusDto gameWindow)
    {
        double sourceScaling = Math.Max(0.5d, panel.FreeDpi / 96d);
        return ResolveGameScaling(gameWindow) / sourceScaling;
    }

    private static double ResolveGameScaling(ZzzWindowStatusDto gameWindow) =>
        gameWindow.Dpi > 0
            ? Math.Max(0.5d, gameWindow.Dpi / 96d)
            : 1d;

    private static ZzzOverlayPhysicalRect ScaleLegacyBounds(ZzzOverlayPanelSettings panel, double scaling) =>
        new(panel.X * scaling, panel.Y * scaling, panel.Width * scaling, panel.Height * scaling);

    private static bool HasNormalizedBounds(ZzzOverlayPanelSettings panel) =>
        panel.LockedX.HasValue && panel.LockedY.HasValue && panel.LockedWidth.HasValue && panel.LockedHeight.HasValue &&
        double.IsFinite(panel.LockedX.Value) &&
        double.IsFinite(panel.LockedY.Value) &&
        double.IsFinite(panel.LockedWidth.Value) &&
        double.IsFinite(panel.LockedHeight.Value) &&
        panel.LockedWidth.Value > 0d && panel.LockedHeight.Value > 0d;

    private static bool HasFreeBounds(ZzzOverlayPanelSettings panel) =>
        panel.FreeX is double x &&
        panel.FreeY is double y &&
        panel.FreeWidth is double width &&
        panel.FreeHeight is double height &&
        double.IsFinite(x) &&
        double.IsFinite(y) &&
        double.IsFinite(width) &&
        double.IsFinite(height) &&
        width > 0d &&
        height > 0d;

    private static bool IsFactoryGeometry(ZzzOverlayPanelSettings panel) => panel.Id switch
    {
        "log" => panel.X == 100d && panel.Y == 100d && panel.Width == 480d && panel.Height == 200d,
        "state" or "battle" or "decision" or "timeline" or "performance" => panel.X == 0d && panel.Y == 0d,
        _ => false,
    };

    private static ZzzOverlayPhysicalRect ResolveDefaultDock(
        ZzzOverlayPanelSettings panel,
        ZzzOverlayPhysicalRect game,
        double scaling,
        IReadOnlyList<ZzzOverlayPanelSettings>? dockPanels)
    {
        double scale = Math.Max(0.5d, scaling);
        double gameWidth = Math.Max(100d, game.Width / scale);
        double gameHeight = Math.Max(100d, game.Height / scale);
        double width = Math.Min(Math.Max(180d, panel.Width), Math.Max(180d, gameWidth - DockMargin * 2d)) * scale;
        double height = ResolveDockHeight(panel, gameHeight, scale);
        double physicalMargin = DockMargin * scale;
        if (panel.Id == "log")
        {
            return new ZzzOverlayPhysicalRect(game.X + physicalMargin, game.Y + physicalMargin, width, height);
        }

        double x = game.Right - physicalMargin - width;
        double y = game.Y + physicalMargin + ResolvePrecedingDockOffset(panel, dockPanels, gameHeight, scale);
        if (y + height > game.Bottom - physicalMargin)
        {
            y = Math.Max(game.Y + physicalMargin, game.Bottom - physicalMargin - height);
        }

        return new ZzzOverlayPhysicalRect(x, y, width, height);
    }

    /// <summary>
    /// 计算右列面板的纵向累计偏移。
    /// 按"排在当前面板之前、已启用且仍使用出厂几何"的面板实际停靠高度累加，禁用面板不占位。
    /// 出厂默认高度各面板不等，用序号乘本面板高度的写法会确定性重叠。
    /// </summary>
    /// <param name="panel">当前面板。</param>
    /// <param name="dockPanels">参与计算的面板集合，可传入全部面板配置。</param>
    /// <param name="gameHeight">逻辑游戏高度。</param>
    /// <param name="scale">缩放系数。</param>
    /// <returns>纵向偏移的物理像素。</returns>
    private static double ResolvePrecedingDockOffset(
        ZzzOverlayPanelSettings panel,
        IReadOnlyList<ZzzOverlayPanelSettings>? dockPanels,
        double gameHeight,
        double scale)
    {
        int index = DockOrderIndex(panel.Id);
        if (index <= 0 || dockPanels is null || dockPanels.Count == 0)
        {
            return 0d;
        }

        double offset = 0d;
        foreach (ZzzOverlayPanelSettings candidate in dockPanels)
        {
            int candidateIndex = DockOrderIndex(candidate.Id);
            if (candidateIndex < 0 || candidateIndex >= index || !candidate.Enabled || candidate.IsFreeMode)
            {
                continue;
            }

            // 只有仍在出厂停靠位上的面板才占位；已保存自定义几何的面板不在这一列里。
            if (HasNormalizedBounds(candidate) || !IsFactoryGeometry(candidate))
            {
                continue;
            }

            offset += ResolveDockHeight(candidate, gameHeight, scale) + 8d * scale;
        }

        return offset;
    }

    private static double ResolveDockHeight(ZzzOverlayPanelSettings panel, double gameHeight, double scale) =>
        Math.Min(Math.Max(90d, panel.Height), Math.Max(90d, gameHeight - DockMargin * 2d)) * scale;

    private static int DockOrderIndex(string id) => Array.IndexOf(RightColumnDockOrder, id);
}

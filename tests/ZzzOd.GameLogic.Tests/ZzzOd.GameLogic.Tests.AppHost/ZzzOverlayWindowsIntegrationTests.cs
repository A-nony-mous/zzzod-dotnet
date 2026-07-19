using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using OpenCvSharp;
using Xunit;
using ZzzOd.AppHost.Backend;
using ZzzOd.AppHost.Overlay;
using ZzzOd.Gui.Overlay;
using ZzzOd.Gui.Pages.ApplicationSettings;
using ZzzOd.Gui.Views.FrontierPages.WorldPatrol;
using AvaloniaWindow = Avalonia.Controls.Window;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;

namespace ZzzOd.GameLogic.Tests.AppHost;

/// <summary>
/// 真实 Win32 客户区与 Avalonia Overlay 的桌面集成验证。
/// 仅在显式启用环境变量后创建桌面窗口，避免常规单元测试影响当前桌面。
/// </summary>
public sealed class ZzzOverlayWindowsIntegrationTests
{
    private const string EnabledEnvironmentVariable = "ZZZOD_RUN_WINDOWS_INTEGRATION";
    private const string EvidenceDirectoryEnvironmentVariable = "ZZZOD_WINDOWS_INTEGRATION_EVIDENCE_DIR";

    [WindowsIntegrationFact]
    [Trait("Category", "WindowsIntegration")]
    public void OverlayFollowsControlledWin32ClientAreaAndKeepsInProcessCaptureAvailable()
    {
        RunOnStaThread(() =>
        {
            EnsureFluentTheme();
            using ControlledWin32Window target = ControlledWin32Window.Create();
            target.Show();
            WindowsIntegrationEvidence? evidence = WindowsIntegrationEvidence.CreateFromEnvironment();
            bool restoreCursor = GetCursorPos(out NativePoint originalCursor);
            nint originalForeground = GetForegroundWindow();
            ZzzWindowStatusDto initial = target.Snapshot();
            Assert.True(target.Activate());
            Assert.True(WaitFor(() => target.IsForeground));
            initial = target.Snapshot();
            AvaloniaWindow owner = new()
            {
                Width = 8,
                Height = 8,
                Position = new PixelPoint(8, 8),
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowDecorations = WindowDecorations.None,
            };
            owner.Show();
            Assert.True(WaitFor(() => owner.IsVisible));
            ZzzOverlayGuiSettings settings = new()
            {
                ClickThrough = true,
                PreventCapture = false,
            };
            ZzzOverlayTechnicalWindow overlay = new();
            ZzzOverlayInfoPanelWindow panel = new();
            ZzzWorldPatrolLargeMapIconEditorWindow classicEditor = new([]);
            FrontierWorldPatrolLargeMapIconEditorWindow frontierEditor = new([]);
            Exception? testFailure = null;
            try
            {
                overlay.ApplySettings(settings);
                overlay.Show(owner);
                overlay.FollowGameWindow(initial);
                NativeRect initialOverlayBounds = AssertOverlayMatchesClient(overlay, initial);
                evidence?.RecordGeometry("initial-follow", initial, initialOverlayBounds);
                Assert.Contains(overlay, owner.OwnedWindows);

                bool overlayClickThrough = ZzzOverlayNativeWindow.HasClickThroughStyle(overlay);
                Assert.True(overlayClickThrough);
                int targetClickCount = target.MouseDownCount;
                ClickAt(initial.X!.Value + initial.Width!.Value / 2, initial.Y!.Value + initial.Height!.Value / 2);
                bool targetReceivedClick = WaitFor(() => target.MouseDownCount > targetClickCount);
                evidence?.RecordClickThrough(
                    "run-mode-click-through",
                    "overlay",
                    overlayClickThrough,
                    targetClickCount,
                    target.MouseDownCount,
                    targetReceivedClick);
                Assert.True(targetReceivedClick);

                bool affinityRead = ZzzOverlayNativeWindow.TryGetDisplayAffinity(overlay, out uint affinity, out int affinityError);
                evidence?.RecordDisplayAffinity(
                    "overlay-wda-none",
                    "overlay",
                    affinityRead,
                    affinity,
                    affinityError,
                    ZzzOverlayNativeWindow.WdaNone);
                Assert.True(
                    affinityRead,
                    $"GetWindowDisplayAffinity failed with Win32 error {affinityError}.");
                Assert.Equal(ZzzOverlayNativeWindow.WdaNone, affinity);

                ZzzOverlayDrawItemDto probe = new(
                    ZzzOverlayDrawItemKind.VisionDrawItem,
                    "yolo:capture-probe",
                    new ZzzOverlayRectDto(192d, 108d, 384d, 216d),
                    Color: "#ff00ff");
                overlay.Render(new ZzzOverlayFrameDto(DateTimeOffset.UtcNow, [probe]));
                NativeRect overlayBounds = GetWindowBounds(overlay);
                int probeX = overlayBounds.Left + (int)Math.Round(overlayBounds.Width * 0.2d);
                int probeY = overlayBounds.Top + (int)Math.Round(overlayBounds.Height * 0.1d);
                DrawingColor visibleOverlayProbeColor = default;
                bool overlayProbeBecameVisible = WaitFor(() =>
                {
                    visibleOverlayProbeColor = CaptureSystemPixel(probeX, probeY);
                    return IsProbeColor(visibleOverlayProbeColor);
                });
                evidence?.SaveSystemCapture("system-overlay-before-prevent-capture.png", GetClientBounds(initial));
                evidence?.RecordSystemCaptureProbe(
                    "system-overlay-probe-visible",
                    "system-overlay-before-prevent-capture.png",
                    overlayBounds,
                    probeX,
                    probeY,
                    visibleOverlayProbeColor,
                    IsProbeColor(visibleOverlayProbeColor));
                Assert.True(
                    overlayProbeBecameVisible,
                    "系统截图没有显示未启用防截图的 Overlay 绘制框，当前桌面无法验证 WDA 行为。");
                AssertSystemCaptureExcludesAffinityProbe(initial, evidence);

                settings.PreventCapture = true;
                overlay.ApplySettings(settings);
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
                bool excludedAffinityRead = ZzzOverlayNativeWindow.TryGetDisplayAffinity(
                    overlay,
                    out uint excludedAffinity,
                    out int excludedAffinityError);
                evidence?.RecordDisplayAffinity(
                    "overlay-wda-exclude-from-capture",
                    "overlay",
                    excludedAffinityRead,
                    excludedAffinity,
                    excludedAffinityError,
                    ZzzOverlayNativeWindow.WdaExcludeFromCapture);
                Assert.True(
                    excludedAffinityRead,
                    $"GetWindowDisplayAffinity failed with Win32 error {excludedAffinityError}.");
                Assert.Equal(ZzzOverlayNativeWindow.WdaExcludeFromCapture, excludedAffinity);
                DrawingColor excludedOverlayProbeColor = visibleOverlayProbeColor;
                bool overlayProbeBecameExcluded = WaitFor(() =>
                {
                    excludedOverlayProbeColor = CaptureSystemPixel(probeX, probeY);
                    return !IsProbeColor(excludedOverlayProbeColor);
                });
                evidence?.SaveSystemCapture("system-overlay-after-prevent-capture.png", GetClientBounds(initial));
                evidence?.RecordSystemCaptureProbe(
                    "system-overlay-probe-excluded",
                    "system-overlay-after-prevent-capture.png",
                    overlayBounds,
                    probeX,
                    probeY,
                    excludedOverlayProbeColor,
                    IsProbeColor(excludedOverlayProbeColor));
                Assert.True(
                    overlayProbeBecameExcluded,
                    "启用 WDA_EXCLUDEFROMCAPTURE 后系统截图仍显示 Overlay 绘制框。");

                using Mat captured = ZzzAvaloniaOverlayCapturer.RenderWindowToBgra(
                    overlay,
                    new PixelSize(overlayBounds.Width, overlayBounds.Height));
                Assert.False(captured.Empty());
                Assert.Equal(MatType.CV_8UC4, captured.Type());
                Assert.Equal(overlayBounds.Width, captured.Cols);
                Assert.Equal(overlayBounds.Height, captured.Rows);
                string? inProcessCaptureArtifact = evidence?.SaveInProcessBgra("overlay-in-process-bgra.png", captured);
                Vec4b inProcessProbe = captured.At<Vec4b>(
                    probeY - overlayBounds.Top,
                    probeX - overlayBounds.Left);
                evidence?.RecordInProcessCapture(
                    "in-process-overlay-render",
                    inProcessCaptureArtifact,
                    captured.Cols,
                    captured.Rows,
                    probeX - overlayBounds.Left,
                    probeY - overlayBounds.Top,
                    inProcessProbe);
                Assert.True(inProcessProbe.Item0 > 200 && inProcessProbe.Item2 > 200 && inProcessProbe.Item1 < 80);

                ZzzOverlayPanelSettings panelSettings = new("state", "状态面板", true, 0, 0, 300, 120);
                settings.LayoutEditMode = true;
                panel.ApplyConfiguration(panelSettings, settings, initial, forceGeometry: true);
                panel.Show(owner);
                bool panelClickThrough = ZzzOverlayNativeWindow.HasClickThroughStyle(panel);
                evidence?.RecordClickThrough("layout-edit-panel", "state-panel", panelClickThrough, null, null, null);
                Assert.False(panelClickThrough);
                Assert.Contains(panel, owner.OwnedWindows);
                Dispatcher.UIThread.RunJobs();
                PixelPoint panelStart = panel.Position;
                double panelStartWidth = panel.Width;
                double panelStartHeight = panel.Height;
                Assert.True(
                    ZzzOverlayNativeWindow.TryGetWindowHandle(panel, out nint panelHandle),
                    "Overlay 信息面板没有原生窗口句柄。");
                NativePoint panelPointer = new(panelStart.X + 16, panelStart.Y + 12);
                Assert.True(
                    panelHandle == WindowFromPoint(panelPointer),
                    "拖拽起点没有命中 Overlay 信息面板。");
                nint hitTest = SendMessageW(
                    panelHandle,
                    WmNcHitTest,
                    0,
                    new nint((panelPointer.Y << 16) | (panelPointer.X & 0xffff)));
                Assert.True(
                    hitTest != new nint(HtTransparent),
                    "Overlay 信息面板仍返回 HTTRANSPARENT。");
                DragFromTo(panelStart.X + 16, panelStart.Y + 12, panelStart.X + 52, panelStart.Y + 39);
                bool panelDragged = WaitFor(() => panel.Position != panelStart);
                PixelPoint panelAfterDrag = panel.Position;
                evidence?.RecordPanelEdit(
                    "panel-drag",
                    "drag",
                    panelClickThrough,
                    panelStart,
                    panelAfterDrag,
                    panelStartWidth,
                    panelStartHeight,
                    panel.Width,
                    panel.Height,
                    panel.DesktopScaling,
                    panelDragged);
                Assert.True(panelDragged);

                double panelWidth = panel.Width;
                double panelHeight = panel.Height;
                double panelScaling = Math.Max(0.5d, panel.DesktopScaling);
                PixelPoint panelPosition = panel.Position;
                DragFromTo(
                    panelPosition.X + (int)Math.Round(panelWidth * panelScaling) - 2,
                    panelPosition.Y + (int)Math.Round(panelHeight * panelScaling) - 2,
                    panelPosition.X + (int)Math.Round(panelWidth * panelScaling) + 28,
                    panelPosition.Y + (int)Math.Round(panelHeight * panelScaling) + 19);
                bool panelResized = WaitFor(() => panel.Width > panelWidth || panel.Height > panelHeight);
                evidence?.RecordPanelEdit(
                    "panel-resize",
                    "resize",
                    panelClickThrough,
                    panelPosition,
                    panel.Position,
                    panelWidth,
                    panelHeight,
                    panel.Width,
                    panel.Height,
                    panel.DesktopScaling,
                    panelResized);
                Assert.True(panelResized);

                target.MoveResize(initial.X!.Value + 73, initial.Y!.Value + 49, initial.Width!.Value + 91, initial.Height!.Value + 47);
                ZzzWindowStatusDto moved = target.Snapshot();
                overlay.FollowGameWindow(moved);
                NativeRect movedOverlayBounds = AssertOverlayMatchesClient(overlay, moved);
                evidence?.RecordGeometry("moved-follow", moved, movedOverlayBounds);

                target.Minimize();
                Assert.True(target.IsMinimized);
                target.Restore();
                Assert.False(target.IsMinimized);
                ZzzWindowStatusDto restored = target.Snapshot();
                overlay.FollowGameWindow(restored);
                NativeRect restoredOverlayBounds = AssertOverlayMatchesClient(overlay, restored);
                evidence?.RecordGeometry("restored-follow", restored, restoredOverlayBounds);
                Assert.True(restored.Dpi > 0);

                evidence?.RecordWindowLifecycle(
                    "classic-editor-lifecycle",
                    AssertOwnedWindowCanOpenAndClose(owner, classicEditor, () => classicEditor.ResourcesReleased));
                evidence?.RecordWindowLifecycle(
                    "frontier-editor-lifecycle",
                    AssertOwnedWindowCanOpenAndClose(owner, frontierEditor, () => frontierEditor.ResourcesReleased));
                evidence?.RecordWindowLifecycle(
                    "state-panel-lifecycle",
                    AssertOwnedWindowClosesAndReleases(
                    owner,
                    panel,
                    () => panel.ResourcesReleased,
                    expectedClickThrough: false));
                evidence?.RecordWindowLifecycle(
                    "vision-overlay-lifecycle",
                    AssertOwnedWindowClosesAndReleases(
                    owner,
                    overlay,
                    () => overlay.ResourcesReleased,
                    expectedClickThrough: true));
                evidence?.RecordControllerLifecycle(
                    "overlay-controller-owner-dispose",
                    AssertControllerOwnerLifecycle(owner, restored));
            }
            catch (Exception exception)
            {
                testFailure = exception;
                evidence?.RecordFailure(exception);
                throw;
            }
            finally
            {
                try
                {
                    evidence?.WriteSummary();
                }
                catch (Exception) when (testFailure is not null)
                {
                }
                finally
                {
                    frontierEditor.Close();
                    classicEditor.Close();
                    panel.Close();
                    overlay.Close();
                    owner.Close();
                    if (restoreCursor)
                    {
                        SetCursorPos(originalCursor.X, originalCursor.Y);
                    }

                    if (originalForeground != 0)
                    {
                        SetForegroundWindow(originalForeground);
                    }
                }
            }
        });
    }

    private static void EnsureFluentTheme()
    {
        if (Avalonia.Application.Current?.Styles.OfType<FluentAvaloniaTheme>().Any() == false)
        {
            Avalonia.Application.Current.Styles.Add(new FluentAvaloniaTheme());
        }
    }

    private static WindowLifecycleEvidence AssertOwnedWindowCanOpenAndClose(
        AvaloniaWindow owner,
        AvaloniaWindow child,
        Func<bool> resourcesReleased)
    {
        child.Show(owner);
        Assert.True(WaitFor(() => child.IsVisible));
        bool ownerRegistered = owner.OwnedWindows.Contains(child);
        Assert.True(ownerRegistered);
        bool nativeHandleCreated = ZzzOverlayNativeWindow.TryGetWindowHandle(child, out nint handle);
        Assert.True(nativeHandleCreated);
        Assert.True(IsWindow(handle));
        return AssertOwnedWindowClosesAndReleases(owner, child, resourcesReleased, expectedClickThrough: null, handle);
    }

    private static WindowLifecycleEvidence AssertOwnedWindowClosesAndReleases(
        AvaloniaWindow owner,
        AvaloniaWindow child,
        Func<bool> resourcesReleased,
        bool? expectedClickThrough,
        nint existingHandle = default)
    {
        bool ownerRegistered = owner.OwnedWindows.Contains(child);
        Assert.True(ownerRegistered);
        bool nativeHandleCreated = ZzzOverlayNativeWindow.TryGetWindowHandle(child, out nint existingWindowHandle);
        Assert.True(nativeHandleCreated);
        bool nativeClickThrough = ZzzOverlayNativeWindow.HasClickThroughStyle(child);
        if (expectedClickThrough.HasValue)
        {
            Assert.Equal(expectedClickThrough.Value, nativeClickThrough);
        }

        nint handle = existingHandle;
        if (handle == 0)
        {
            handle = existingWindowHandle;
        }

        int closedCount = 0;
        EventHandler onClosed = (_, _) => closedCount++;
        child.Closed += onClosed;
        child.Close();
        bool closed = WaitFor(() => closedCount == 1);
        bool removedFromOwner = WaitFor(() => !owner.OwnedWindows.Contains(child));
        bool nativeHandleDestroyed = WaitFor(() => !IsWindow(handle));
        child.Closed -= onClosed;
        bool released = resourcesReleased();
        Assert.True(closed);
        Assert.True(removedFromOwner);
        Assert.True(nativeHandleDestroyed);
        Assert.True(released);
        return new WindowLifecycleEvidence(
            child.GetType().Name,
            ownerRegistered,
            nativeHandleCreated,
            nativeClickThrough,
            closedCount,
            removedFromOwner,
            nativeHandleDestroyed,
            released);
    }

    private static ControllerLifecycleEvidence AssertControllerOwnerLifecycle(
        AvaloniaWindow owner,
        ZzzWindowStatusDto gameWindow)
    {
        string root = Path.Combine(Path.GetTempPath(), $"zzz-overlay-controller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        ZzzOverlayController? controller = null;
        try
        {
            ZzzConfigScopeService scopes = new(root);
            ZzzBackendResult<ZzzConfigScopeValuesDto> saved = scopes.Save(new ZzzSaveConfigScopeRequest(
                "overlay",
                new Dictionary<string, object?>
                {
                    ["enabled"] = true,
                    ["visible"] = true,
                    ["anti_capture"] = false,
                    ["state_panel_enabled"] = true,
                    ["log_panel_enabled"] = false,
                    ["decision_panel_enabled"] = false,
                    ["timeline_panel_enabled"] = false,
                    ["performance_panel_enabled"] = false,
                }));
            Assert.True(saved.Success, saved.Error);

            IZzzAppBackend backend = DispatchProxy.Create<IZzzAppBackend, WindowsIntegrationBackendProxy>();
            WindowsIntegrationBackendProxy proxy = (WindowsIntegrationBackendProxy)backend;
            proxy.Scopes = scopes;
            proxy.Window = gameWindow with
            {
                IsWinValid = true,
                IsWinActive = true,
                IsWinMinimized = false,
            };
            using ZzzOverlayService service = new();
            controller = new ZzzOverlayController(service, backend);
            controller.AttachOwner(owner);
            controller.Show();
            Assert.True(WaitFor(() => controller.VisionWindowForTesting?.IsVisible == true));
            Assert.True(WaitFor(() => controller.PanelWindowCountForTesting == 1));
            ZzzOverlayTechnicalWindow vision = Assert.IsType<ZzzOverlayTechnicalWindow>(controller.VisionWindowForTesting);
            ZzzOverlayInfoPanelWindow panel = Assert.Single(controller.PanelWindowsForTesting);
            Assert.Contains(vision, owner.OwnedWindows);
            Assert.Contains(panel, owner.OwnedWindows);
            Assert.True(ZzzOverlayNativeWindow.TryGetWindowHandle(vision, out nint visionHandle));
            Assert.True(ZzzOverlayNativeWindow.TryGetWindowHandle(panel, out nint panelHandle));
            Assert.True(IsWindow(visionHandle));
            Assert.True(IsWindow(panelHandle));
            bool timersRunning = controller.TimersRunningForTesting;
            Assert.True(timersRunning);

            controller.Dispose();
            bool visionDestroyed = WaitFor(() => !IsWindow(visionHandle));
            bool panelDestroyed = WaitFor(() => !IsWindow(panelHandle));
            bool windowsRemoved = WaitFor(
                () => !owner.OwnedWindows.Contains(vision) && !owner.OwnedWindows.Contains(panel));
            bool resourcesReleased = vision.ResourcesReleased && panel.ResourcesReleased;
            bool controllerDetached = controller.OwnerWindowForTesting is null &&
                                      controller.VisionWindowForTesting is null &&
                                      controller.PanelWindowCountForTesting == 0 &&
                                      !controller.TimersRunningForTesting;
            Assert.True(visionDestroyed);
            Assert.True(panelDestroyed);
            Assert.True(windowsRemoved);
            Assert.True(resourcesReleased);
            Assert.True(controllerDetached);
            return new ControllerLifecycleEvidence(
                OwnerAttached: true,
                OwnedWindowCountBeforeDispose: 2,
                TimersRunningBeforeDispose: timersRunning,
                VisionHandleDestroyed: visionDestroyed,
                PanelHandleDestroyed: panelDestroyed,
                WindowsRemovedFromOwner: windowsRemoved,
                ResourcesReleased: resourcesReleased,
                ControllerDetached: controllerDetached);
        }
        finally
        {
            controller?.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    private static NativeRect AssertOverlayMatchesClient(ZzzOverlayTechnicalWindow overlay, ZzzWindowStatusDto client)
    {
        NativeRect actual = default;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            actual = GetWindowBounds(overlay);
            if (MatchesWithinOnePhysicalPixel(actual, client))
            {
                return actual;
            }

            Thread.Sleep(15);
        }

        Assert.InRange(Math.Abs(actual.Left - client.X!.Value), 0, 1);
        Assert.InRange(Math.Abs(actual.Top - client.Y!.Value), 0, 1);
        Assert.InRange(Math.Abs(actual.Right - (client.X.Value + client.Width!.Value)), 0, 1);
        Assert.InRange(Math.Abs(actual.Bottom - (client.Y.Value + client.Height!.Value)), 0, 1);
        return actual;
    }

    private static bool MatchesWithinOnePhysicalPixel(NativeRect overlay, ZzzWindowStatusDto client) =>
        Math.Abs(overlay.Left - client.X!.Value) <= 1 &&
        Math.Abs(overlay.Top - client.Y!.Value) <= 1 &&
        Math.Abs(overlay.Right - (client.X.Value + client.Width!.Value)) <= 1 &&
        Math.Abs(overlay.Bottom - (client.Y.Value + client.Height!.Value)) <= 1;

    private static NativeRect GetWindowBounds(Avalonia.Controls.Window window)
    {
        Assert.True(ZzzOverlayNativeWindow.TryGetWindowHandle(window, out nint hwnd));
        Assert.True(GetWindowRect(hwnd, out NativeRect bounds));
        return bounds;
    }

    private static NativeRect GetClientBounds(ZzzWindowStatusDto client)
    {
        int left = client.X!.Value;
        int top = client.Y!.Value;
        int width = client.Width!.Value;
        int height = client.Height!.Value;
        return new NativeRect
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height,
        };
    }

    private static DrawingColor CaptureSystemPixel(int x, int y)
    {
        using DrawingBitmap bitmap = new(1, 1);
        using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
        CopyScreenRegion(graphics, x, y, 1, 1);
        return bitmap.GetPixel(0, 0);
    }

    private static void CopyScreenRegion(DrawingGraphics graphics, int sourceX, int sourceY, int width, int height)
    {
        nint destination = graphics.GetHdc();
        nint source = GetDC(0);
        try
        {
            if (source == 0 || !BitBlt(destination, 0, 0, width, height, source, sourceX, sourceY, SrccopyCaptureBlt))
            {
                throw new InvalidOperationException($"BitBlt failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }
        }
        finally
        {
            if (source != 0)
            {
                ReleaseDC(0, source);
            }

            graphics.ReleaseHdc(destination);
        }
    }

    private static bool IsProbeColor(DrawingColor color) => color.R > 200 && color.G < 80 && color.B > 200;

    private static void ClickAt(int x, int y)
    {
        MoveCursor(x, y);
        MouseEvent(MouseEventLeftDown, 0, 0, 0, 0);
        MouseEvent(MouseEventLeftUp, 0, 0, 0, 0);
    }

    private static void DragFromTo(int fromX, int fromY, int toX, int toY)
    {
        Exception? injectionFailure = null;
        Thread injector = new(() =>
        {
            bool mouseDown = false;
            try
            {
                MoveCursor(fromX, fromY);
                Thread.Sleep(50);
                MouseEvent(MouseEventLeftDown, 0, 0, 0, 0);
                mouseDown = true;
                Thread.Sleep(80);
                const int steps = 6;
                for (int step = 1; step <= steps; step++)
                {
                    MoveCursor(
                        fromX + (toX - fromX) * step / steps,
                        fromY + (toY - fromY) * step / steps);
                    Thread.Sleep(20);
                }
            }
            catch (Exception exception)
            {
                injectionFailure = exception;
            }
            finally
            {
                if (mouseDown)
                {
                    MouseEvent(MouseEventLeftUp, 0, 0, 0, 0);
                }
            }
        })
        {
            IsBackground = true,
            Name = "ZzzOd Overlay Input Injector",
        };
        injector.Start();
        while (injector.IsAlive)
        {
            PumpNativeMessages();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        injector.Join();
        PumpNativeMessages();
        Dispatcher.UIThread.RunJobs();
        if (injectionFailure is not null)
        {
            ExceptionDispatchInfo.Capture(injectionFailure).Throw();
        }
    }

    private static void MoveCursor(int x, int y)
    {
        if (!SetCursorPos(x, y))
        {
            throw new InvalidOperationException($"SetCursorPos failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }
    }

    private static bool WaitFor(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            PumpNativeMessages();
            Dispatcher.UIThread.RunJobs();
            PumpNativeMessages();
            if (condition())
            {
                return true;
            }

            Thread.Sleep(15);
        }

        return condition();
    }

    private static void PumpNativeMessages()
    {
        while (PeekMessageW(out NativeMessage message, 0, 0, 0, PmRemove))
        {
            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }
    }

    private static void AssertSystemCaptureExcludesAffinityProbe(
        ZzzWindowStatusDto client,
        WindowsIntegrationEvidence? evidence)
    {
        Avalonia.Controls.Window probe = new()
        {
            Width = 80d,
            Height = 80d,
            Position = new PixelPoint(client.X!.Value + 24, client.Y!.Value + 24),
            Background = Brushes.Magenta,
            Topmost = true,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowDecorations = WindowDecorations.None,
        };
        try
        {
            probe.Show();
            ZzzOverlayNativeWindow.Apply(probe, clickThrough: false, preventCapture: false);
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
            NativeRect bounds = GetWindowBounds(probe);
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;
            evidence?.SaveSystemCapture("system-affinity-probe-visible.png", bounds);
            DrawingColor visibleProbeColor = CaptureSystemPixel(centerX, centerY);
            evidence?.RecordSystemCaptureProbe(
                "system-affinity-probe-visible",
                "system-affinity-probe-visible.png",
                bounds,
                centerX,
                centerY,
                visibleProbeColor,
                IsProbeColor(visibleProbeColor));
            Assert.True(
                IsProbeColor(visibleProbeColor),
                "系统截图没有显示未设置防截图的实心测试窗口，当前桌面无法验证 WDA 行为。");

            ZzzOverlayNativeWindow.Apply(probe, clickThrough: false, preventCapture: true);
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
            bool affinityRead = ZzzOverlayNativeWindow.TryGetDisplayAffinity(probe, out uint affinity, out int affinityError);
            evidence?.RecordDisplayAffinity(
                "system-affinity-probe-wda-exclude-from-capture",
                "system-affinity-probe",
                affinityRead,
                affinity,
                affinityError,
                ZzzOverlayNativeWindow.WdaExcludeFromCapture);
            Assert.True(affinityRead, $"GetWindowDisplayAffinity failed with Win32 error {affinityError}.");
            Assert.Equal(ZzzOverlayNativeWindow.WdaExcludeFromCapture, affinity);
            evidence?.SaveSystemCapture("system-affinity-probe-excluded.png", bounds);
            DrawingColor excludedProbeColor = CaptureSystemPixel(centerX, centerY);
            evidence?.RecordSystemCaptureProbe(
                "system-affinity-probe-excluded",
                "system-affinity-probe-excluded.png",
                bounds,
                centerX,
                centerY,
                excludedProbeColor,
                IsProbeColor(excludedProbeColor));
            Assert.False(IsProbeColor(excludedProbeColor));
        }
        finally
        {
            probe.Close();
        }
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                AppBuilder.Configure<Avalonia.Application>().UsePlatformDetect().SetupWithoutStarting();
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "ZzzOd Overlay Windows Integration",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private class WindowsIntegrationBackendProxy : DispatchProxy
    {
        public ZzzConfigScopeService Scopes { get; set; } = null!;

        public ZzzWindowStatusDto Window { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= [];
            return targetMethod.Name switch
            {
                nameof(IZzzAppBackend.GetConfigScope) => Scopes.Read(
                    (string)args[0]!,
                    (int?)args[1],
                    (string?)args[2]),
                nameof(IZzzAppBackend.SaveConfigScope) => Scopes.Save((ZzzSaveConfigScopeRequest)args[0]!),
                nameof(IZzzAppBackend.GetWindow) => ZzzBackendResult<ZzzWindowStatusDto>.Ok(Window),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }
    }

    private sealed class WindowsIntegrationEvidence
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly List<WindowsIntegrationEvidenceRecord> _records = [];
        private readonly List<string> _artifacts = [];
        private readonly DateTimeOffset _startedAtUtc;
        private readonly string _directory;

        private WindowsIntegrationEvidence(string configuredDirectory)
        {
            _startedAtUtc = DateTimeOffset.UtcNow;
            string runDirectoryName = $"overlay-windows-integration-{_startedAtUtc:yyyyMMddTHHmmssfff'Z'}-{Environment.ProcessId}";
            _directory = Path.Combine(Path.GetFullPath(configuredDirectory), runDirectoryName);
            Directory.CreateDirectory(_directory);
        }

        public static WindowsIntegrationEvidence? CreateFromEnvironment()
        {
            string? configuredDirectory = Environment.GetEnvironmentVariable(EvidenceDirectoryEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configuredDirectory)
                ? null
                : new WindowsIntegrationEvidence(configuredDirectory.Trim());
        }

        public void RecordGeometry(string stage, ZzzWindowStatusDto target, NativeRect overlay)
        {
            NativeRect targetBounds = GetClientBounds(target);
            int leftDelta = overlay.Left - targetBounds.Left;
            int topDelta = overlay.Top - targetBounds.Top;
            int rightDelta = overlay.Right - targetBounds.Right;
            int bottomDelta = overlay.Bottom - targetBounds.Bottom;
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                Geometry: new WindowGeometryEvidence(
                    PhysicalRectEvidence.From(targetBounds),
                    PhysicalRectEvidence.From(overlay),
                    leftDelta,
                    topDelta,
                    rightDelta,
                    bottomDelta,
                    target.Dpi,
                    Math.Abs(leftDelta) <= 1 &&
                    Math.Abs(topDelta) <= 1 &&
                    Math.Abs(rightDelta) <= 1 &&
                    Math.Abs(bottomDelta) <= 1)));
        }

        public void RecordDisplayAffinity(
            string stage,
            string window,
            bool querySucceeded,
            uint affinity,
            int errorCode,
            uint expectedAffinity)
        {
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                DisplayAffinity: new DisplayAffinityEvidence(
                    window,
                    querySucceeded,
                    affinity,
                    errorCode,
                    expectedAffinity)));
        }

        public void RecordClickThrough(
            string stage,
            string window,
            bool nativeClickThrough,
            int? targetMouseDownBefore,
            int? targetMouseDownAfter,
            bool? targetReceivedInput)
        {
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                ClickThrough: new ClickThroughEvidence(
                    window,
                    nativeClickThrough,
                    targetMouseDownBefore,
                    targetMouseDownAfter,
                    targetReceivedInput)));
        }

        public void RecordPanelEdit(
            string stage,
            string interaction,
            bool clickThroughDuringEdit,
            PixelPoint beforePosition,
            PixelPoint afterPosition,
            double beforeWidth,
            double beforeHeight,
            double afterWidth,
            double afterHeight,
            double desktopScaling,
            bool completed)
        {
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                PanelEdit: new PanelEditEvidence(
                    interaction,
                    clickThroughDuringEdit,
                    new PixelPointEvidence(beforePosition.X, beforePosition.Y),
                    new PixelPointEvidence(afterPosition.X, afterPosition.Y),
                    beforeWidth,
                    beforeHeight,
                    afterWidth,
                    afterHeight,
                    desktopScaling,
                    completed)));
        }

        public void RecordSystemCaptureProbe(
            string stage,
            string artifact,
            NativeRect bounds,
            int sampleX,
            int sampleY,
            DrawingColor sampleColor,
            bool probeVisible)
        {
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                SystemCapture: new SystemCaptureEvidence(
                    artifact,
                    PhysicalRectEvidence.From(bounds),
                    sampleX,
                    sampleY,
                    $"#{sampleColor.A:X2}{sampleColor.R:X2}{sampleColor.G:X2}{sampleColor.B:X2}",
                    probeVisible)));
        }

        public void RecordInProcessCapture(
            string stage,
            string? artifact,
            int width,
            int height,
            int probeX,
            int probeY,
            Vec4b probeColor)
        {
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                InProcessCapture: new OverlayCaptureEvidence(
                    artifact,
                    width,
                    height,
                    probeX,
                    probeY,
                    probeColor.Item0,
                    probeColor.Item1,
                    probeColor.Item2,
                    probeColor.Item3)));
        }

        public void RecordWindowLifecycle(string stage, WindowLifecycleEvidence lifecycle)
        {
            ArgumentNullException.ThrowIfNull(lifecycle);
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                WindowLifecycle: lifecycle));
        }

        public void RecordControllerLifecycle(string stage, ControllerLifecycleEvidence lifecycle)
        {
            ArgumentNullException.ThrowIfNull(lifecycle);
            _records.Add(new WindowsIntegrationEvidenceRecord(
                stage,
                DateTimeOffset.UtcNow,
                ControllerLifecycle: lifecycle));
        }

        public void SaveSystemCapture(string fileName, NativeRect bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), "系统截图区域必须具有正尺寸。");
            }

            using DrawingBitmap bitmap = new(bounds.Width, bounds.Height);
            using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
            CopyScreenRegion(graphics, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            bitmap.Save(GetOutputPath(fileName), DrawingImageFormat.Png);
            _artifacts.Add(fileName);
        }

        public string SaveInProcessBgra(string fileName, Mat bgra)
        {
            ArgumentNullException.ThrowIfNull(bgra);
            if (!Cv2.ImWrite(GetOutputPath(fileName), bgra))
            {
                throw new InvalidOperationException($"无法写入 Overlay BGRA 证据图像 {fileName}。");
            }

            _artifacts.Add(fileName);
            return fileName;
        }

        public void RecordFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            _records.Add(new WindowsIntegrationEvidenceRecord(
                "failure",
                DateTimeOffset.UtcNow,
                Failure: $"{exception.GetType().FullName}: {exception.Message}"));
        }

        public void WriteSummary()
        {
            WindowsIntegrationEvidenceDocument document = new(
                "ZzzOverlayWindowsIntegrationTests.OverlayFollowsControlledWin32ClientAreaAndKeepsInProcessCaptureAvailable",
                EvidenceDirectoryEnvironmentVariable,
                _directory,
                _startedAtUtc,
                DateTimeOffset.UtcNow,
                _records.ToArray(),
                _artifacts.ToArray());
            File.WriteAllText(
                GetOutputPath("summary.json"),
                JsonSerializer.Serialize(document, JsonOptions));
        }

        private string GetOutputPath(string fileName) => Path.Combine(_directory, fileName);
    }

    private sealed record WindowsIntegrationEvidenceDocument(
        string Test,
        string EnvironmentVariable,
        string OutputDirectory,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        IReadOnlyList<WindowsIntegrationEvidenceRecord> Records,
        IReadOnlyList<string> Artifacts);

    private sealed record WindowsIntegrationEvidenceRecord(
        string Stage,
        DateTimeOffset RecordedAtUtc,
        WindowGeometryEvidence? Geometry = null,
        DisplayAffinityEvidence? DisplayAffinity = null,
        ClickThroughEvidence? ClickThrough = null,
        PanelEditEvidence? PanelEdit = null,
        SystemCaptureEvidence? SystemCapture = null,
        OverlayCaptureEvidence? InProcessCapture = null,
        WindowLifecycleEvidence? WindowLifecycle = null,
        ControllerLifecycleEvidence? ControllerLifecycle = null,
        string? Failure = null);

    private sealed record WindowLifecycleEvidence(
        string Window,
        bool OwnerRegistered,
        bool NativeHandleCreated,
        bool NativeClickThrough,
        int ClosedCount,
        bool RemovedFromOwner,
        bool NativeHandleDestroyed,
        bool ResourcesReleased);

    private sealed record ControllerLifecycleEvidence(
        bool OwnerAttached,
        int OwnedWindowCountBeforeDispose,
        bool TimersRunningBeforeDispose,
        bool VisionHandleDestroyed,
        bool PanelHandleDestroyed,
        bool WindowsRemovedFromOwner,
        bool ResourcesReleased,
        bool ControllerDetached);

    private sealed record WindowGeometryEvidence(
        PhysicalRectEvidence TargetClient,
        PhysicalRectEvidence Overlay,
        int LeftDeltaPixels,
        int TopDeltaPixels,
        int RightDeltaPixels,
        int BottomDeltaPixels,
        uint TargetDpi,
        bool WithinOnePhysicalPixel);

    private sealed record PhysicalRectEvidence(int Left, int Top, int Width, int Height)
    {
        public static PhysicalRectEvidence From(NativeRect bounds)
            => new(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    private sealed record DisplayAffinityEvidence(
        string Window,
        bool QuerySucceeded,
        uint Affinity,
        int Win32Error,
        uint ExpectedAffinity);

    private sealed record ClickThroughEvidence(
        string Window,
        bool NativeClickThrough,
        int? TargetMouseDownBefore,
        int? TargetMouseDownAfter,
        bool? TargetReceivedInput);

    private sealed record PanelEditEvidence(
        string Interaction,
        bool ClickThroughDuringEdit,
        PixelPointEvidence BeforePosition,
        PixelPointEvidence AfterPosition,
        double BeforeWidth,
        double BeforeHeight,
        double AfterWidth,
        double AfterHeight,
        double DesktopScaling,
        bool Completed);

    private sealed record PixelPointEvidence(int X, int Y);

    private sealed record SystemCaptureEvidence(
        string Artifact,
        PhysicalRectEvidence CaptureBounds,
        int SampleX,
        int SampleY,
        string SampleArgb,
        bool ProbeColorVisible);

    private sealed record OverlayCaptureEvidence(
        string? Artifact,
        int Width,
        int Height,
        int ProbeX,
        int ProbeY,
        byte ProbeBlue,
        byte ProbeGreen,
        byte ProbeRed,
        byte ProbeAlpha);

    private sealed class ControlledWin32Window : IDisposable
    {
        private const uint WsOverlappedWindow = 0x00CF0000;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const int SwShow = 5;
        private const int SwMinimize = 6;
        private const int SwRestore = 9;
        private const int ErrorClassAlreadyExists = 1410;
        private static readonly object RegistrationLock = new();
        private static readonly ConcurrentDictionary<nint, int> MouseDownCounts = new();
        private static readonly string ClassName = $"ZzzOdOverlayIntegration.{typeof(ControlledWin32Window).Assembly.GetName().Version}";
        private static readonly WindowProcedure Procedure = WindowProcedureImpl;
        private static bool _registered;
        private nint _handle;

        private ControlledWin32Window(nint handle)
        {
            _handle = handle;
        }

        public bool IsMinimized => IsIconic(_handle);

        public bool IsForeground => GetForegroundWindow() == _handle;

        public int MouseDownCount => MouseDownCounts.TryGetValue(_handle, out int count) ? count : 0;

        public static ControlledWin32Window Create()
        {
            EnsureRegistered();
            nint handle = CreateWindowExW(
                0,
                ClassName,
                "ZZZ Overlay Integration Target",
                WsOverlappedWindow,
                80,
                80,
                960,
                640,
                0,
                0,
                GetModuleHandleW(null),
                0);
            if (handle == 0)
            {
                throw new InvalidOperationException($"CreateWindowExW failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            return new ControlledWin32Window(handle);
        }

        public void Show()
        {
            ShowWindow(_handle, SwShow);
        }

        public bool Activate()
        {
            nint foreground = GetForegroundWindow();
            uint currentThread = GetCurrentThreadId();
            uint foregroundThread = foreground == 0 ? 0u : GetWindowThreadProcessId(foreground, out _);
            bool attached = foregroundThread != 0 && foregroundThread != currentThread &&
                AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                return SetForegroundWindow(_handle);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }

        public void MoveResize(int x, int y, int width, int height)
        {
            if (!SetWindowPos(_handle, 0, x, y, width, height, SwpNoZOrder | SwpNoActivate))
            {
                throw new InvalidOperationException($"SetWindowPos failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }
        }

        public void Minimize()
        {
            ShowWindow(_handle, SwMinimize);
        }

        public void Restore()
        {
            ShowWindow(_handle, SwRestore);
        }

        public ZzzWindowStatusDto Snapshot()
        {
            if (!GetClientRect(_handle, out NativeRect client))
            {
                throw new InvalidOperationException($"GetClientRect failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            NativePoint origin = new(0, 0);
            if (!ClientToScreen(_handle, ref origin))
            {
                throw new InvalidOperationException($"ClientToScreen failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            return new ZzzWindowStatusDto(
                null,
                true,
                IsForeground,
                false,
                origin.X,
                origin.Y,
                client.Width,
                client.Height,
                IsMinimized,
                GetDpiForWindow(_handle));
        }

        public void Dispose()
        {
            if (_handle != 0)
            {
                DestroyWindow(_handle);
                MouseDownCounts.TryRemove(_handle, out _);
                _handle = 0;
            }
        }

        private static void EnsureRegistered()
        {
            lock (RegistrationLock)
            {
                if (_registered)
                {
                    return;
                }

                WindowClassEx windowClass = new()
                {
                    Size = (uint)Marshal.SizeOf<WindowClassEx>(),
                    Procedure = Procedure,
                    Instance = GetModuleHandleW(null),
                    ClassName = ClassName,
                };
                ushort atom = RegisterClassExW(ref windowClass);
                if (atom == 0 && Marshal.GetLastWin32Error() != ErrorClassAlreadyExists)
                {
                    throw new InvalidOperationException($"RegisterClassExW failed with Win32 error {Marshal.GetLastWin32Error()}.");
                }

                _registered = true;
            }
        }

        private static nint WindowProcedureImpl(nint hwnd, uint message, nint wParam, nint lParam)
        {
            if (message == WmLButtonDown)
            {
                MouseDownCounts.AddOrUpdate(hwnd, 1, static (_, count) => count + 1);
            }

            return DefWindowProcW(hwnd, message, wParam, lParam);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint Size;
        public uint Style;
        public WindowProcedure Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandleW(string? moduleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WindowClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint hwnd, uint message, nint wParam, nint lParam);

    private const uint WmLButtonDown = 0x0201;
    private const uint WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint PmRemove = 0x0001;
    private const uint SrccopyCaptureBlt = 0x40CC0020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        nint destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        nint source,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(
        out NativeMessage message,
        nint window,
        uint minimumMessage,
        uint maximumMessage,
        uint removeMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DispatchMessageW(ref NativeMessage message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(uint flags, uint dx, uint dy, uint data, nuint extraInfo);

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class WindowsIntegrationFactAttribute : FactAttribute
    {
        public WindowsIntegrationFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "Requires a Windows desktop session.";
                return;
            }

            if (!string.Equals(
                    Environment.GetEnvironmentVariable(EnabledEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                Skip = $"Requires {EnabledEnvironmentVariable}=1 and an interactive Windows desktop session.";
            }
        }
    }
}

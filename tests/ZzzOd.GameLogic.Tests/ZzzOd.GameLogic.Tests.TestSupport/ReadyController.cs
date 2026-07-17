using System;
using OneDragon.Core.Abstractions.Geometry;
using OneDragon.Core.Controller;
using OpenCvSharp;

namespace ZzzOd.GameLogic.Tests.TestSupport;

internal sealed class ReadyController : ControllerBase
{
	public override bool IsGameWindowReady => true;

	public override bool InitBeforeContextRun()
	{
		return true;
	}

	public override bool Click(OneDragon.Core.Abstractions.Geometry.Point? position = null, TimeSpan? pressTime = null, bool pcAlt = false, string? gamepadAction = null)
	{
		return true;
	}

	public override void Scroll(int down, OneDragon.Core.Abstractions.Geometry.Point? position = null)
	{
	}

	public override void DragTo(OneDragon.Core.Abstractions.Geometry.Point end, OneDragon.Core.Abstractions.Geometry.Point? start = null, TimeSpan? duration = null)
	{
	}

	public override void InputText(string text)
	{
	}

	public override void MouseMove(OneDragon.Core.Abstractions.Geometry.Point position)
	{
	}

	protected override Mat? GetScreenshot(bool independent = false)
	{
		return null;
	}
}

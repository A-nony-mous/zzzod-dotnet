using System;
using OneDragon.Core.Abstractions.Geometry;

namespace ZzzOd.GameLogic.Application.HollowZero.LostVoid;

/// <summary>
/// 藏品候选位置。
/// </summary>
public sealed class LostVoidArtifactPos
{
	/// <summary>藏品。</summary>
	public LostVoidArtifact Artifact { get; set; }

	/// <summary>位置。</summary>
	public Rect Rect { get; }

	/// <summary>OCR 原文。</summary>
	public string OcrText { get; }

	/// <summary>是否主选名称。</summary>
	public bool IsPrimaryName { get; set; }

	/// <summary>是否 NEW。</summary>
	public bool IsNew { get; set; }

	/// <summary>是否已选择。</summary>
	public bool Chosen { get; set; }

	/// <summary>是否可选。</summary>
	public bool CanChoose { get; set; } = true;

	/// <summary>是否同流派武备。</summary>
	public bool HasSameStyle { get; set; }

	/// <summary>商店价格。</summary>
	public int? StorePrice { get; private set; }

	/// <summary>商店购买按钮位置。</summary>
	public Rect? StoreBuyRect { get; private set; }

	/// <summary>
	/// 初始化藏品候选。
	/// </summary>
	public LostVoidArtifactPos(LostVoidArtifact artifact, Rect rect, string ocrText = "", bool isPrimaryName = true)
	{
		Artifact = artifact;
		Rect = rect;
		OcrText = ocrText;
		IsPrimaryName = isPrimaryName;
	}

	/// <summary>
	/// 关联商店价格。
	/// </summary>
	public bool AddPrice(int price, Rect rect)
	{
		int num = Math.Abs(Rect.Center.X - rect.Center.X);
		if (num >= Rect.Width)
		{
			return false;
		}
		StorePrice = price;
		return true;
	}

	/// <summary>
	/// 关联商店购买按钮。
	/// </summary>
	public bool AddBuy(Rect rect)
	{
		int num = Math.Abs(Rect.Center.X - rect.Center.X);
		if (num >= Rect.Width)
		{
			return false;
		}
		StoreBuyRect = rect;
		return true;
	}
}

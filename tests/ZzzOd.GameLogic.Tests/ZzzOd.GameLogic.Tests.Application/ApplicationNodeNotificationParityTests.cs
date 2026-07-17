using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OneDragon.Core.Abstractions.Operations;
using Xunit;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.CityFund;
using ZzzOd.GameLogic.Application.Coffee;
using ZzzOd.GameLogic.Application.DriveDiscDismantle;
using ZzzOd.GameLogic.Application.EmailApp;
using ZzzOd.GameLogic.Application.EngagementReward;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Application.HouHouBakery;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Application.LifeOnLine;
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.Application.RedemptionCode;
using ZzzOd.GameLogic.Application.RiduWeekly;
using ZzzOd.GameLogic.Application.ScratchCard;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.GameLogic.Application.TrigramsCollection;
using ZzzOd.GameLogic.Application.WorldPatrol;

namespace ZzzOd.GameLogic.Tests.Application;

public sealed class ApplicationNodeNotificationParityTests
{
	public static IEnumerable<object?[]> PythonNodeNotifications => new object[33][]
	{
		new object[5]
		{
			typeof(ChargePlanOperation),
			"BackToWorld",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(CityFundOperation),
			"ClickLevelClaim",
			OperationNodeNotifyTiming.CurrentSuccess,
			true,
			false
		},
		new object[5]
		{
			typeof(CoffeeOperation),
			"ChargeConfirm",
			OperationNodeNotifyTiming.CurrentSuccess,
			true,
			false
		},
		new object[5]
		{
			typeof(DriveDiscDismantleOperation),
			"ClickSalvage",
			OperationNodeNotifyTiming.CurrentSuccess,
			true,
			false
		},
		new object[5]
		{
			typeof(EmailOperation),
			"ClickGetAll",
			OperationNodeNotifyTiming.CurrentSuccess,
			true,
			false
		},
		new object[5]
		{
			typeof(EngagementRewardOperation),
			"CheckEngagement",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(LostVoidApp).Assembly.GetType("ZzzOd.GameLogic.Application.HollowZero.LostVoid.LostVoidAppOperation"),
			"CheckBountyCommission",
			OperationNodeNotifyTiming.CurrentDone,
			false,
			true
		},
		new object[5]
		{
			typeof(LostVoidApp).Assembly.GetType("ZzzOd.GameLogic.Application.HollowZero.LostVoid.LostVoidAppOperation"),
			"ClaimAll",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			false
		},
		new object[5]
		{
			typeof(LostVoidRunLevel),
			"OnEntry",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(LostVoidRunLevel),
			"PushErrorAsync",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(LostVoidRunLevel),
			"HandleBattleFail",
			OperationNodeNotifyTiming.PreviousDone,
			true,
			true
		},
		new object[5]
		{
			typeof(LostVoidApp).Assembly.GetType("ZzzOd.GameLogic.Application.HollowZero.WitheredDomain.WitheredDomainFinishOperation"),
			"FinishAsync",
			OperationNodeNotifyTiming.PreviousDone,
			true,
			true
		},
		new object[5]
		{
			typeof(HouHouBakeryOperation),
			"Collect",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			false
		},
		new object[5]
		{
			typeof(IntelBoardOperation),
			"FinishProcessing",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(LifeOnLineOperation),
			"BackToWorld",
			OperationNodeNotifyTiming.PreviousDone,
			true,
			false
		},
		new object[5]
		{
			typeof(NotoriousHuntOperation),
			"ClaimAll",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			false
		},
		new object[5]
		{
			typeof(RandomPlayOperation),
			"BackToWorld",
			OperationNodeNotifyTiming.PreviousDone,
			true,
			false
		},
		new object[5]
		{
			typeof(RedemptionCodeOperation),
			"ConfirmCode",
			OperationNodeNotifyTiming.CurrentSuccess,
			true,
			false
		},
		new object[5]
		{
			typeof(RiduWeeklyOperation),
			"ClaimReward",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			false
		},
		new object[5]
		{
			typeof(ScratchCardOperation),
			"BackToWorld",
			OperationNodeNotifyTiming.PreviousDone,
			true,
			false
		},
		new object[5]
		{
			typeof(ShiyuDefenseOperation),
			"ToNextNode",
			OperationNodeNotifyTiming.PreviousDone,
			true,
			true
		},
		new object[5]
		{
			typeof(ShiyuDefenseOperation),
			"ClaimReward",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleAutoManage",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleAdventureSquad",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleYumChaSin",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleAdventureSquadAfterYumChaSin",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleCraft",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleSalesStall",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleGoodGoods",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandleBooBox",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(SuibianTempleOperation),
			"HandlePawnshop",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		},
		new object[5]
		{
			typeof(TrigramsCollectionOperation),
			"GetTrigram",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			false
		},
		new object[5]
		{
			typeof(WorldPatrolAppOperation),
			"RunRouteAsync",
			OperationNodeNotifyTiming.CurrentDone,
			true,
			true
		}
	};

	[Theory]
	[MemberData("PythonNodeNotifications", new object[] { })]
	public void ApplicationNode_UsesPythonNotificationMetadata(Type operationType, string methodName, OperationNodeNotifyTiming timing, bool sendImage, bool detail)
	{
		MethodInfo method = operationType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.NotNull(method);
		Assert.Contains(method.GetCustomAttributes<OperationNodeNotifyAttribute>(), (OperationNodeNotifyAttribute annotation) => annotation.Timing == timing && annotation.SendImage == sendImage && annotation.Detail == detail);
	}

	[Fact]
	public void WitheredDomainCompletion_UsesPythonPreviousDoneNodeGraph()
	{
		Type type = typeof(LostVoidApp).Assembly.GetType("ZzzOd.GameLogic.Application.HollowZero.WitheredDomain.WitheredDomainFinishOperation");
		IReadOnlyDictionary<string, MethodInfo> readOnlyDictionary = (from method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select new
			{
				Method = method,
				Node = method.GetCustomAttribute<OperationNodeAttribute>()
			} into item
			where item.Node != null
			select item).ToDictionary(item => item.Node.Name, item => item.Method);
		Assert.Equal(new string[2] { "完成后等待加载", "完成" }, readOnlyDictionary.Keys);
		Assert.True(readOnlyDictionary["完成后等待加载"].GetCustomAttribute<OperationNodeAttribute>().IsStartNode);
		Assert.Contains(readOnlyDictionary["完成"].GetCustomAttributes<NodeFromAttribute>(), (NodeFromAttribute edge) => edge.FromName == "完成后等待加载");
		Assert.Contains(readOnlyDictionary["完成"].GetCustomAttributes<OperationNodeNotifyAttribute>(), (OperationNodeNotifyAttribute annotation) => annotation.Timing == OperationNodeNotifyTiming.PreviousDone && annotation.Detail && annotation.SendImage);
	}
}

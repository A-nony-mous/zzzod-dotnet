using System;
using System.Collections.Generic;
using OneDragon.Core.Abstractions.Operations;
using ZzzOd.GameLogic.Application.BattleAssistant.AutoBattle;
using ZzzOd.GameLogic.Application.BattleAssistant.DodgeAssistant;
using ZzzOd.GameLogic.Application.ChargePlan;
using ZzzOd.GameLogic.Application.CityFund;
using ZzzOd.GameLogic.Application.Coffee;
using ZzzOd.GameLogic.Application.CommissionAssistant;
using ZzzOd.GameLogic.Application.Devtools.OperationDebug;
using ZzzOd.GameLogic.Application.Devtools.ScreenshotHelper;
using ZzzOd.GameLogic.Application.DriveDiscDismantle;
using ZzzOd.GameLogic.Application.EmailApp;
using ZzzOd.GameLogic.Application.EngagementReward;
using ZzzOd.GameLogic.Application.GameConfigChecker.MouseSensitivityChecker;
using ZzzOd.GameLogic.Application.GameConfigChecker.PredefinedTeamChecker;
using ZzzOd.GameLogic.Application.HollowZero.LostVoid;
using ZzzOd.GameLogic.Application.HollowZero.WitheredDomain;
using ZzzOd.GameLogic.Application.HouHouBakery;
using ZzzOd.GameLogic.Application.IntelBoard;
using ZzzOd.GameLogic.Application.LifeOnLine;
using ZzzOd.GameLogic.Application.Notify;
using ZzzOd.GameLogic.Application.NotoriousHunt;
using ZzzOd.GameLogic.Application.OneDragonApp;
using ZzzOd.GameLogic.Application.RandomPlay;
using ZzzOd.GameLogic.Application.RedemptionCode;
using ZzzOd.GameLogic.Application.RiduWeekly;
using ZzzOd.GameLogic.Application.ScratchCard;
using ZzzOd.GameLogic.Application.ShiyuDefense;
using ZzzOd.GameLogic.Application.SuibianTemple;
using ZzzOd.GameLogic.Application.TrigramsCollection;
using ZzzOd.GameLogic.Application.WorldPatrol;
using ZzzOd.GameLogic.Const;
using ZzzOd.GameLogic.Context;

namespace ZzzOd.GameLogic.Application;

/// <summary>
/// 应用 factory 注册表。
/// </summary>
public sealed class ApplicationFactoryRegistry
{
	private readonly ZContext _context;

	public IReadOnlyList<string> RegisteredAppIds => ZzzApplicationIds.All;

	public IReadOnlyList<ZApplicationDirectoryMetadata> BuiltInApplicationDirectories => ZApplicationDirectoryCatalog.BuiltInDirectories;

	public ApplicationFactoryRegistry(ZContext context)
	{
		_context = context;
	}

	public IApplicationFactory CreateFactory(string appId)
	{
		if (1 == 0)
		{
		}
		IApplicationFactory result = appId switch
		{
			"auto_battle" => CreateAutoBattleFactory(), 
			"charge_plan" => CreateChargePlanFactory(), 
			"city_fund" => CreateCityFundFactory(), 
			"coffee" => CreateCoffeeFactory(), 
			"commission_assistant" => CreateCommissionAssistantFactory(), 
			"dodge_assistant" => CreateDodgeAssistantFactory(), 
			"drive_disc_dismantle" => CreateDriveDiscDismantleFactory(), 
			"email" => CreateEmailFactory(), 
			"engagement_reward" => CreateEngagementRewardFactory(), 
			"hou_hou_bakery" => CreateHouHouBakeryFactory(), 
			"intel_board" => CreateIntelBoardFactory(), 
			"life_on_line" => CreateLifeOnLineFactory(), 
			"lost_void" => CreateLostVoidFactory(), 
			"mouse_sensitivity_checker" => CreateMouseSensitivityCheckerFactory(), 
			"notorious_hunt" => CreateNotoriousHuntFactory(), 
			"notify" => CreateNotifyFactory(), 
			"one_dragon" => CreateOneDragonFactory(), 
			"operation_debug" => CreateOperationDebugFactory(), 
			"predefined_team_checker" => CreatePredefinedTeamCheckerFactory(), 
			"random_play" => CreateRandomPlayFactory(), 
			"redemption_code" => CreateRedemptionCodeFactory(), 
			"ridu_weekly" => CreateRiduWeeklyFactory(), 
			"scratch_card" => CreateScratchCardFactory(), 
			"screenshot_helper" => CreateScreenshotHelperFactory(), 
			"shiyu_defense" => CreateShiyuDefenseFactory(), 
			"suibian_temple" => CreateSuibianTempleFactory(), 
			"trigrams_collection" => CreateTrigramsCollectionFactory(), 
			"withered_domain" => CreateWitheredDomainFactory(), 
			"world_patrol" => CreateWorldPatrolFactory(), 
			_ => throw new InvalidOperationException("未知内置应用 " + appId), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public void RegisterBuiltInApplications()
	{
		foreach (ZApplicationDirectoryMetadata builtInApplicationDirectory in BuiltInApplicationDirectories)
		{
			foreach (string appId in builtInApplicationDirectory.AppIds)
			{
				RegisterApplication(CreateFactory(appId), builtInApplicationDirectory.DefaultGroup);
			}
		}
	}

	public ZOneDragonAppFactory CreateOneDragonFactory()
	{
		return new ZOneDragonAppFactory(_context);
	}

	public AutoBattleAppFactory CreateAutoBattleFactory()
	{
		return new AutoBattleAppFactory(_context);
	}

	public DodgeAssistantFactory CreateDodgeAssistantFactory()
	{
		return new DodgeAssistantFactory(_context);
	}

	public ScreenshotHelperAppFactory CreateScreenshotHelperFactory()
	{
		return new ScreenshotHelperAppFactory(_context);
	}

	public OperationDebugAppFactory CreateOperationDebugFactory()
	{
		return new OperationDebugAppFactory(_context);
	}

	public EmailAppFactory CreateEmailFactory()
	{
		return new EmailAppFactory(_context);
	}

	public ChargePlanAppFactory CreateChargePlanFactory()
	{
		return new ChargePlanAppFactory(_context);
	}

	public CoffeeAppFactory CreateCoffeeFactory()
	{
		return new CoffeeAppFactory(_context);
	}

	public CommissionAssistantFactory CreateCommissionAssistantFactory()
	{
		return new CommissionAssistantFactory(_context);
	}

	public CityFundAppFactory CreateCityFundFactory()
	{
		return new CityFundAppFactory(_context);
	}

	public ScratchCardFactory CreateScratchCardFactory()
	{
		return new ScratchCardFactory(_context);
	}

	public EngagementRewardAppFactory CreateEngagementRewardFactory()
	{
		return new EngagementRewardAppFactory(_context);
	}

	public RedemptionCodeFactory CreateRedemptionCodeFactory()
	{
		return new RedemptionCodeFactory(_context);
	}

	public NotoriousHuntAppFactory CreateNotoriousHuntFactory()
	{
		return new NotoriousHuntAppFactory(_context);
	}

	public ShiyuDefenseAppFactory CreateShiyuDefenseFactory()
	{
		return new ShiyuDefenseAppFactory(_context);
	}

	public RiduWeeklyAppFactory CreateRiduWeeklyFactory()
	{
		return new RiduWeeklyAppFactory(_context);
	}

	public IntelBoardAppFactory CreateIntelBoardFactory()
	{
		return new IntelBoardAppFactory(_context);
	}

	public DriveDiscDismantleAppFactory CreateDriveDiscDismantleFactory()
	{
		return new DriveDiscDismantleAppFactory(_context);
	}

	public PredefinedTeamCheckerFactory CreatePredefinedTeamCheckerFactory()
	{
		return new PredefinedTeamCheckerFactory(_context);
	}

	public MouseSensitivityCheckerFactory CreateMouseSensitivityCheckerFactory()
	{
		return new MouseSensitivityCheckerFactory(_context);
	}

	public HouHouBakeryFactory CreateHouHouBakeryFactory()
	{
		return new HouHouBakeryFactory(_context);
	}

	public LifeOnLineAppFactory CreateLifeOnLineFactory()
	{
		return new LifeOnLineAppFactory(_context);
	}

	public NotifyAppFactory CreateNotifyFactory()
	{
		return new NotifyAppFactory(_context);
	}

	public RandomPlayAppFactory CreateRandomPlayFactory()
	{
		return new RandomPlayAppFactory(_context);
	}

	public TrigramsCollectionFactory CreateTrigramsCollectionFactory()
	{
		return new TrigramsCollectionFactory(_context);
	}

	public SuibianTempleFactory CreateSuibianTempleFactory()
	{
		return new SuibianTempleFactory(_context);
	}

	public WorldPatrolFactory CreateWorldPatrolFactory()
	{
		return new WorldPatrolFactory(_context);
	}

	public WitheredDomainAppFactory CreateWitheredDomainFactory()
	{
		return new WitheredDomainAppFactory(_context);
	}

	public LostVoidAppFactory CreateLostVoidFactory()
	{
		return new LostVoidAppFactory(_context);
	}

	public void RegisterOneDragonApplication()
	{
		RegisterApplication(CreateOneDragonFactory());
	}

	public void RegisterAutoBattleApplication()
	{
		RegisterApplication(CreateAutoBattleFactory());
	}

	public void RegisterDodgeAssistantApplication()
	{
		RegisterApplication(CreateDodgeAssistantFactory());
	}

	public void RegisterScreenshotHelperApplication()
	{
		RegisterApplication(CreateScreenshotHelperFactory());
	}

	public void RegisterOperationDebugApplication()
	{
		RegisterApplication(CreateOperationDebugFactory());
	}

	public void RegisterEmailApplication()
	{
		RegisterApplication(CreateEmailFactory(), defaultGroup: true);
	}

	public void RegisterChargePlanApplication()
	{
		RegisterApplication(CreateChargePlanFactory(), defaultGroup: true);
	}

	public void RegisterCoffeeApplication()
	{
		RegisterApplication(CreateCoffeeFactory(), defaultGroup: true);
	}

	public void RegisterCommissionAssistantApplication()
	{
		RegisterApplication(CreateCommissionAssistantFactory());
	}

	public void RegisterCityFundApplication()
	{
		RegisterApplication(CreateCityFundFactory(), defaultGroup: true);
	}

	public void RegisterScratchCardApplication()
	{
		RegisterApplication(CreateScratchCardFactory(), defaultGroup: true);
	}

	public void RegisterEngagementRewardApplication()
	{
		RegisterApplication(CreateEngagementRewardFactory(), defaultGroup: true);
	}

	public void RegisterRedemptionCodeApplication()
	{
		RegisterApplication(CreateRedemptionCodeFactory(), defaultGroup: true);
	}

	public void RegisterNotoriousHuntApplication()
	{
		RegisterApplication(CreateNotoriousHuntFactory(), defaultGroup: true);
	}

	public void RegisterShiyuDefenseApplication()
	{
		RegisterApplication(CreateShiyuDefenseFactory(), defaultGroup: true);
	}

	public void RegisterRiduWeeklyApplication()
	{
		RegisterApplication(CreateRiduWeeklyFactory(), defaultGroup: true);
	}

	public void RegisterIntelBoardApplication()
	{
		RegisterApplication(CreateIntelBoardFactory(), defaultGroup: true);
	}

	public void RegisterDriveDiscDismantleApplication()
	{
		RegisterApplication(CreateDriveDiscDismantleFactory(), defaultGroup: true);
	}

	public void RegisterPredefinedTeamCheckerApplication()
	{
		RegisterApplication(CreatePredefinedTeamCheckerFactory());
	}

	public void RegisterMouseSensitivityCheckerApplication()
	{
		RegisterApplication(CreateMouseSensitivityCheckerFactory());
	}

	public void RegisterHouHouBakeryApplication()
	{
		RegisterApplication(CreateHouHouBakeryFactory(), defaultGroup: true);
	}

	public void RegisterLifeOnLineApplication()
	{
		RegisterApplication(CreateLifeOnLineFactory(), defaultGroup: true);
	}

	public void RegisterNotifyApplication()
	{
		RegisterApplication(CreateNotifyFactory(), defaultGroup: true);
	}

	public void RegisterRandomPlayApplication()
	{
		RegisterApplication(CreateRandomPlayFactory(), defaultGroup: true);
	}

	public void RegisterTrigramsCollectionApplication()
	{
		RegisterApplication(CreateTrigramsCollectionFactory(), defaultGroup: true);
	}

	public void RegisterSuibianTempleApplication()
	{
		RegisterApplication(CreateSuibianTempleFactory(), defaultGroup: true);
	}

	public void RegisterWorldPatrolApplication()
	{
		RegisterApplication(CreateWorldPatrolFactory(), defaultGroup: true);
	}

	public void RegisterWitheredDomainApplication()
	{
		RegisterApplication(CreateWitheredDomainFactory(), defaultGroup: true);
	}

	public void RegisterLostVoidApplication()
	{
		RegisterApplication(CreateLostVoidFactory(), defaultGroup: true);
	}

	public void RegisterApplication(IApplicationFactory factory, bool defaultGroup = false)
	{
		_context.RunContext.RegisterApplication(factory, defaultGroup);
	}

	public void RegisterApplications(IEnumerable<IApplicationFactory> factories, bool defaultGroup = false)
	{
		_context.RunContext.RegisterApplications(factories, defaultGroup);
	}

	public void ClearApplications()
	{
		_context.RunContext.ClearApplications();
	}
}

using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

public sealed class AppCompletionContractTests
{
    public static TheoryData<string, string> NormalWorldApplications => new()
    {
        { "coffee", "ZzzOd.GameLogic.Application.Coffee/CoffeeOperation.cs" },
        { "scratch_card", "ZzzOd.GameLogic.Application.ScratchCard/ScratchCardOperation.cs" },
        { "charge_plan", "ZzzOd.GameLogic.Application.ChargePlan/ChargePlanOperation.cs" },
        { "suibian_temple", "ZzzOd.GameLogic.Application.SuibianTemple/SuibianTempleOperation.cs" },
        { "email", "ZzzOd.GameLogic.Application.EmailApp/EmailOperation.cs" },
        { "redemption_code", "ZzzOd.GameLogic.Application.RedemptionCode/RedemptionCodeOperation.cs" },
        { "random_play", "ZzzOd.GameLogic.Application.RandomPlay/DefaultRandomPlayOperationServices.cs" },
        { "trigrams_collection", "ZzzOd.GameLogic.Application.TrigramsCollection/DefaultTrigramsCollectionOperationServices.cs" },
        { "notorious_hunt", "ZzzOd.GameLogic.Application.NotoriousHunt/NotoriousHuntOperation.cs" },
        { "engagement_reward", "ZzzOd.GameLogic.Application.EngagementReward/EngagementRewardOperation.cs" },
        { "withered_domain", "ZzzOd.GameLogic.Application.HollowZero.WitheredDomain/DefaultWitheredDomainAppActions.cs" },
        { "ridu_weekly", "ZzzOd.GameLogic.Application.RiduWeekly/RiduWeeklyOperation.cs" },
        { "drive_disc_dismantle", "ZzzOd.GameLogic.Application.DriveDiscDismantle/DefaultDriveDiscDismantleOperationServices.cs" },
        { "lost_void", "ZzzOd.GameLogic.Application.HollowZero.LostVoid/LostVoidAppOperation.cs" },
        { "city_fund", "ZzzOd.GameLogic.Application.CityFund/CityFundOperation.cs" },
        { "world_patrol", "ZzzOd.GameLogic.Application.WorldPatrol/WorldPatrolAppOperation.cs" },
        { "life_on_line", "ZzzOd.GameLogic.Application.LifeOnLine/DefaultLifeOnLineOperationServices.cs" },
        { "shiyu_defense", "ZzzOd.GameLogic.Application.ShiyuDefense/DefaultShiyuDefenseOperationServices.cs" },
        { "hou_hou_bakery", "ZzzOd.GameLogic.Application.HouHouBakery/DefaultHouHouBakeryOperationServices.cs" },
    };

    [Theory]
    [MemberData(nameof(NormalWorldApplications))]
    public void NormalWorldApplicationCompletionPathReferencesBackToNormalWorld(string appId, string relativePath)
    {
        Assert.NotEmpty(appId);
        string source = File.ReadAllText(Path.Combine(FindGameLogicRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("BackToNormalWorld", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ZzzOd.GameLogic.Application.IntelBoard/IntelBoardOperation.cs")]
    [InlineData("ZzzOd.GameLogic.Application.Notify/NotifyApp.cs")]
    public void SpecialCompletionPathDoesNotReferenceBackToNormalWorld(string relativePath)
    {
        string source = File.ReadAllText(Path.Combine(FindGameLogicRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("BackToNormalWorld", source, StringComparison.Ordinal);
    }

    private static string FindGameLogicRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string root = Path.Combine(directory.FullName, "zzzod-dotnet", "src", "ZzzOd.GameLogic");
            if (Directory.Exists(root))
            {
                return root;
            }
        }

        throw new DirectoryNotFoundException("未找到 ZzzOd.GameLogic 源码目录。");
    }
}

using Xunit;

namespace ZzzOd.GameLogic.Tests.Audit;

[Trait("Category", "Audit")]
public sealed class AppCompletionContractTests
{
    public static TheoryData<string> NormalWorldApplications => new()
    {
        "ZzzOd.GameLogic.Application.Coffee/CoffeeOperation.cs",
        "ZzzOd.GameLogic.Application.ScratchCard/ScratchCardOperation.cs",
        "ZzzOd.GameLogic.Application.ChargePlan/ChargePlanOperation.cs",
        "ZzzOd.GameLogic.Application.SuibianTemple/SuibianTempleOperation.cs",
        "ZzzOd.GameLogic.Application.EmailApp/EmailOperation.cs",
        "ZzzOd.GameLogic.Application.RedemptionCode/RedemptionCodeOperation.cs",
        "ZzzOd.GameLogic.Application.RandomPlay/DefaultRandomPlayOperationServices.cs",
        "ZzzOd.GameLogic.Application.TrigramsCollection/DefaultTrigramsCollectionOperationServices.cs",
        "ZzzOd.GameLogic.Application.NotoriousHunt/NotoriousHuntOperation.cs",
        "ZzzOd.GameLogic.Application.EngagementReward/EngagementRewardOperation.cs",
        "ZzzOd.GameLogic.Application.HollowZero.WitheredDomain/DefaultWitheredDomainAppActions.cs",
        "ZzzOd.GameLogic.Application.RiduWeekly/RiduWeeklyOperation.cs",
        "ZzzOd.GameLogic.Application.DriveDiscDismantle/DefaultDriveDiscDismantleOperationServices.cs",
        "ZzzOd.GameLogic.Application.HollowZero.LostVoid/LostVoidAppOperation.cs",
        "ZzzOd.GameLogic.Application.CityFund/CityFundOperation.cs",
        "ZzzOd.GameLogic.Application.WorldPatrol/WorldPatrolAppOperation.cs",
        "ZzzOd.GameLogic.Application.LifeOnLine/DefaultLifeOnLineOperationServices.cs",
        "ZzzOd.GameLogic.Application.ShiyuDefense/DefaultShiyuDefenseOperationServices.cs",
        "ZzzOd.GameLogic.Application.HouHouBakery/DefaultHouHouBakeryOperationServices.cs",
    };

    [Theory]
    [MemberData(nameof(NormalWorldApplications))]
    public void NormalWorldApplicationCompletionPathReferencesBackToNormalWorld(string relativePath)
    {
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

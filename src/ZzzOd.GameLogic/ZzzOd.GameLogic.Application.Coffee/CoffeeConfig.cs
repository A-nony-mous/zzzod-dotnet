using OneDragon.Core.Abstractions.Operations;
using OneDragon.Core.Configuration;
using OneDragon.Core.Runtime;
using YamlDotNet.Serialization;

namespace ZzzOd.GameLogic.Application.Coffee;

/// <summary>
/// 咖啡店应用配置。
/// </summary>
public sealed class CoffeeConfig : ZApplicationConfig, IApplicationConfig
{
	[YamlMember(Alias = "transport_point", ApplyNamingConventions = false)]
	public string TransportPoint { get; set; } = CoffeeTransportPoint.SixthStreet.Value;

	[YamlMember(Alias = "choose_way", ApplyNamingConventions = false)]
	public string ChooseWay { get; set; } = "优先体力计划";

	[YamlMember(Alias = "challenge_way", ApplyNamingConventions = false)]
	public string ChallengeWay { get; set; } = "全都挑战";

	[YamlMember(Alias = "card_num", ApplyNamingConventions = false)]
	public string CardNum { get; set; } = "1";

	[YamlMember(Alias = "auto_battle", ApplyNamingConventions = false)]
	public string AutoBattle { get; set; } = "全配队通用";

	[YamlMember(Alias = "day_coffee_1", ApplyNamingConventions = false)]
	public string DayCoffee1 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "day_coffee_2", ApplyNamingConventions = false)]
	public string DayCoffee2 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "day_coffee_3", ApplyNamingConventions = false)]
	public string DayCoffee3 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "day_coffee_4", ApplyNamingConventions = false)]
	public string DayCoffee4 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "day_coffee_5", ApplyNamingConventions = false)]
	public string DayCoffee5 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "day_coffee_6", ApplyNamingConventions = false)]
	public string DayCoffee6 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "day_coffee_7", ApplyNamingConventions = false)]
	public string DayCoffee7 { get; set; } = "汀曼特调";

	[YamlMember(Alias = "predefined_team_idx", ApplyNamingConventions = false)]
	public int PredefinedTeamIndex { get; set; } = -1;

	[YamlMember(Alias = "run_charge_plan_afterwards", ApplyNamingConventions = false)]
	public bool RunChargePlanAfterwards { get; set; }

	/// <summary>
	/// 加载 BaselineParity 兼容配置。
	/// </summary>
	public static CoffeeConfig Load(OneDragonEnvironment environment, int instanceIndex, string groupId)
	{
		YamlConfig<CoffeeConfig> yamlConfig = new YamlConfig<CoffeeConfig>(environment, "coffee", null, instanceIndex, new string[2] { "app_config", groupId });
		CoffeeConfig current = yamlConfig.Current;
		current.TransportPoint = CoffeeTransportPoint.FromValue(current.TransportPoint).Value;
		current.ConfigureRuntime("coffee", instanceIndex, groupId);
		return current;
	}

	/// <summary>
	/// 按星期读取配置咖啡名，星期一为 1，星期日为 7。
	/// </summary>
	public string GetCoffeeByDay(int day)
	{
		if (1 == 0)
		{
		}
		string result = day switch
		{
			1 => DayCoffee1, 
			2 => DayCoffee2, 
			3 => DayCoffee3, 
			4 => DayCoffee4, 
			5 => DayCoffee5, 
			6 => DayCoffee6, 
			7 => DayCoffee7, 
			_ => DayCoffee1, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}

using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Scratch;

internal static class Program
{
    private static void Main()
    {
        IDeserializer deserializer = new DeserializerBuilder().Build();
        const string yaml = """
            plan_list:
            - tab_name: 体力计划
              category_name: 实战模拟室
              mission_type_name: 定期清剿
              mission_name: 基础材料
              level: 默认等级
            """;
        Dictionary<string, object> result = deserializer.Deserialize<Dictionary<string, object>>(yaml);
        object normalized = result["plan_list"];
        ISerializer serializer = new SerializerBuilder().Build();
        string serialized = serializer.Serialize(normalized);
        Console.WriteLine(serialized);
        List<ZzzOd.GameLogic.Application.ChargePlan.ChargePlanItem>? list = deserializer.Deserialize<List<ZzzOd.GameLogic.Application.ChargePlan.ChargePlanItem>>(serialized);
        Console.WriteLine(list?.Count);
    }
}

using System.Collections.Generic;

namespace ExplorersIcebox.Util;

/// <summary>
/// 內嵌路線（EmbedRoutes.Routes）的顯示名稱在地化。
/// 路線 key 是作者手寫、混編號與英文物品名的複合字串（例："7: Clay/Sand [Ground XP Route]"），
/// 沒有對應的遊戲 sheet 可反查，因此以固定映射逐條翻成台服官方物品名。
/// 🔴 這只做「顯示」用。原始 key 仍是 EmbedRoutes.Routes 的字典鍵、也是選單索引比對的依據，
///     絕不可改動；查不到對照時原樣退回英文 key（可見、不留白）。
/// </summary>
public static class RouteNameLoc
{
    private static readonly Dictionary<string, string> Display = new()
    {
        { "0: IsleFish/Clam Shell | Larve/Squid", "0: 海水魚/雙殼貝 | 球藻/烏賊" },
        { "1: Islewort | (Popoto/Parsnip Seeds)", "1: 草葉 |（新薯/高山蘿蔔種子）" },
        { "2: Sugarcane | Vine", "2: 甘蔗 | 藤蔓" },
        { "3: Tinsand/Sand | Marble/Limestone", "3: 錫砂/砂礫 | 大理石/石灰岩" },
        { "4: Apple | Beehive", "4: 蘋果 | 蜂巢" },
        { "5: Coconut/Palm Log/Leaf | Marble/Limestone", "5: 椰子/棕櫚原木/棕櫚葉 | 大理石/石灰岩" },
        { "6: Cotton", "6: 棉花" },
        { "7: Clay/Sand [Ground XP Route]", "7: 黏土/砂礫〔地面經驗路線〕" },
        { "8: Marble/Limestone", "8: 大理石/石灰岩" },
        { "9: Branch | Log | Resin", "9: 小樹枝 | 原木 | 天然樹脂" },
        { "10: Copper | Mythril", "10: 銅礦 | 靈銀礦" },
        { "11: Opal | Sap | (Log)", "11: 木化石 | 樹液 |（原木）" },
        { "12: Hemp", "12: 麻" },
        { "13: Multicolored Isleblooms | Quartz", "13: 色彩繽紛的花 | 石英" },
        { "14: Iron Ore", "14: 鐵礦" },
        { "15: Laver / Squid | Jellyfish / Coral", "15: 球藻 / 烏賊 | 水母 / 珊瑚" },
        { "16: Rocksalt", "16: 岩鹽" },
        { "17: Leucogranite", "17: 花崗岩" },
        { "18: Quartz [Mountain XP Loop]", "18: 石英〔高山經驗循環〕" },
        { "19: Coal / Shale | Glimshroom", "19: 石炭 / 堆積岩 | 燈火茸" },
        { "20: Effervescent Water", "20: 氣泡水" },
        { "21. Crystal / Hawk Sand | Yelow Copper / Gold Ore", "21. 水晶層 / 鷹眼砂 | 黃銅礦 / 金礦" },
        { "22: Cabbage Seeds", "22: 甘藍種子" },
        { "23. Pumpkin Seeds", "23. 南瓜種子" },
    };

    /// <summary>回傳路線 key 的中文顯示名；查無對照時原樣退回原字串。</summary>
    public static string Translate(string routeKey)
        => Display.TryGetValue(routeKey, out var zh) ? zh : routeKey;
}

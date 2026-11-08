namespace Sanguosha.Core
{
    public static partial class Heroes
    {
        private static IEnumerable<Hero> CreateRefreshed()
        {
            yield return new("re_caocao", "界曹操", "魏", 4, "奸雄 · 护驾",
                "受到伤害后，可获得造成伤害的实体牌并摸一张。主公技：可令其他魏势力角色替你打出闪；每回合限一次，其他魏势力角色在其回合外使用/打出闪后，可令你摸一张。",
                "宁教我负天下人！", 33, "界限突破·标准", "new_rejianxiong/rehujia", "caocao");
            yield return new("re_huanggai", "界黄盖", "吴", 4, "苦肉 · 诈降",
                "苦肉：出牌阶段限一次，弃置一张手牌或装备并失去1点体力。诈降：每失去1点体力后摸三张；若在自己的出牌阶段，此阶段使用杀的次数+1，红色杀无距离限制且不能被闪响应。",
                "铁索连舟，火起万船！", 34, "界限突破·标准", "rekurou/zhaxiang", "huanggai");
            yield return new("re_diaochan", "界貂蝉", "群", 3, "离间 · 闭月",
                "离间：出牌阶段限一次，弃置一张手牌或装备，令两名男性角色依次进行无实体牌的决斗。闭月：结束阶段可以摸一张牌，若没有手牌则改为摸两张。",
                "失礼了。", 35, "界限突破·标准", "lijian/rebiyue", "diaochan");
            yield return new("re_zhouyu", "界周瑜", "吴", 3, "英姿 · 反间",
                "英姿：锁定技，摸牌阶段多摸一张，手牌上限为体力上限。反间：出牌阶段限一次，交给其他角色一张手牌，令其选择：展示手牌并弃置所有与该牌同花色的手牌/装备，或失去1点体力。",
                "谈笑间，樯橹灰飞烟灭。", 36, "界限突破·标准", "reyingzi/refanjian", "zhouyu");
            yield return new("zhanghe", "张郃", "魏", 4, "巧变",
                "判定、摸牌、出牌、弃牌阶段开始前，可弃置一张手牌并跳过该阶段。跳过摸牌时，可从至多两名其他角色各获得一张手牌；跳过出牌时，可移动场上一张装备/判定牌至合法区域。",
                "兵无常势，水无常形。", 37, "神话·山", "经典·qiaobian");
        }
    }
}

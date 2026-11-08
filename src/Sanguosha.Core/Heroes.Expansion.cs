namespace Sanguosha.Core
{
    public static partial class Heroes
    {
        // Classic, individually identified rule variants; not an alias for the latest re_* roster.
        // See docs/EXPANSION_BATCH1.md for the upstream skill IDs and deferred mechanics.
        private static IEnumerable<Hero> CreateExpansion()
        {
            yield return new("huaxiong", "华雄", "群", 6, "耀武",
                "锁定技：当你受到红色【杀】造成的伤害时，伤害来源选择回复1点体力或摸一张牌。",
                "关东鼠辈，谁敢出战？", 25, "标准补充", "经典·yaowu");
            yield return new("huangzhong", "黄忠", "蜀", 4, "烈弓",
                "经典烈弓：出牌阶段使用【杀】时，若目标手牌数≥你的体力值，或≤你的攻击范围，你可以令此【杀】不能被响应。",
                "百步穿杨！", 26, "神话·风", "经典·liegong");
            yield return new("weiyan", "魏延", "蜀", 4, "狂骨",
                "锁定技：造成伤害后，若造成伤害时你与受伤角色距离≤1，你回复等同伤害值的体力。",
                "谁敢杀我！", 27, "神话·风", "经典·kuanggu");
            yield return new("dianwei", "典韦", "魏", 4, "强袭",
                "出牌阶段限一次：失去1点体力，或弃置一张武器牌，对攻击范围内一名其他角色造成1点无牌伤害。选择弃置已装备武器时，按移除该武器后的攻击范围检查目标。",
                "吃我一戟！", 28, "神话·火", "经典·qiangxi");
            yield return new("pangde", "庞德", "群", 4, "马术 · 猛进",
                "锁定技：你计算与其他角色的距离-1。你使用的【杀】被【闪】抵消后，可以弃置该角色手牌区或装备区的一张牌。",
                "我要杀你们个片甲不留！", 29, "神话·火", "经典·mashu/mengjin");
            yield return new("yanliangwenchou", "颜良文丑", "群", 4, "双雄",
                "摸牌阶段，你可改为判定并获得判定牌；本回合可将与判定牌颜色不同的一张手牌当【决斗】使用。",
                "吾等兄弟，天下无敌！", 30, "神话·火", "经典·shuangxiong");
            yield return new("zhurong", "祝融", "蜀", 4, "巨象 · 烈刃",
                "锁定技：【南蛮入侵】对你无效，其他角色使用的南蛮结算后，你获得仍在弃牌堆中的实体牌。使用【杀】造成伤害后，可与受伤角色拼点；若赢，获得其手牌区或装备区的一张牌。",
                "尝尝本姑娘飞刀的厉害！", 31, "神话·林", "经典·juxiang/lieren");
            yield return new("menghuo", "孟获", "蜀", 4, "祸首 · 再起",
                "锁定技：【南蛮入侵】对你无效，你视为南蛮的伤害来源。摸牌阶段若受伤，可改为亮出已损失体力数的牌：每张红桃回复1点体力并弃置，其余获得。",
                "南蛮之地，皆我子民！", 32, "神话·林", "经典·huoshou/zaiqi");
        }
    }
}

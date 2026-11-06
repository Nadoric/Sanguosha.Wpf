namespace Sanguosha.Core
{
    public enum Role
    {
        Lord,
        Loyalist,
        Rebel,
        Renegade
    }

    public enum GameMode
    {
        Identity4,
        Identity5,
        Identity8,
        Team2V2
    }

    public enum CardKind
    {
        Slash,
        Dodge,
        Peach,
        Duel,
        DrawTwo,
        Dismantle,
        Steal,
        Barbarians,
        Arrows,
        Feast,
        Indulgence,
        Crossbow,
        Blade,
        EightTrigrams,
        Horse
    }

    public enum GamePhase
    {
        NotStarted,
        Resolving,
        Play,
        Response,
        Discard,
        GameOver
    }

    public enum ResponseKind
    {
        Dodge,
        Slash,
        Peach
    }

    public sealed record Hero(
        string Id,
        string Name,
        string Kingdom,
        int Health,
        string Skill,
        string Description,
        string Quote,
        int PortraitIndex);

    public static class Heroes
    {
        public static IReadOnlyList<Hero> All { get; } = new List<Hero>
        {
            new(
                "liubei",
                "刘备",
                "蜀",
                4,
                "仁德",
                "出牌阶段限一次：交给其他角色一张手牌，并回复1点体力。",
                "惟贤惟德，能服于人。",
                0),

            new(
                "guanyu",
                "关羽",
                "蜀",
                4,
                "武圣",
                "可将任意红色手牌当【杀】使用或打出。",
                "义薄云天，忠贯日月。",
                1),

            new(
                "zhangfei",
                "张飞",
                "蜀",
                4,
                "咆哮",
                "出牌阶段使用【杀】没有次数限制。",
                "燕人张翼德在此！",
                2),

            new(
                "zhaoyun",
                "赵云",
                "蜀",
                4,
                "龙胆",
                "可将【杀】当【闪】、【闪】当【杀】使用或打出。",
                "一身是胆，百战不殆。",
                3),

            new(
                "caocao",
                "曹操",
                "魏",
                4,
                "奸雄",
                "受到伤害并存活后，获得造成该伤害的牌（若仍在弃牌堆）。",
                "宁教我负天下人。",
                4),

            new(
                "sunquan",
                "孙权",
                "吴",
                4,
                "制衡",
                "出牌阶段限一次：弃置一张手牌，再摸一张牌。",
                "容我三思，再作定夺。",
                5),

            new(
                "simayi",
                "司马懿",
                "魏",
                3,
                "反馈",
                "每次受到伤害并存活后，获得伤害来源的一张牌（优先装备）。",
                "天命，也由我来定。",
                6),

            new(
                "xiahoudun",
                "夏侯惇",
                "魏",
                4,
                "刚烈",
                "受到伤害并存活后判定：非红桃时，来源弃两张手牌；不足则失去1点体力。",
                "鼠辈，竟敢伤我！",
                7),

            new(
                "zhangliao",
                "张辽",
                "魏",
                4,
                "突袭",
                "摸牌阶段改为从手牌最多的两名其他角色各获得一张手牌；不足两人时补摸。",
                "没想到吧！",
                8),

            new(
                "xuchu",
                "许褚",
                "魏",
                4,
                "裸衣",
                "摸牌阶段少摸一张；本回合杀与决斗造成的伤害+1。",
                "谁来与我大战三百回合！",
                9),

            new(
                "guojia",
                "郭嘉",
                "魏",
                3,
                "遗计",
                "每受到1点伤害并存活后，摸两张牌。",
                "就这样罢。",
                10),

            new(
                "zhenji",
                "甄姬",
                "魏",
                3,
                "洛神 · 倾国",
                "回合开始连续判定，获得所有黑色判定牌，红色时停止；黑色手牌可当闪。",
                "仿佛兮若轻云之蔽月。",
                11),

            new(
                "zhugeliang",
                "诸葛亮",
                "蜀",
                3,
                "观星 · 空城",
                "回合开始自动整理牌堆顶五张，将优先牌留作摸牌；没有手牌时不能成为杀、决斗的目标。",
                "知天易，逆天难。",
                12),

            new(
                "machao",
                "马超",
                "蜀",
                4,
                "马术 · 铁骑",
                "你计算与他人的距离-1；使用杀时判定，红色令目标不能出闪。",
                "全军突击！",
                13),

            new(
                "huangyueying",
                "黄月英",
                "蜀",
                3,
                "集智 · 奇才",
                "使用非延时锦囊后摸一张牌；锦囊不受距离限制。",
                "奇思妙想，尽在其中。",
                14),

            new(
                "ganning",
                "甘宁",
                "吴",
                4,
                "奇袭",
                "可勾选技能，将任意黑色手牌当过河拆桥使用。",
                "接招吧！",
                15),

            new(
                "lvmeng",
                "吕蒙",
                "吴",
                4,
                "克己",
                "本回合未使用或打出杀时，跳过弃牌阶段。",
                "留得青山在，不怕没柴烧。",
                16),

            new(
                "huanggai",
                "黄盖",
                "吴",
                4,
                "苦肉",
                "出牌阶段可反复发动：失去1点体力，存活后摸两张牌。",
                "请鞭笞我吧，公瑾！",
                17),

            new(
                "zhouyu",
                "周瑜",
                "吴",
                3,
                "英姿",
                "摸牌阶段额外摸一张牌。",
                "谈笑间，樯橹灰飞烟灭。",
                18),

            new(
                "daqiao",
                "大乔",
                "吴",
                3,
                "国色",
                "可勾选技能，将任意方块手牌当乐不思蜀使用。",
                "国色天香，倾国倾城。",
                19),

            new(
                "luxun",
                "陆逊",
                "吴",
                3,
                "谦逊 · 连营",
                "不能成为顺手牵羊或乐不思蜀的目标；失去最后一张手牌后摸一张。",
                "谦谦君子，温润如玉。",
                20),

            new(
                "sunshangxiang",
                "孙尚香",
                "吴",
                3,
                "结姻 · 枭姬",
                "出牌阶段限一次，交给一名受伤角色一张牌，你与其各回复1点体力；失去装备时摸两张牌。",
                "姻缘天定，弓马随心。",
                21),

            new(
                "lvbu",
                "吕布",
                "群",
                4,
                "无双",
                "你的杀需要两张闪抵消；与你决斗时，对方每轮须打出两张杀。",
                "谁能挡我！",
                22),

            new(
                "huatuo",
                "华佗",
                "群",
                3,
                "青囊 · 急救",
                "出牌阶段限一次，弃一张手牌令任意受伤角色回复1点体力；回合外红色手牌可当桃救援。",
                "救人一命，胜造七级浮屠。",
                23),

            new(
                "diaochan",
                "貂蝉",
                "群",
                3,
                "离间 · 闭月",
                "出牌阶段限一次，弃一张手牌，令目标与你指定的第二名角色决斗；回合结束摸一张牌。",
                "夫君，你要替妾身做主。",
                24)

        };

        public static Hero Find(string id)
        {
            return All.First(hero => hero.Id == id);
        }
    }

    public sealed record Card(int Id, CardKind Kind, string Suit, int Rank)
    {
        public bool IsRed => Suit is "♥" or "♦";

        public string RankText => Rank switch
        {
            1 => "A",
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => Rank.ToString()
        };

        public string Name => Kind switch
        {
            CardKind.Slash => "杀",
            CardKind.Dodge => "闪",
            CardKind.Peach => "桃",
            CardKind.Duel => "决斗",
            CardKind.DrawTwo => "无中生有",
            CardKind.Dismantle => "过河拆桥",
            CardKind.Steal => "顺手牵羊",
            CardKind.Barbarians => "南蛮入侵",
            CardKind.Arrows => "万箭齐发",
            CardKind.Feast => "桃园结义",
            CardKind.Indulgence => "乐不思蜀",
            CardKind.Crossbow => "诸葛连弩",
            CardKind.Blade => "青龙偃月刀",
            CardKind.EightTrigrams => "八卦阵",
            CardKind.Horse => "的卢",
            _ => "未知"
        };

        public bool IsEquipment => Kind >= CardKind.Crossbow;

        public string Category => IsEquipment ? "装备" : Kind <= CardKind.Peach ? "基本" : "锦囊";

        public string Description => Kind switch
        {
            CardKind.Slash => "对攻击范围内一名其他角色使用，需出闪，否则受到1点伤害。每回合限一次。",
            CardKind.Dodge => "响应杀或万箭齐发，抵消一次伤害。",
            CardKind.Peach => "出牌时回复自己1点体力；濒死时可救援任意角色。",
            CardKind.Duel => "与一名其他角色交替打出杀，首先无法响应者受到1点伤害。",
            CardKind.DrawTwo => "立即摸两张牌。",
            CardKind.Dismantle => "弃置一名其他角色的一张装备；无装备时随机弃置一张手牌。",
            CardKind.Steal => "获得距离1以内一名其他角色的一张装备；无装备时随机获得一张手牌。",
            CardKind.Barbarians => "其他所有角色依次打出一张杀，否则受到1点伤害。",
            CardKind.Arrows => "其他所有角色依次打出一张闪，否则受到1点伤害。",
            CardKind.Feast => "所有存活角色各回复1点体力。",
            CardKind.Indulgence => "延时锦囊 · 置于目标判定区。其下回合判定非红桃，跳过出牌阶段。",
            CardKind.Crossbow => "武器 · 攻击范围1。使用杀没有次数限制。",
            CardKind.Blade => "武器 · 攻击范围3。",
            CardKind.EightTrigrams => "防具 · 需要闪时自动判定：红色视为打出一张闪。",
            CardKind.Horse => "坐骑 · 其他角色计算与你的距离时+1。",
            _ => ""
        };
    }

    public sealed class Player
    {
        public int Seat { get; init; }
        public Hero Hero { get; init; } = Heroes.All[0];
        public Role Role { get; init; }
        public bool IsHuman { get; init; }
        public int Team { get; init; }
        public bool IsAlive { get; set; } = true;
        public int Health { get; set; }
        public int MaxHealth { get; init; }
        public List<Card> Hand { get; } = new();
        public List<Card> Equipment { get; } = new();
        public List<Card> Judgments { get; } = new();
        public int SlashCount { get; set; }
        public bool PlayedSlash { get; set; }
        public bool SkillUsed { get; set; }
        public int AttackRange => Equipment.Any(card => card.Kind == CardKind.Blade) ? 3 : 1;
        public string RoleName => Role switch
        {
            Role.Lord => "主公",
            Role.Loyalist => "忠臣",
            Role.Rebel => "反贼",
            _ => "内奸"
        };
    }

    public sealed record GameLog(int Number, string Text);

    public sealed record ResponseRequest(
        ResponseKind Kind,
        string Prompt,
        Player Responder,
        Action<Card?> Continue);
}

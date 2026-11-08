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
        Horse,
        SupplyShortage,
        IronChain,
        Nullification,
        FireAttack,
        Wine,
        Lightning
    }

    public enum GamePhase
    {
        NotStarted,
        Resolving,
        Play,
        Response,
        Discard,
        GameOver,
        SelectCard
    }

    public enum ResponseKind
    {
        Dodge,
        Slash,
        Peach
    }

    public enum CardZone
    {
        Hand,
        Equipment,
        Judgment
    }

    public sealed record SkillDecisionOption(string Key, string Label);

    public sealed record SkillDecisionRequest(
        string Prompt,
        IReadOnlyList<SkillDecisionOption> Options,
        Action<string> Continue);

    // HandPosition is an opaque face-down slot, NEVER a physical hand-card id.
    public sealed record TargetCardOption(CardZone Zone, int? CardId, string Label, int? HandPosition = null);

    public sealed record TargetCardRequest(
        int TargetSeat,
        bool Steal,
        IReadOnlyList<TargetCardOption> Options,
        Action Continue)
    {
        // Engine-only snapshot; no hidden identities in UI options / serialized public request.
        internal IReadOnlyList<int> HiddenHandIds { get; init; } = Array.Empty<int>();
    }

    public sealed record Hero(
        string Id,
        string Name,
        string Kingdom,
        int Health,
        string Skill,
        string Description,
        string Quote,
        int PortraitIndex,
        string Pack = "标准",
        string Ruleset = "标准经典·适配牌堆",
        string? BaseHeroId = null)
    {
        public bool IsFemale => (BaseHeroId ?? Id) is "huangyueying" or "zhenji" or "daqiao" or "sunshangxiang" or "diaochan" or "zhurong" or "xiaoqiao" or "caiwenji" or "ganfuren";
    }

    public static partial class Heroes
    {
        public static IReadOnlyList<Hero> All { get; } = new List<Hero>
        {
            new(
                "liubei",
                "刘备",
                "蜀",
                4,
                "仁德 · 激将",
                "出牌阶段可将手牌交给其他角色；本阶段累计交出至少两张时回复1点。主公可激将请其他蜀角色代打杀；未选手牌时点技能请求主动出杀。",
                "惟贤惟德，能服于人。",
                0),

            new(
                "guanyu",
                "关羽",
                "蜀",
                4,
                "武圣",
                "可将任意红色手牌或装备当【杀】使用或打出；使用已装备牌请点技能按钮选择装备转化。",
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
                "奸雄 · 护驾",
                "受伤后可获得伤害牌；身份局主公可护驾请其他魏角色代打闪。",
                "宁教我负天下人。",
                4),

            new(
                "sunquan",
                "孙权",
                "吴",
                4,
                "制衡 · 救援",
                "阶段一次，弃置任意数量手牌或装备再摸等量牌。主公救援：其他吴角色用桃救你时多回复1点。",
                "容我三思，再作定夺。",
                5),

            new(
                "simayi",
                "司马懿",
                "魏",
                3,
                "反馈 · 鬼才",
                "受伤后可获得来源一张手牌或装备；任意判定生效前可用一张手牌发动鬼才替换判定。",
                "天命，也由我来定。",
                6),

            new(
                "xiahoudun",
                "夏侯惇",
                "魏",
                4,
                "刚烈",
                "受伤后可判定，非红桃令来源选择弃两张手牌，或受到你造成的1点伤害。",
                "鼠辈，竟敢伤我！",
                7),

            new(
                "zhangliao",
                "张辽",
                "魏",
                4,
                "突袭",
                "摸牌阶段可放弃正常摸牌，改为从自选的至多两名其他角色各获得一张手牌；选一人不补摸。",
                "没想到吧！",
                8),

            new(
                "xuchu",
                "许褚",
                "魏",
                4,
                "裸衣",
                "摸牌阶段可少摸一张，若如此做，本回合杀或决斗造成的非传导伤害+1。",
                "谁来与我大战三百回合！",
                9),

            new(
                "guojia",
                "郭嘉",
                "魏",
                3,
                "天妒 · 遗计",
                "每点伤害后可摸两张，并将其中任意牌分配给其他角色；天妒可获得自己最终判定牌。",
                "就这样罢。",
                10),

            new(
                "zhenji",
                "甄姬",
                "魏",
                3,
                "洛神 · 倾国",
                "准备阶段可重复洛神判定并获得黑色判定牌，遇红停止，也可自行停止；黑色手牌可当闪。",
                "仿佛兮若轻云之蔽月。",
                11),

            new(
                "zhugeliang",
                "诸葛亮",
                "蜀",
                3,
                "观星 · 空城",
                "准备阶段观看牌堆顶X张（X为存活人数至多5），任意排序置顶/底；空手不能成为杀或决斗目标。",
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
                "英姿 · 反间",
                "摸牌阶段可额外摸一；反间阶段一次，让其他角色猜花色并随机得到你一张手牌，不符受1点伤害。",
                "谈笑间，樯橹灰飞烟灭。",
                18),

            new(
                "daqiao",
                "大乔",
                "吴",
                3,
                "国色 · 流离",
                "方块手牌或装备可当乐不思蜀；成为杀目标时可弃一手牌/装备，转移给攻击范围内除来源外的其他人。",
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
                "阶段一次，弃两张手牌，与你选择的受伤男性各回复1点；每失去一张装备摸两张。",
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
                "阶段一次，弃一张手牌令一名受伤角色回复1点；回合外红色手牌或装备可当桃救援。",
                "救人一命，胜造七级浮屠。",
                23),

            new(
                "diaochan",
                "貂蝉",
                "群",
                3,
                "离间 · 闭月",
                "阶段一次，弃一张手牌或装备令另外两名男性无实体牌决斗；结束阶段可摸一张。",
                "夫君，你要替妾身做主。",
                24)

        }.Concat(CreateExpansion()).Concat(CreateRefreshed()).Concat(CreateBatchTwo()).Concat(CreateBatchThree()).Concat(CreateBatchFour()).ToArray();

        public static Hero Find(string id)
        {
            return All.First(hero => hero.Id == id);
        }
    }

    public sealed record Card(int Id, CardKind Kind, string Suit, int Rank, DamageNature Nature = DamageNature.Normal)
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
            CardKind.Slash => Nature == DamageNature.Fire ? "火杀" : Nature == DamageNature.Thunder ? "雷杀" : "杀",
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
            CardKind.SupplyShortage => "兵粮寸断",
            CardKind.IronChain => "铁索连环",
            CardKind.Nullification => "无懈可击",
            CardKind.FireAttack => "火攻",
            CardKind.Wine => "酒",
            CardKind.Lightning => "闪电",
            _ => "未知"
        };

        public bool IsEquipment => Kind is CardKind.Crossbow or CardKind.Blade or CardKind.EightTrigrams or CardKind.Horse;

        public bool IsBasic => Kind is CardKind.Slash or CardKind.Dodge or CardKind.Peach or CardKind.Wine;
        public bool IsTrick => !IsBasic && !IsEquipment;
        public bool IsDelayed => Kind is CardKind.Indulgence or CardKind.SupplyShortage or CardKind.Lightning;
        public string Category => IsEquipment ? "装备" : IsBasic ? "基本" : "锦囊";

        public string Description => Kind switch
        {
            CardKind.Slash => Nature == DamageNature.Normal ? "对攻击范围内一名其他角色使用，需出闪，否则受到1点伤害。每回合限一次。" : $"造成{(Nature == DamageNature.Fire ? "火焰" : "雷电")}伤害；横置角色会传导伤害并重置。",
            CardKind.Dodge => "响应杀或万箭齐发，抵消一次伤害。",
            CardKind.Peach => "出牌时回复自己1点体力；濒死时可救援任意角色。",
            CardKind.Duel => "与一名其他角色交替打出杀，首先无法响应者受到1点伤害。",
            CardKind.DrawTwo => "立即摸两张牌。",
            CardKind.Dismantle => "选择并弃置一名其他角色手牌区、装备区或判定区的一张牌；手牌随机抽取。",
            CardKind.Steal => "获得距离1以内其他角色手牌区、装备区或判定区的一张牌；手牌随机抽取。",
            CardKind.Barbarians => "其他所有角色依次打出一张杀，否则受到1点伤害。",
            CardKind.Arrows => "其他所有角色依次打出一张闪，否则受到1点伤害。",
            CardKind.Feast => "所有存活角色各回复1点体力。",
            CardKind.Indulgence => "延时锦囊 · 置于目标判定区。其下回合判定非红桃，跳过出牌阶段。",
            CardKind.Crossbow => "武器 · 攻击范围1。使用杀没有次数限制。",
            CardKind.Blade => "武器 · 攻击范围3。",
            CardKind.EightTrigrams => "防具 · 需要闪时可判定：红色视为打出闪，可被鬼才改判。",
            CardKind.Horse => "坐骑 · 其他角色计算与你的距离时+1。",
            CardKind.SupplyShortage => "判定非梅花时，跳过摸牌阶段。",
            CardKind.IronChain => "选择至多两名角色，分别横置或重置；也可重铸摸一。",
            CardKind.Nullification => "抵消一次锦囊效果，其他角色可继续反无懈。",
            CardKind.FireAttack => "目标展示一张手牌；你可弃同花色手牌对其造成1点火焰伤害。",
            CardKind.Wine => "阶段限一次，下张杀伤害+1；自己濒死时可当桃自救。",
            CardKind.Lightning => "判定黑桃2至9：受到3点无来源雷电伤害；否则传给下一合法角色。",
            _ => ""
        };
    }

    public sealed partial class Player
    {
        public int Seat { get; init; }
        public Hero Hero { get; init; } = Heroes.All[0];
        public Role Role { get; init; }
        public bool IsHuman { get; init; }
        public int Team { get; init; }
        public bool IsAlive { get; set; } = true;
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public List<Card> Hand { get; } = new();
        public List<Card> Equipment { get; } = new();
        public List<Card> Judgments { get; } = new();
        public int SlashCount { get; set; }
        public bool PlayedSlash { get; set; }
        public bool SkillUsed { get; set; }
        public bool? DuelJudgmentRed { get; set; }
        public int ZhaXiangCount { get; set; }
        public bool HujiaDrawUsed { get; set; }
        public string RuleId => SkillsDisabled ? "" : SkillIdentity switch
        {
            "re_guojia" => "guojia", "re_zhangliao" => "zhangliao", "re_zhugeliang" => "zhugeliang",
            "re_huatuo" => "huatuo", "re_huangyueying" => "huangyueying", "re_liubei" => "liubei", "re_sunquan" => "sunquan", "re_zhaoyun" => "zhaoyun", "re_zhangfei" => "zhangfei", "re_ganning" => "ganning", "re_lvmeng" => "lvmeng", "re_guanyu" => "guanyu", "re_machao" => "machao", _ => SkillIdentity
        };
        public Dictionary<string, int> Marks { get; } = new();
        public Dictionary<string, List<Card>> NamedZones { get; } = new();
        public int Mark(string key) => Marks.GetValueOrDefault(key);
        public void SetMark(string key, int value) { if (value == 0) Marks.Remove(key); else Marks[key] = value; }
        public List<Card> Zone(string key) { if (!NamedZones.TryGetValue(key, out List<Card>? zone)) NamedZones[key] = zone = new(); return zone; }
        public IEnumerable<Card> NamedCards => NamedZones.Values.SelectMany(cards => cards);
        public bool TurnedOver { get; set; }
        public bool Chained { get; set; }
        public bool Awakened { get; set; }
        public bool LimitedUsed { get; set; }
        public bool SkillsDisabled { get; set; }
        public bool LuoyiActive { get; set; }
        public int TianyiState { get; set; }
        public int RendeGiven { get; set; }
        public bool InPlayPhase { get; set; }
        public bool JijiangFailed { get; set; }
        public List<Card> SpecialCards { get; } = new();
        public HashSet<int> LuoshenCards { get; } = new();
        public HashSet<int> SupplyShortages { get; } = new();
        internal Func<int>? LordHandBonus { get; set; }
        public int HandLimit => Math.Max(0, (HasAbility("英姿") && (SkillIdentity == "re_zhouyu" || RuleId == "sunce" && Awakened) ? MaxHealth : Health) +
            Hand.Count(card => LuoshenCards.Contains(card.Id)) + Mark("handBonus") + Mark("qingjianBonus") + (LordHandBonus?.Invoke() ?? Mark("xueyiBonus")) - Mark("wangzunPenalty"));
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

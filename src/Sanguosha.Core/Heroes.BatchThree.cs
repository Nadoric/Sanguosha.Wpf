namespace Sanguosha.Core;
public static partial class Heroes
{
    private static IEnumerable<Hero> CreateBatchThree()
    {
        yield return new("zhangjiao", "张角", "群", 3, "雷击 · 鬼道 · 黄天", "打出闪后可令一人判定，黑桃受2雷伤；黑色手牌/装备改判并取得旧判定牌。主公黄天可收群将每阶段一次赠闪/闪电。", "雷公助我！", 0, "神话·风", "leiji / guidao / huangtian（经典）");
        yield return new("xunyu", "荀彧", "魏", 3, "驱虎 · 节命", "阶段一次，与体力较高者拼点；赢令其对其范围内一人造成1伤，否则其对你造成1伤。每点伤害可令一人补牌至体力上限且至多5张。", "借你之手，与他一搏吧。", 0, "神话·火", "quhu / jieming");
        yield return new("wolong", "卧龙诸葛亮", "蜀", 3, "八阵 · 火计 · 看破", "无防具时可用八卦阵判定；红色手牌可当火攻，黑色手牌可当无懈可击。", "看破，世间万象。", 0, "神话·火", "bazhen / huoji / kanpo（经典）");
        yield return new("yuanshao", "袁绍", "群", 4, "乱击 · 血裔", "任意两张同花色手牌当万箭齐发。主公血裔：每名其他群角色令手牌上限+2。", "弓箭手，准备放箭！", 0, "神话·火", "luanji / xueyi（经典）");
        yield return new("caopi", "曹丕", "魏", 3, "行殇 · 放逐 · 颂威", "可获得阵亡者全部手牌/装备；受伤后可令他人摸已损体力张并翻面。主公颂威：其他魏将黑色判定后可令你摸一。", "翻手为云，覆手为雨。", 0, "神话·林", "xingshang / fangzhu / songwei");
        yield return new("dongzhuo", "董卓", "群", 8, "酒池 · 肉林 · 崩坏 · 暴虐", "黑桃手牌当酒；与女性互相使用杀需双闪。结束时若体力不是最少，失去1体力或上限。主公可因其他群将伤害后的黑桃判定回复。", "醉生梦死，尽享极乐。", 0, "神话·林", "jiuchi / roulin / benghuai / baonue（经典）");
        yield return new("jiaxu", "贾诩", "群", 3, "完杀 · 乱武 · 帷幕", "自己回合仅你与濒死者可用桃；限定乱武令其他人依次向距离最近者出杀，否则失去1体力；不能成为黑色锦囊目标。", "哭喊吧，哀求吧！", 0, "神话·林", "wansha / luanwu / weimu（经典）");
        yield return new("dengai", "邓艾", "魏", 4, "屯田 · 凿险", "回合外失牌可判定，非红桃置为田，距离-田数。准备阶段至少三田觉醒，减上限获急袭：田当顺手牵羊。", "偷渡阴平，直取蜀汉！", 0, "神话·山", "tuntian / zaoxian / jixi");
        yield return new("liushan", "刘禅", "蜀", 3, "享乐 · 放权 · 若愚", "杀的来源须另弃基本牌，否则无效；可跳过出牌，回合结束弃一手牌令他人额外回合。主公准备时体力最少觉醒，加上限回一并获界激将。", "父皇，父皇！", 0, "神话·山", "xiangle / fangquan / ruoyu / rejijiang");
        yield return new("sunce", "孙策", "吴", 4, "激昂 · 魂姿 · 制霸", "使用或成为红杀/决斗目标可摸一。准备时体力不大于1觉醒，减上限获英姿/英魂。其他吴将每阶段可制霸拼点，不胜时你可收两张；觉醒后可拒绝。", "江东子弟，何惧于天下！", 0, "神话·山", "jiang / hunzi / zhiba");
        yield return new("yuejin", "乐进", "魏", 4, "骁果", "其他人结束阶段可弃基本手牌，令其弃一装备牌，否则受到1点伤害。", "三军听我号令！", 0, "标准", "std_yuejin · stdxiaoguo");
        yield return new("lidian", "李典", "魏", 3, "恂恂 · 忘隙", "摸牌可改为看四张取二，其余排序置底；每点造成或受到的伤害后，可令你与另一方各摸一。", "大丈夫，何拘小节。", 0, "标准", "old_re_lidian · xunxun / wangxi");
        yield return new("ganfuren", "甘夫人", "蜀", 3, "淑慎 · 神智", "每回复1体力可令他人摸一（其空手则摸二）；准备时可弃全部手牌，若数量至少等于体力则回复1。", "子龙将军，一切都托付给你。", 0, "标准", "stdshushen / shenzhi");
        yield return new("panfeng", "潘凤", "群", 4, "狂斧", "出牌阶段首次用杀造成伤害后，若受伤者体力小于你则摸二，否则你失去1体力。", "吾乃上将潘凤！", 0, "标准", "std_panfeng · stdkuangfu");
        yield return new("gongsunzan", "公孙瓒", "群", 4, "义从", "计算与他人的距离始终-1；体力不大于2时，他人与你距离+1。", "白马义从，所向披靡！", 0, "标准", "reyicong（不附加趫猛）");
        yield return new("yiji", "伊籍", "蜀", 3, "机捷 · 急援", "阶段一次看牌堆底牌，令一人获得；角色濒死或其他角色获得你给出的牌后，可令其摸一。", "逢凶化吉，遇难成祥。", 0, "标准", "xf_yiji · xinfu_jijie / xinfu_jiyuan");
        yield return new("yuanshu", "袁术", "群", 4, "妄尊 · 同疾", "他人准备时体力高于你，你摸一；其为主公则再摸一且其本回合上限-1。杀的其他目标可弃一牌，将杀转移给其攻击范围内的你。", "玉玺在手，天下我有！", 0, "标准", "re_yuanshu · rewangzun / retongji");
        foreach (var data in new[] {
            ("huatuo", "华佗", "青囊 · 急救", "可弃手牌治疗本回合未治疗过的角色；弃黑牌后本回合不能再青囊。回合外红色手牌/装备当桃。", "new_reqingnang / jijiu"),
            ("daqiao", "大乔", "国色 · 流离", "阶段一次，以方块手牌/装备使用乐不思蜀，或弃方块并弃场上一张乐不思蜀，然后摸一；流离转移杀。", "reguose / liuli"),
            ("zhangliao", "张辽", "突袭", "摸牌阶段可少摸至多两张，获得同数量其他角色的各一张手牌，剩余正常摸。", "new_retuxi"),
            ("guojia", "郭嘉", "天妒 · 遗计", "天妒可收最终判定牌；每点伤害可摸二，并将至多两张任意手牌交给其他角色。", "tiandu / new_reyiji"),
            ("zhugeliang", "诸葛亮", "观星 · 空城", "观星看五张，存活人数小于四则三张；若全置底，结束阶段可再观星。空手不能成为杀/决斗目标。", "reguanxing / kongcheng"),
            ("huangyueying", "黄月英", "集智 · 奇才", "使用锦囊可摸一，若为基本牌可弃之令本回合上限+1；锦囊无距离，装备防具不能被他人弃置。", "rejizhi / reqicai"),
            ("liubei", "刘备", "仁德 · 激将", "每阶段各目标限一次，赠任意手牌；累计二张时可视为使用一张基本牌。界激将及其他蜀将回合外出杀可令主公摸一。", "rerende / rejijiang"),
            ("sunquan", "孙权", "制衡 · 救援", "弃任意手牌/装备换牌，若含全部手牌额外摸一；其他吴将在其回合回复且体力不低于你时，可改为你回复、其摸一。", "rezhiheng / rejiuyuan"),
            ("zhaoyun", "赵云", "龙胆 · 涯角", "杀闪互换、桃酒互换；回合外使用/打出手牌可展示顶牌，同类别可赠人，不同可弃攻击范围包含你的角色一牌，余牌置顶。", "ollongdan / olyajiao"),
            ("zhangfei", "张飞", "咆哮 · 替身", "杀无次数；杀被闪后，下次杀伤害增加等同被闪次数。准备阶段限定回复至满并摸等同回复量。", "olpaoxiao / oltishen"),
            ("ganning", "甘宁", "奇袭 · 奋威", "黑牌当过河拆桥；限定：锦囊指定至少两人时，可令其对其中任意角色无效。", "qixi / fenwei"),
            ("lvmeng", "吕蒙", "克己 · 勤学 · 博图", "未出杀可跳弃牌；准备或结束时手牌至少比体力多二觉醒减上限、回一或摸二，获攻心。回合结束若本回合弃牌含四花色可额外回合，每轮至多存活人数且最多三次。", "keji / qinxue / rebotu") })
        {
            Hero original = AllBase(data.Item1);
            yield return new("re_" + data.Item1, "界" + data.Item2, original.Kingdom, original.Health, data.Item3, data.Item4, original.Quote, original.PortraitIndex, "界限突破", data.Item5, data.Item1);
        }
    }
    private static Hero AllBase(string id) => id switch
    {
        "huatuo" => new(id,"华佗","群",3,"","","救人一命，胜造七级浮屠。",23),
        "daqiao" => new(id,"大乔","吴",3,"","","国色天香，倾国倾城。",19),
        "zhangliao" => new(id,"张辽","魏",4,"","","没想到吧！",8),
        "guojia" => new(id,"郭嘉","魏",3,"","","就这样罢。",10),
        "zhugeliang" => new(id,"诸葛亮","蜀",3,"","","知天易，逆天难。",12),
        "huangyueying" => new(id,"黄月英","蜀",3,"","","奇思妙想，尽在其中。",14),
        "liubei" => new(id,"刘备","蜀",4,"","","惟贤惟德，能服于人。",0),
        "sunquan" => new(id,"孙权","吴",4,"","","容我三思，再作定夺。",5),
        "zhaoyun" => new(id,"赵云","蜀",4,"","","一身是胆，百战不殆。",3),
        "zhangfei" => new(id,"张飞","蜀",4,"","","燕人张翼德在此！",2),
        "ganning" => new(id,"甘宁","吴",4,"","","接招吧！",15),
        "lvmeng" => new(id,"吕蒙","吴",4,"","","留得青山在，不怕没柴烧。",16),
        _ => throw new ArgumentException("未声明的标准基础武将", nameof(id))
    };
}

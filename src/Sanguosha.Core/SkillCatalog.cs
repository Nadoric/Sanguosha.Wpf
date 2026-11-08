namespace Sanguosha.Core;

public enum SkillCategory { Ordinary, Locked, Lord, Awakening, Limited, LockedLord }
public sealed record SkillDefinition(string Name, SkillCategory Category, bool Active);

/// <summary>Independent ownership: 化身 grants ONE named skill, never another general's complete kit.</summary>
public static class SkillCatalog
{
    private static readonly HashSet<string> Locked = new(StringComparer.Ordinal) { "咆哮", "马术", "奇才", "空城", "无双", "红颜", "祸首", "巨象", "狂骨", "义从", "狂斧", "耀武", "完杀", "帷幕", "享乐", "肉林", "崩坏", "诈降", "断肠", "妄尊", "八阵" };
    private static readonly HashSet<string> Lords = new(StringComparer.Ordinal) { "激将", "护驾", "救援", "黄天", "血裔", "颂威", "暴虐", "制霸" };
    private static readonly HashSet<string> Awakening = new(StringComparer.Ordinal) { "志继", "凿险", "若愚", "魂姿", "勤学" };
    private static readonly HashSet<string> Limited = new(StringComparer.Ordinal) { "涅槃", "乱武", "奋威", "替身" };
    private static readonly HashSet<string> Active = new(StringComparer.Ordinal) { "仁德", "制衡", "武圣", "奇袭", "国色", "断粮", "青囊", "结姻", "离间", "反间", "苦肉", "强袭", "连环", "天义", "挑衅", "缔盟", "直谏", "驱虎", "乱击", "火计", "酒池", "乱武", "急袭", "机捷", "攻心", "义绝", "蛊惑", "激将" };
    public static IReadOnlyList<SkillDefinition> For(Hero hero) => hero.Skill.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(name => Define(hero, name)).ToArray();
    public static SkillDefinition Define(Hero hero, string name) => new(name,
        Lords.Contains(name) ? (name == "血裔" || name == "救援" && hero.Id == "sunquan" ? SkillCategory.LockedLord : SkillCategory.Lord) : Awakening.Contains(name) ? SkillCategory.Awakening : Limited.Contains(name) ? SkillCategory.Limited :
        Locked.Contains(name) || name == "谦逊" && hero.Id == "luxun" || name == "英姿" && hero.Id is "re_zhouyu" or "sunce" ? SkillCategory.Locked : SkillCategory.Ordinary, Active.Contains(name));
    public static IReadOnlyList<SkillDefinition> Borrowable(Hero hero) => hero.Id == "zuoci" ? Array.Empty<SkillDefinition>() : For(hero).Where(skill => skill.Category is SkillCategory.Ordinary or SkillCategory.Locked).ToArray();
}

public sealed partial class Player
{
    public List<string> Incarnations { get; } = new();
    public string? IncarnationHeroId { get; set; }
    public string? IncarnationSkill { get; set; }
    public HashSet<SkillCategory> TemporarySkillBans { get; } = new();
    public bool HandUseBanned { get; set; }
    public string SkillIdentity => !SkillsDisabled && Hero.Id == "zuoci" && IncarnationHeroId != null ? IncarnationHeroId : Hero.Id;
    public string Kingdom => !SkillsDisabled && Hero.Id == "zuoci" && IncarnationHeroId != null ? Heroes.Find(IncarnationHeroId).Kingdom : Hero.Kingdom;
    public bool IsFemale => !SkillsDisabled && Hero.Id == "zuoci" && IncarnationHeroId != null ? Heroes.Find(IncarnationHeroId).IsFemale : Hero.IsFemale;
    public IEnumerable<SkillDefinition> OwnedSkills
    {
        get
        {
            foreach (var skill in SkillCatalog.For(Hero)) yield return skill;
            if (Hero.Id == "zuoci" && IncarnationHeroId != null && IncarnationSkill != null) yield return SkillCatalog.Define(Heroes.Find(IncarnationHeroId), IncarnationSkill);
            if (Awakened)
                foreach (string name in Hero.Id switch { "jiangwei" => new[] { "观星" }, "dengai" => new[] { "急袭" }, "liushan" => new[] { "激将" }, "sunce" => new[] { "英姿", "英魂" }, "re_lvmeng" => new[] { "攻心" }, _ => Array.Empty<string>() }) yield return SkillCatalog.Define(Hero, name);
        }
    }
    public bool HasAbility(string name) => !SkillsDisabled && OwnedSkills.Any(skill => skill.Name == name && !TemporarySkillBans.Contains(skill.Category));
    public bool HasActiveAbility => OwnedSkills.Any(skill => skill.Active && HasAbility(skill.Name));
    public string EffectiveSkillText => SkillsDisabled ? "断肠·技能已失去" : Hero.Skill + (Hero.Id == "zuoci" && IncarnationHeroId != null ? $" · 化身{Heroes.Find(IncarnationHeroId).Name}〔{IncarnationSkill}〕 · {Kingdom}/{(IsFemale ? "女" : "男")}" : "") + (TemporarySkillBans.Count > 0 ? " · 非锁定技暂失效" : "") + (HandUseBanned ? " · 手牌禁用" : "");
}

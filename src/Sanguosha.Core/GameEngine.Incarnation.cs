namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private static string GeneralIdentity(Hero hero) => hero.BaseHeroId ?? hero.Id;
    private void GainIncarnations(Player owner, int count)
    {
        HashSet<string> unavailable = Players.Where(player => player.IsAlive).Select(player => GeneralIdentity(player.Hero)).ToHashSet(StringComparer.Ordinal);
        foreach (Player player in Players.Where(player => player.IsAlive && !player.SkillsDisabled))
            foreach (string id in player.Incarnations) unavailable.Add(GeneralIdentity(Heroes.Find(id)));
        for (int i = 0; i < count; i++)
        {
            List<Hero> candidates = Heroes.All.Where(hero => !unavailable.Contains(GeneralIdentity(hero)) && SkillCatalog.Borrowable(hero).Count > 0).ToList();
            if (candidates.Count == 0) break;
            Hero chosen = candidates[random.Next(candidates.Count)]; owner.Incarnations.Add(chosen.Id); unavailable.Add(GeneralIdentity(chosen));
        }
        AddLog($"{owner.Hero.Name}现拥有{owner.Incarnations.Count}张化身（内容仅本人可见）。");
    }
    private void InitializeIncarnations(int index, Action done)
    {
        if (index >= Players.Count) { done(); return; }
        Player owner = Players[index];
        if (!owner.HasAbility("化身")) { InitializeIncarnations(index + 1, done); return; }
        GainIncarnations(owner, 2); ChooseIncarnation(owner, false, () => InitializeIncarnations(index + 1, done));
    }
    private double IncarnationScore(Player owner, Hero hero, SkillDefinition skill) => skill.Name switch
    {
        "急救" or "倾国" or "空城" or "不屈" or "八阵" => owner.Health <= 2 ? 20 : 7,
        "苦肉" => owner.Health >= 3 && owner.Hand.Count < 5 ? 10 : 1,
        "武圣" or "龙胆" or "咆哮" or "铁骑" or "无双" => owner.Hand.Count(card => card.Kind is CardKind.Slash or CardKind.Dodge || card.IsRed) + 5,
        "奸雄" or "遗计" or "反馈" or "刚烈" or "节命" or "放逐" or "新生" => 8,
        "英姿" or "好施" or "突袭" or "恂恂" or "观星" or "洛神" => owner.InPlayPhase ? 4 : 12,
        "仁德" or "青囊" or "结姻" => owner.Health < owner.MaxHealth ? 8 : 3,
        _ => 5 + (skill.Category == SkillCategory.Locked ? 1 : 0)
    };
    private void ChooseIncarnation(Player owner, bool optional, Action done)
    {
        if (!owner.IsAlive || !owner.HasAbility("化身") || owner.Incarnations.Count == 0) { done(); return; }
        List<Hero> forms = owner.Incarnations.Select(Heroes.Find).ToList();
        var best = forms.SelectMany(hero => SkillCatalog.Borrowable(hero).Select(skill => (Hero: hero, Skill: skill))).MaxBy(pair => IncarnationScore(owner, pair.Hero, pair.Skill));
        List<SkillDecisionOption> options = forms.Select(hero => new SkillDecisionOption(hero.Id, $"{hero.Name} · {hero.Kingdom}/{(hero.IsFemale ? "女" : "男")} · {string.Join("/", SkillCatalog.Borrowable(hero).Select(skill => skill.Name))}")).ToList();
        if (optional) options.Add(new("no", "保持当前化身与技能"));
        Decide(owner, optional ? "化身：回合开始/结束，可换化身及其中一项技能" : "化身：选择初始武将", options,
            optional && owner.IncarnationHeroId == best.Hero.Id && owner.IncarnationSkill == best.Skill.Name ? "no" : best.Hero.Id, key =>
        {
            if (key == "no") { done(); return; }
            Hero form = forms.First(hero => hero.Id == key); IReadOnlyList<SkillDefinition> skills = SkillCatalog.Borrowable(form);
            string ai = skills.MaxBy(skill => IncarnationScore(owner, form, skill))!.Name;
            Decide(owner, $"化身{form.Name}：仅获得以下一项技能（不获得整套武将技能）", skills.Select(skill => new SkillDecisionOption(skill.Name, skill.Name + (skill.Category == SkillCategory.Locked ? "（锁定）" : "") + " · " + form.Description)).ToList(), ai,
                skill => SetIncarnation(owner, form.Id, skill, done));
        });
    }
    private void SetIncarnation(Player owner, string id, string skill, Action done)
    {
        if (!owner.Incarnations.Contains(id) || !SkillCatalog.Borrowable(Heroes.Find(id)).Any(candidate => candidate.Name == skill)) throw new InvalidOperationException("无效的化身或不可借用的技能。");
        string? old = owner.IncarnationSkill;
        owner.IncarnationHeroId = id; owner.IncarnationSkill = skill;
        if (old == "屯田" && skill != old) { DiscardPile.AddRange(owner.Zone("田")); owner.Zone("田").Clear(); }
        if (old == "不屈" && skill != old) { DiscardPile.AddRange(owner.SpecialCards); owner.SpecialCards.Clear(); }
        // A selected form's sex/faction changes immediately; effects already created (e.g. 谦逊 return) remain independently scheduled.
        AddLog($"{owner.Hero.Name}化身为{Heroes.Find(id).Name}，获得〔{skill}〕，势力{owner.Kingdom}，{(owner.IsFemale ? "女性" : "男性")}。"); EmitSkill(owner, "化身");
        if (owner.Health <= 0 && !IsBuquAlive(owner)) StartRescue(null, owner, done); else done();
    }
    private void Xinsheng(Player owner, int amount, Action done)
    {
        void Next(int index)
        {
            if (index >= amount || !owner.IsAlive || !owner.HasAbility("新生")) { done(); return; }
            OptionalSkill(owner, $"新生：第{index + 1}/{amount}点伤害，是否再获得一张随机化身？", true, use =>
            { if (use) { GainIncarnations(owner, 1); EmitSkill(owner, "新生"); } Next(index + 1); });
        }
        Next(0);
    }
}

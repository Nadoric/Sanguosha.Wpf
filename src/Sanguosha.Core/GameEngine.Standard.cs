namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private bool ExecuteStandardSkill(Player actor, Card? card, Player? target, Player? second)
    {
        if (!actor.HasActiveAbility) return false;
        Action done = () => CompleteAction(actor);
        if (((actor.RuleId == "liubei" && actor.HasAbility("仁德"))))
        {
            if (card == null) return UseJijiang(actor, target);
            if (target == null || target == actor) return false;
            actor.Hand.Remove(card); Gain(target, card); OnHandLost(actor);
            int before = actor.RendeGiven++; if (before < 2 && actor.RendeGiven >= 2) Heal(actor);
            EmitSkill(actor, "仁德"); done(); return true;
        }
        if (actor.SkillUsed) return false;
        if (((actor.RuleId == "sunquan" && actor.HasAbility("制衡"))))
        {
            if (!He(actor).Any()) return false;
            Phase = GamePhase.Resolving;
            ChooseOwnCards(actor, "制衡：选择任意数量手牌/装备后点击完成", 1, He(actor).Count(), true, cards =>
            { actor.SkillUsed = true; PayCards(actor, cards); Draw(actor, cards.Count); EmitSkill(actor, "制衡"); done(); }); return true;
        }
        if (((actor.RuleId == "sunshangxiang" && actor.HasAbility("结姻"))))
        {
            if (actor.Hand.Count < 2 || target == null || target == actor || target.IsFemale || target.Health >= target.MaxHealth) return false;
            Phase = GamePhase.Resolving;
            ChooseOwnCards(actor, "结姻：弃两张手牌，与你选择的受伤男性各回复1点", 2, 2, false, cards =>
            { actor.SkillUsed = true; PayCards(actor, cards); EmitSkill(actor, "结姻"); HealWithChoices(actor, () => HealWithChoices(target, done)); }); return true;
        }
        if (((actor.RuleId == "zhouyu" && actor.HasAbility("反间"))))
        {
            if (actor.Hand.Count == 0 || target == null || target == actor) return false;
            actor.SkillUsed = true; Phase = GamePhase.Resolving;
            Decide(target, "反间：声明一种花色，再随机获得周瑜一张手牌；不符则受1点伤害", new[] { "♠", "♥", "♣", "♦" }.Select(suit => new SkillDecisionOption(suit, suit)).ToList(), "♥", suit =>
            {
                Card given = actor.Hand[random.Next(actor.Hand.Count)]; actor.Hand.Remove(given); Gain(target, given); OnHandLost(actor); ShowCard(given); EmitSkill(actor, "反间");
                if (SuitFor(target, given) != suit) Damage(actor, target, null, done); else done();
            }); return true;
        }
        return false;
    }

    private void StandardVictimDamage(Player? source, Player victim, Card? card, int amount, Action done)
    {
        if (!victim.IsAlive || Phase == GamePhase.GameOver) { done(); return; }
        if (source != null && ((victim.RuleId == "simayi" && victim.HasAbility("反馈")) || (victim.RuleId == "re_simayi" && victim.HasAbility("反馈"))))
        {
            int times = ((victim.RuleId == "re_simayi" && victim.HasAbility("反馈"))) ? amount : 1;
            void Next(int index)
            {
                if (index >= times || !source.IsAlive || (source == victim ? source.Equipment.Count : He(source).Count()) == 0) { done(); return; }
                OptionalSkill(victim, "反馈：是否获得伤害来源一张牌？", Hostility(victim, source) > 0, use =>
                {
                    if (!use) { Next(index + 1); return; }
                    EmitSkill(victim, "反馈");
                    if (source == victim)
                    {
                        Decide(victim, "反馈：将自己一张装备收入手牌", source.Equipment.Select(item => new SkillDecisionOption(item.Id.ToString(), CardLabel(item), PreviewCard: item)).ToList(), source.Equipment[0].Id.ToString(), key =>
                        { Card selected = source.Equipment.First(item => item.Id.ToString() == key); source.Equipment.Remove(selected); Gain(source, selected); OnEquipmentLost(source); Next(index + 1); });
                    }
                    else ChooseOtherCard(victim, source, true, () => Next(index + 1));
                });
            }
            Next(0); return;
        }
        if (((victim.RuleId == "caocao" && victim.HasAbility("奸雄"))))
        {
            if (DamageMaterials(card).Count == 0 || !DamageMaterials(card).All(DiscardPile.Contains)) { done(); return; }
            OptionalSkill(victim, "奸雄：是否获得造成伤害的牌？", true, use =>
            { if (use) { ObtainDamageCards(victim, card); EmitSkill(victim, "奸雄"); } done(); }); return;
        }
        if (((victim.RuleId == "guojia" && victim.HasAbility("遗计"))))
        {
            void Next(int index)
            {
                if (index >= amount) { done(); return; }
                OptionalSkill(victim, "遗计：是否摸两张牌，并可将其中的牌分配给其他人？", true, use =>
                {
                    if (!use) { Next(index + 1); return; }
                    HashSet<int> old = victim.Hand.Select(item => item.Id).ToHashSet(); Draw(victim, 2); EmitSkill(victim, "遗计");
                    List<Card> gained = victim.Hand.Where(item => ((victim.SkillIdentity == "re_guojia" && victim.HasAbility("遗计"))) || !old.Contains(item.Id)).ToList();
                    int given = 0;
                    void Give()
                    {
                        if (gained.Count == 0 || given >= 2) { Next(index + 1); return; }
                        List<SkillDecisionOption> options = gained.Select(item => new SkillDecisionOption(item.Id.ToString(), CardLabel(item), PreviewCard: item)).ToList(); options.Add(new("done", "保留剩余牌"));
                        Decide(victim, ((victim.SkillIdentity == "re_guojia" && victim.HasAbility("遗计"))) ? "界遗计：选择任意手牌分配（至多两张），或保留" : "遗计：选择一张新摸的牌分配，或保留", options, "done", key =>
                        {
                            if (key == "done") { Next(index + 1); return; }
                            Card selected = gained.First(item => item.Id.ToString() == key);
                            ChoosePlayer(victim, "遗计：交给一名其他角色", Players.Where(player => player != victim), false, player => -Hostility(victim, player), target =>
                            { victim.Hand.Remove(selected); Gain(target!, selected); gained.Remove(selected); given++; OnHandLost(victim); Give(); });
                        });
                    }
                    Give();
                });
            }
            Next(0); return;
        }
        if (source != null && ((victim.RuleId == "xiahoudun" && victim.HasAbility("刚烈"))))
        {
            OptionalSkill(victim, "刚烈：是否判定？非红桃令来源弃两张手牌或受到1点伤害", Hostility(victim, source) > 0, use =>
            {
                if (!use) { done(); return; }
                EmitSkill(victim, "刚烈");
                Judge(victim, "刚烈", judgment =>
                {
                    if (judgment == null || SuitFor(victim, judgment) == "♥" || !source.IsAlive) { done(); return; }
                    List<SkillDecisionOption> options = new() { new("damage", "受到1点伤害") };
                    if (source.Hand.Count >= 2) options.Add(new("discard", "弃两张手牌"));
                    Decide(source, "刚烈：选择代价", options, source.Hand.Count >= 2 && source.Health <= 2 ? "discard" : "damage", choice =>
                    { if (choice == "discard") DiscardOwnCards(source, "刚烈：弃两张手牌", 2, false, done); else Damage(victim, source, null, done); });
                });
            }); return;
        }
        done();
    }
}

namespace Sanguosha.Core;

public enum DamageNature { Normal, Fire, Thunder }

public sealed partial class GameEngine
{
    private void TurnOver(Player player)
    {
        player.TurnedOver = !player.TurnedOver;
        AddLog($"{player.Hero.Name}翻面，现为{(player.TurnedOver ? "背面" : "正面")}朝上。");
    }

    private void Preparation(Player actor, Action done)
    {
        void Stars()
        {
            if (((actor.RuleId == "zhugeliang" && actor.HasAbility("观星"))) || ((actor.RuleId == "jiangwei" && actor.HasAbility("观星"))) && actor.Awakened) Guanxing(actor, done);
            else if (((actor.RuleId == "zhenji" && actor.HasAbility("洛神")) || (actor.RuleId == "re_zhenji" && actor.HasAbility("洛神")))) Luoshen(actor, PhysicalCardCount, done);
            else done();
        }
        if (((actor.RuleId == "jiangwei" && actor.HasAbility("志继"))) && !actor.Awakened && actor.Hand.Count == 0)
        {
            actor.Awakened = true; actor.MaxHealth--; actor.Health = Math.Min(actor.Health, actor.MaxHealth); EmitSkill(actor, "志继");
            Decide(actor, "志继觉醒：体力上限-1，获得观星；选择回复1点或摸2张", new[] { new SkillDecisionOption("heal", "回复1点体力"), new SkillDecisionOption("draw", "摸两张牌") },
                actor.Health < actor.MaxHealth ? "heal" : "draw", key => { if (key == "heal") Heal(actor); else Draw(actor, 2); Stars(); });
        }
        else if (((actor.RuleId == "sunjian" && actor.HasAbility("英魂"))) && actor.Health < actor.MaxHealth)
        {
            int lost = actor.MaxHealth - actor.Health;
            ChoosePlayer(actor, "英魂：选择一名其他角色，或不发动", Players.Where(player => player != actor), true,
                player => Allied(actor, player) ? lost : lost - 1, target =>
                {
                    if (target == null) { Stars(); return; }
                    Decide(actor, $"英魂 · {target.Hero.Name}：选择效果（X={lost}）", new[] { new SkillDecisionOption("draw", $"摸{lost}弃1"), new SkillDecisionOption("discard", $"摸1弃{lost}") },
                        Allied(actor, target) ? "draw" : "discard", key =>
                        { EmitSkill(actor, "英魂"); Draw(target, key == "draw" ? lost : 1); DiscardOwnCards(target, "英魂：选择弃牌", key == "draw" ? 1 : lost, true, Stars); });
                });
        }
        else Stars();
    }

    private bool BatchDraw(Player actor, Action done)
    {
        if (((actor.RuleId == "zhouyu" && actor.HasAbility("英姿")) || (actor.RuleId == "re_zhouyu" && actor.HasAbility("英姿"))))
        {
            OptionalSkill(actor, "英姿：摸牌阶段是否额外摸一张？", true, use => { Draw(actor, use ? 3 : 2); if (use) EmitSkill(actor, "英姿"); done(); }); return true;
        }
        if (((actor.RuleId == "xuchu" && actor.HasAbility("裸衣")) || (actor.RuleId == "re_xuchu" && actor.HasAbility("裸衣"))))
        {
            OptionalSkill(actor, "裸衣：是否替代正常摸牌？", actor.Hand.Any(card => card.Kind is CardKind.Slash or CardKind.Duel), use =>
            {
                if (!use) Draw(actor, 2);
                else
                {
                    actor.LuoyiActive = true; EmitSkill(actor, "裸衣");
                    if (((actor.RuleId == "xuchu" && actor.HasAbility("裸衣")))) Draw(actor, 1);
                    else
                    {
                        List<Card> cards = new();
                        for (int index = 0; index < 3; index++) { Card? card = TakeTop(); if (card != null) cards.Add(card); }
                        foreach (Card card in cards) { ShowCard(card); if (card.IsBasic || card.Kind is CardKind.Duel or CardKind.Blade or CardKind.Crossbow) Gain(actor, card); else DiscardPile.Add(card); }
                    }
                }
                done();
            });
            return true;
        }
        if (((actor.RuleId == "lusu" && actor.HasAbility("好施"))))
        {
            int minimumHand = Players.Where(player => player.IsAlive && player != actor).Min(player => player.Hand.Count);
            bool goodRecipient = Players.Any(player => player.IsAlive && player != actor && player.Hand.Count == minimumHand && Allied(actor, player));
            OptionalSkill(actor, "好施：是否多摸两张？若之后手牌超过五须交一半。", actor.Hand.Count <= 1 || goodRecipient, use =>
            {
                Draw(actor, use ? 4 : 2);
                if (!use || actor.Hand.Count <= 5) { done(); return; }
                int minimum = Players.Where(player => player.IsAlive && player != actor).Min(player => player.Hand.Count);
                ChoosePlayer(actor, "好施：选择其他角色中手牌最少的一人", Players.Where(player => player != actor && player.Hand.Count == minimum), false,
                    player => -Hostility(actor, player), target =>
                    {
                        int count = actor.Hand.Count / 2;
                        ChooseOwnCards(actor, "好施：选择交出的手牌", count, count, false, cards =>
                        { GiveCards(actor, target!, cards); EmitSkill(actor, "好施"); done(); });
                    });
            });
            return true;
        }
        if (((actor.RuleId == "zhangliao" && actor.HasAbility("突袭"))))
        {
            List<Player> chosen = new();
            void Pick()
            {
                if (chosen.Count == 2) { Finish(); return; }
                ChoosePlayer(actor, $"突袭：选择第{chosen.Count + 1}名角色（至多两名）；不选择则正常摸牌", Players.Where(player => player != actor && player.Hand.Count > 0 && !chosen.Contains(player)), true,
                    player => Hostility(actor, player), target => { if (target == null) Finish(); else { chosen.Add(target); Pick(); } });
            }
            void Finish()
            {
                if (chosen.Count == 0) { Draw(actor, 2); done(); return; }
                EmitSkill(actor, "突袭");
                void Take(int index)
                {
                    if (index >= chosen.Count || !actor.IsAlive || Phase == GamePhase.GameOver)
                    {
                        if (actor.IsAlive && Phase != GamePhase.GameOver && actor.SkillIdentity == "re_zhangliao" && actor.HasAbility("突袭"))
                            Draw(actor, 2 - chosen.Count);
                        done();
                        return;
                    }
                    ChooseOtherHandCard(actor, chosen[index], () => Take(index + 1));
                }
                Take(0);
            }
            Pick(); return true;
        }
        return false;
    }

    private void TryShensu(Player actor, bool first, Action notUsed)
    {
        List<Player> targets = Players.Where(player => player.IsAlive && player != actor && !(((player.RuleId == "zhugeliang" && player.HasAbility("空城"))) && player.Hand.Count == 0)).ToList();
        if (targets.Count == 0) { notUsed(); return; }
        List<Card> costs = He(actor).Where(card => card.IsEquipment).ToList();
        if (!first && costs.Count == 0) { notUsed(); return; }
        OptionalSkill(actor, first ? "神速一：跳过判定和摸牌，视为使用无距离杀？" : "神速二：弃一装备牌并跳过出牌，视为使用无距离杀？",
            first ? actor.Judgments.Any(card => !actor.SupplyShortages.Contains(card.Id)) : costs.Any(card => KeepValue(card) < 6) && actor.Hand.All(card => card.Kind != CardKind.Slash), use =>
            {
                if (!use) { notUsed(); return; }
                ChoosePlayer(actor, "神速：选择杀的目标", targets, false,
                    player => Hostility(actor, player), target =>
                    {
                        void Strike()
                        {
                            EmitSkill(actor, "神速");
                            Card virtualCard = new(0, CardKind.Slash, "", 0);
                            EmitCue(GameCueKind.CardPlayed, actor, CardKind.Slash, target: target, physicalCard: virtualCard);
                            actor.SetMark("wineSlash", actor.Mark("wine")); actor.SetMark("wine", 0);
                            BeginCardUse(actor, CardKind.Slash, Array.Empty<Card>(), new[] { target! }, finish => DeclaredSlash(actor, target!, virtualCard, finish), () => { actor.SetMark("wineSlash", 0); if (first) FinishDrawPhase(actor, false); else BeginDiscard(actor); });
                        }
                        if (first) Strike();
                        else ChooseOwnCards(actor, "神速二：弃一装备牌", 1, 1, true, cards => { PayCard(actor, cards[0]); Strike(); }, card => card.IsEquipment);
                    });
            });
    }

    private void BatchEnd(Player actor, Action done)
    {
        actor.LuoshenCards.Clear(); actor.TianyiState = 0;
        if (((actor.RuleId == "xuchu" && actor.HasAbility("裸衣")))) actor.LuoyiActive = false;
        if (actor.IsAlive && ((actor.RuleId == "caoren" && actor.HasAbility("据守"))))
            OptionalSkill(actor, "据守：结束阶段摸三张，然后翻面？", actor.Health <= 2 || actor.Hand.Count <= 3, use =>
            { if (use) { Draw(actor, 3); TurnOver(actor); EmitSkill(actor, "据守"); } done(); });
        else done();
    }

    private bool TrySurvival(Player? source, Player victim, Action after)
    {
        if (((victim.RuleId == "pangtong" && victim.HasAbility("涅槃"))) && !victim.LimitedUsed)
        {
            OptionalSkill(victim, "涅槃（限定）：是否弃置所有区域牌，重置、恢复至最多3体力并摸3？", true, use =>
            {
                if (!use) { StartRescue(source, victim, after); return; }
                victim.LimitedUsed = true;
                DiscardPile.AddRange(victim.Hand); DiscardPile.AddRange(victim.Equipment); DiscardPile.AddRange(victim.Judgments); DiscardPile.AddRange(victim.SpecialCards);
                victim.Hand.Clear(); victim.Equipment.Clear(); victim.Judgments.Clear(); victim.SupplyShortages.Clear(); victim.SpecialCards.Clear();
                victim.TurnedOver = false; victim.Chained = false; victim.Health = Math.Min(3, victim.MaxHealth); Draw(victim, 3); EmitSkill(victim, "涅槃"); after();
            }); return true;
        }
        if (((victim.RuleId == "zhoutai" && victim.HasAbility("不屈"))))
        {
            OptionalSkill(victim, "不屈：是否将牌置于武将牌？点数互异则不进入濒死", true, use =>
            {
                if (!use) { StartRescue(source, victim, after); return; }
                while (victim.SpecialCards.Count < 1 - victim.Health) { Card? card = TakeTop(); if (card == null) break; victim.SpecialCards.Add(card); ShowCard(card); }
                EmitSkill(victim, "不屈");
                if (victim.SpecialCards.Count == 1 - victim.Health && victim.SpecialCards.Select(card => card.Rank).Distinct().Count() == victim.SpecialCards.Count) after();
                else StartRescue(source, victim, after);
            }); return true;
        }
        return false;
    }

    private void StartRescue(Player? source, Player victim, Action after)
    {
        List<Player> rescuers = Enumerable.Range(0, Players.Count).Select(offset => Players[(victim.Seat + offset) % Players.Count]).Where(player => player.IsAlive).ToList();
        dyingPlayers.Add(victim);
        if (((Current.RuleId == "jiaxu" && Current.HasAbility("完杀")))) { rescuers = rescuers.Where(player => player == Current || player == victim).ToList(); if (victim != Current) EmitSkill(Current, "完杀"); }
        List<Player> helpers = Players.Where(player => player.IsAlive && ((player.RuleId == "yiji" && player.HasAbility("急援")))).ToList();
        void Help(int index, Action next) { if (index >= helpers.Count) next(); else Jiyuan(helpers[index], victim, () => Help(index + 1, next)); }
        EmitCue(GameCueKind.NearDeath, victim); Help(0, () => Rescue(source, victim, rescuers, 0, () => { dyingPlayers.RemoveAt(dyingPlayers.LastIndexOf(victim)); after(); }));
    }

    private void TrimBuqu(Player player)
    {
        int keep = Math.Max(0, 1 - player.Health);
        while (player.SpecialCards.Count > keep)
        {
            // Prefer removing a duplicate to restore a valid 不屈 set after rescue.
            Card card = player.SpecialCards.GroupBy(card => card.Rank).OrderByDescending(group => group.Count()).First().First();
            player.SpecialCards.Remove(card); DiscardPile.Add(card);
        }
    }
}

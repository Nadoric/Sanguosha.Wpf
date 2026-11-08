namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        private void CompletePhysicalResponse(Player responder, ResponseKind kind, Card? material, Action<bool> done)
        {
            if (material != null) reservedUseMaterials.Add(material.Id);
            CompleteResponse(responder, kind, material != null, success =>
            { if (material != null) reservedUseMaterials.Remove(material.Id); lastResponseCard = material; done(success); });
        }
        private void CompleteResponse(Player responder, ResponseKind kind, bool success, Action<bool> done)
        {
            if (DrainReactions(() => CompleteResponse(responder, kind, success, done))) return;
            if (success && kind == ResponseKind.Dodge && ((responder.RuleId == "zhangjiao" && responder.HasAbility("雷击")))) { Leiji(responder, () => CompleteResponseAfterLightning(responder, kind, success, done)); return; }
            CompleteResponseAfterLightning(responder, kind, success, done);
        }

        private void CompleteResponseAfterLightning(Player responder, ResponseKind kind, bool success, Action<bool> done)
        {
            if (success && kind == ResponseKind.Slash) JijiangDraw(responder, () => CompleteResponseAfterLords(responder, kind, success, done));
            else CompleteResponseAfterLords(responder, kind, success, done);
        }
        private void CompleteResponseAfterLords(Player responder, ResponseKind kind, bool success, Action<bool> done)
        {
            Player? lord = Players.FirstOrDefault(player => player.IsAlive && ((player.RuleId == "re_caocao" && player.HasAbility("护驾"))) &&
                player.Role == Role.Lord && Mode != GameMode.Team2V2 && !player.HujiaDrawUsed && player != responder);
            if (success && kind == ResponseKind.Dodge && responder.Kingdom == "魏" && responder != Current && lord != null)
            {
                OptionalSkill(responder, $"护驾：是否令主公{lord.Hero.Name}摸一张牌？（每回合限一次）",
                    Allied(responder, lord), use =>
                    {
                        if (use)
                        {
                            lord.HujiaDrawUsed = true;
                            Draw(lord, 1);
                            EmitSkill(lord, "护驾");
                        }
                        done(true);
                    });
            }
            else done(success);
        }

        private void RequestHujia(Player lord, string prompt, Action<bool> done, bool allowArmor = true)
        {
            List<Player> helpers = Enumerable.Range(1, Players.Count - 1)
                .Select(offset => Players[(lord.Seat + offset) % Players.Count])
                .Where(player => player.IsAlive && player.Kingdom == "魏").ToList();
            void Ask(int index)
            {
                if (index >= helpers.Count)
                {
                    if (allowArmor) Request(lord, ResponseKind.Dodge, prompt, done, allowHujia: false);
                    else RequestPlain(lord, ResponseKind.Dodge, prompt, done);
                    return;
                }
                Player helper = helpers[index];
                OptionalSkill(helper, $"护驾：是否替主公{lord.Hero.Name}打出闪？", Allied(helper, lord), offer =>
                {
                    if (!offer) { Ask(index + 1); return; }
                    Request(helper, ResponseKind.Dodge, $"护驾：请为{lord.Hero.Name}打出闪，或放弃。",
                        success => { if (success) done(true); else Ask(index + 1); }, allowHujia: false);
                });
            }
            List<SkillDecisionOption> options = new() { new("yes", "发动护驾，请求魏势力打闪"), new("no", "自行响应") };
            if (allowArmor && lord.Equipment.Any(card => card.Kind == CardKind.EightTrigrams)) options.Add(new("armor", "先发动八卦阵，失败后仍可护驾"));
            Decide(lord, "护驾：选择请求其他魏角色、自行响应或先判八卦阵", options, "yes", key =>
            {
                if (key == "yes") { EmitSkill(lord, "护驾"); Ask(0); }
                else if (key == "armor") Judge(lord, "八卦阵", card =>
                { if (card != null && RedFor(lord, card)) CompleteResponse(lord, ResponseKind.Dodge, true, done); else RequestHujia(lord, prompt, done, false); });
                else if (allowArmor) Request(lord, ResponseKind.Dodge, prompt, done, allowHujia: false);
                else RequestPlain(lord, ResponseKind.Dodge, prompt, done);
            });
        }

        private void DiscardOwned(Player owner, Card card)
        {
            if (owner.Equipment.Remove(card))
            {
                DiscardPile.Add(card);
                OnEquipmentLost(owner);
            }
            else DiscardFromHand(owner, card);
        }

        private void ChooseOwnedCost(Player actor, string prompt, Action<Card?> done)
        {
            List<Card> cards = actor.Hand.Concat(actor.Equipment).ToList();
            if (cards.Count == 0) { done(null); return; }
            List<SkillDecisionOption> options = cards.Select(card => new SkillDecisionOption(card.Id.ToString(),
                $"弃置{(actor.Equipment.Contains(card) ? "装备" : "手牌")} · {card.Suit}{card.RankText} {card.Name}", PreviewCard: card)).ToList();
            options.Add(new("cancel", "取消"));
            Decide(actor, prompt, options, cards.OrderBy(KeepValue).First().Id.ToString(), key =>
            {
                if (key == "cancel") { done(null); return; }
                Card card = cards.Single(item => item.Id.ToString() == key);
                DiscardOwned(actor, card);
                done(card);
            });
        }

        private bool ExecuteRefreshedSkill(Player actor, Card? card, Player? target, Player? second)
        {
        if (!actor.HasActiveAbility) return false;
            if (actor.SkillUsed) { Status = "本阶段已发动此技能。"; return false; }
            if (((actor.RuleId == "re_huanggai" && actor.HasAbility("苦肉"))))
            {
                if (actor.Hand.Count + actor.Equipment.Count == 0) { Status = "苦肉需要弃置一张牌。"; return false; }
                ChooseOwnedCost(actor, "界苦肉：选择弃置一张牌，再失去1点体力。", paid =>
                {
                    if (paid == null) { CompleteAction(actor); return; }
                    actor.SkillUsed = true;
                    EmitSkill(actor, "苦肉");
                    LoseHealth(actor, 1, () => CompleteAction(actor));
                });
                return true;
            }
            if (((actor.RuleId == "diaochan" && actor.HasAbility("离间")) || (actor.RuleId == "re_diaochan" && actor.HasAbility("离间"))))
            {
                if (target == null || second == null || target == second || target == actor || second == actor ||
                    target.IsFemale || second.IsFemale || !target.IsAlive || !second.IsAlive ||
                    actor.Hand.Count + actor.Equipment.Count == 0)
                { Status = "界离间：请选择两名不同的男性角色，并准备一张手牌或装备作代价。"; return false; }
                ChooseOwnedCost(actor, "界离间：选择弃置代价。", paid =>
                {
                    if (paid == null) { CompleteAction(actor); return; }
                    actor.SkillUsed = true;
                    EmitSkill(actor, "离间");
                    StartVirtualDuel(target, second, () => CompleteAction(actor), !((actor.RuleId == "diaochan" && actor.HasAbility("离间"))));
                });
                return true;
            }
            if (((actor.RuleId == "re_zhouyu" && actor.HasAbility("反间"))) && card != null && target != null && target != actor && target.IsAlive)
            {
                actor.SkillUsed = true;
                actor.Hand.Remove(card);
                Gain(target, card);
                EmitSkill(actor, "反间");
                AddLog($"界周瑜反间交给{target.Hero.Name}一张{card.Suit}牌。");
                int sameSuit = target.Hand.Concat(target.Equipment).Count(item => item.Suit == card.Suit);
                Decide(target, $"反间：选择展示手牌并弃置全部{card.Suit}牌，或失去1点体力。", new[]
                {
                    new SkillDecisionOption("discard", $"展示手牌并弃置全部{card.Suit}牌"),
                    new SkillDecisionOption("hp", "失去1点体力")
                }, sameSuit <= 1 || target.Health <= 1 ? "discard" : "hp", choice =>
                {
                    if (choice == "hp") LoseHealth(target, 1, () => CompleteAction(actor));
                    else
                    {
                        AddLog($"{target.Hero.Name}展示手牌：" + string.Join("、", target.Hand.Select(item => $"{item.Suit}{item.RankText}{item.Name}")));
                        foreach (Card discarded in target.Hand.Concat(target.Equipment).Where(item => item.Suit == card.Suit).ToList())
                            DiscardOwned(target, discarded);
                        CompleteAction(actor);
                    }
                });
                return true;
            }
            Status = "反间：请选择一张手牌和一名其他角色。";
            return false;
        }
    }
}

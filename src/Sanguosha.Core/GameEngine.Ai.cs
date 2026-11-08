namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        private bool Allied(Player first, Player second)
        {
            if (first == second)
            {
                return true;
            }

            if (Mode == GameMode.Team2V2)
            {
                return first.Team == second.Team;
            }

            if (first.Role == Role.Rebel && second.Role == Role.Rebel) return true;
            return first.Role is Role.Lord or Role.Loyalist &&
                second.Role is Role.Lord or Role.Loyalist;
        }

        private double Hostility(Player actor, Player target)
        {
            if (Allied(actor, target))
            {
                return -100;
            }

            if (Mode == GameMode.Team2V2)
            {
                return 100;
            }

            if (actor.Role == Role.Rebel)
            {
                return target.Role == Role.Lord ? 100 : target.Role == Role.Loyalist ? 70 : 25;
            }

            if (actor.Role == Role.Renegade)
            {
                bool othersRemain = Players.Any(player => player.IsAlive &&
                    player.Role is Role.Rebel or Role.Loyalist);
                if (target.Role == Role.Lord && othersRemain)
                {
                    return -20;
                }

                return target.Role == Role.Rebel ? 85 : 65;
            }

            return target.Role == Role.Rebel ? 100 : 60;
        }

        private void AiAction(Player actor)
        {
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            if (!actor.IsAlive)
            {
                NextTurn();
                return;
            }

            if (TryCoreCompletionAi(actor)) return;
            if (TryBatchThreeAi(actor)) return;
            Card? healing = actor.Hand.FirstOrDefault(card => card.Kind == CardKind.Peach || ((actor.SkillIdentity == "re_zhaoyun" && actor.HasAbility("龙胆"))) && !actor.SkillsDisabled && card.Kind == CardKind.Wine);
            if (healing != null && actor.Health < actor.MaxHealth)
            {
                ResolveCard(actor, healing, null, healing.Kind == CardKind.Wine);
                return;
            }

            Card? draw = actor.Hand.FirstOrDefault(card => card.Kind == CardKind.DrawTwo);
            if (draw != null)
            {
                ResolveCard(actor, draw, null, false);
                return;
            }

            Card? equipment = actor.Hand.FirstOrDefault(card => card.IsEquipment &&
                !actor.Equipment.Any(item => EquipmentSlot(item) == EquipmentSlot(card)));
            if (equipment != null)
            {
                ResolveCard(actor, equipment, null, false);
                return;
            }

            if (((actor.RuleId == "huanggai" && actor.HasAbility("苦肉"))) && actor.Health >= 3)
            {
                ExecuteActiveSkill(actor, null, null, null);
                return;
            }

            if ((((actor.RuleId == "liubei" && actor.HasAbility("仁德"))) ? actor.RendeGiven < 2 : !actor.SkillUsed) && ((actor.RuleId == "liubei" && actor.HasAbility("仁德")) || (actor.RuleId == "sunshangxiang" && actor.HasAbility("结姻")) || (actor.RuleId == "huatuo" && actor.HasAbility("青囊"))))
            {
                Player? friend = Players.FirstOrDefault(player => player.IsAlive && Allied(actor, player) &&
                    (((actor.RuleId == "huatuo" && actor.HasAbility("青囊"))) || player != actor) &&
                    (((actor.RuleId == "liubei" && actor.HasAbility("仁德"))) || player.Health < player.MaxHealth) &&
                    (!((actor.RuleId == "sunshangxiang" && actor.HasAbility("结姻"))) || !player.IsFemale && actor.Hand.Count >= 2));
                Card? gift = actor.Hand.OrderBy(KeepValue).FirstOrDefault();
                if (friend != null && gift != null && (actor.Health < actor.MaxHealth || actor.Hand.Count > 3))
                {
                    if (ExecuteActiveSkill(actor, gift, friend, null)) return;
                }
            }

            List<Player> enemies = Players.Where(player => player.IsAlive && player != actor &&
                    Hostility(actor, player) > 0)
                .OrderByDescending(player => Hostility(actor, player) + (5 - player.Health) * 8)
                .ToList();

            if (((actor.RuleId == "dianwei" && actor.HasAbility("强袭"))) && !actor.SkillUsed)
            {
                Player? target = enemies.FirstOrDefault(player => Distance(actor, player) <= actor.AttackRange);
                bool canPay = actor.Health >= 2 || actor.Hand.Any(card => card.Kind is CardKind.Blade or CardKind.Crossbow) ||
                    (target != null && Distance(actor, target) <= 1 && actor.Equipment.Any(card => card.Kind is CardKind.Blade or CardKind.Crossbow));
                if (target != null && canPay)
                {
                    ExecuteActiveSkill(actor, null, target, null);
                    return;
                }
            }

            if (!actor.SkillUsed && ((actor.RuleId == "re_huanggai" && actor.HasAbility("苦肉"))) && actor.Health >= 2 && actor.Hand.Count + actor.Equipment.Count > 0)
            {
                ExecuteRefreshedSkill(actor, null, null, null);
                return;
            }
            if (!actor.SkillUsed && ((actor.RuleId == "re_zhouyu" && actor.HasAbility("反间"))) && enemies.Count > 0 && actor.Hand.Count > 0)
            {
                ExecuteRefreshedSkill(actor, actor.Hand.OrderBy(KeepValue).First(), enemies[0], null);
                return;
            }
            if (!actor.SkillUsed && ((actor.RuleId == "re_diaochan" && actor.HasAbility("离间"))) && actor.Hand.Count + actor.Equipment.Count > 0)
            {
                List<Player> males = enemies.Where(player => !player.IsFemale).Take(2).ToList();
                if (males.Count == 2)
                {
                    ExecuteRefreshedSkill(actor, null, males[0], males[1]);
                    return;
                }
            }

            if (((actor.RuleId == "diaochan" && actor.HasAbility("离间"))) && !actor.SkillUsed && He(actor).Any() && enemies.Count(player => !player.IsFemale) >= 2)
            {
                List<Player> males = enemies.Where(player => !player.IsFemale).Take(2).ToList();
                ExecuteActiveSkill(actor, null, males[0], males[1]);
                return;
            }

            if (TryBatchAiSkill(actor)) return;
            List<(Card Card, bool Transform)> plans = actor.Hand
                .SelectMany(card => CanUseConversion(actor, card)
                    ? new[] { (card, false), (card, true) }
                    : new[] { (card, false) })
                .OrderBy(plan => AttackPriority(EffectiveKind(actor, plan.Item1, plan.Item2)))
                .ToList();
            foreach ((Card card, bool transform) in plans)
            {
                CardKind kind = EffectiveKind(actor, card, transform);
                if (NeedsTarget(kind))
                {
                    Player? target = enemies.Where(player => ValidatePlay(actor, card, player, transform) == null)
                        .Where(player => kind != CardKind.Slash || transform || card.Nature == DamageNature.Normal || !player.Chained ||
                            Players.Where(linked => linked.IsAlive && linked.Chained).Sum(linked => Hostility(actor, linked)) > 0)
                        .FirstOrDefault();
                    if (target != null)
                    {
                        ResolveCard(actor, card, target, transform);
                        return;
                    }
                }
                else if (kind is CardKind.Arrows or CardKind.Barbarians)
                {
                    int enemyCount = enemies.Count;
                    int friendCount = Players.Count(player => player.IsAlive && player != actor && Allied(actor, player));
                    if (enemyCount > friendCount)
                    {
                        ResolveCard(actor, card, null, false);
                        return;
                    }
                }
                else if (kind == CardKind.Feast && actor.Health < actor.MaxHealth)
                {
                    ResolveCard(actor, card, null, false);
                    return;
                }
            }

            if (!actor.SkillsDisabled && !actor.SkillUsed && ((actor.SkillIdentity == "sunquan" && actor.HasAbility("制衡"))) && actor.Hand.Count > 0)
            {
                Card unwanted = actor.Hand.OrderBy(KeepValue).First();
                if (KeepValue(unwanted) < 5)
                {
                    DiscardFromHand(actor, unwanted);
                    Draw(actor, 1);
                    actor.SkillUsed = true;
                    AddLog("孙权发动制衡，更换一张手牌。");
                    EmitSkill(actor, "制衡");
                    CompleteAction(actor);
                    return;
                }
            }

            BeginDiscard(actor);
        }

        private static int KeepValue(Card card)
        {
            return card.Kind switch
            {
                CardKind.Peach => 10,
                CardKind.Nullification => 8,
                CardKind.Wine => 6,
                CardKind.Dodge => 8,
                CardKind.DrawTwo => 9,
                CardKind.Slash => 5,
                CardKind.Duel => 4,
                _ => 2
            };
        }

        private static int AttackPriority(CardKind kind)
        {
            return kind switch
            {
                CardKind.Dismantle => 0,
                CardKind.Steal => 1,
                CardKind.Slash => 2,
                CardKind.Duel => 3,
                _ => 4
            };
        }
    }
}

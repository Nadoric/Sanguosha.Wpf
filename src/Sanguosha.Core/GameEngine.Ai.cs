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

            Card? healing = actor.Hand.FirstOrDefault(card => card.Kind == CardKind.Peach);
            if (healing != null && actor.Health < actor.MaxHealth)
            {
                ResolveCard(actor, healing, null, false);
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

            if (actor.Hero.Id == "huanggai" && actor.Health >= 3)
            {
                ExecuteActiveSkill(actor, null, null, null);
                return;
            }

            if (!actor.SkillUsed && actor.Hero.Id is "liubei" or "sunshangxiang" or "huatuo")
            {
                Player? friend = Players.FirstOrDefault(player => player.IsAlive && Allied(actor, player) &&
                    (actor.Hero.Id == "huatuo" || player != actor) &&
                    (actor.Hero.Id == "liubei" || player.Health < player.MaxHealth));
                Card? gift = actor.Hand.OrderBy(KeepValue).FirstOrDefault();
                if (friend != null && gift != null && (actor.Health < actor.MaxHealth || actor.Hand.Count > 3))
                {
                    ExecuteActiveSkill(actor, gift, friend, null);
                    return;
                }
            }

            List<Player> enemies = Players.Where(player => player.IsAlive && player != actor &&
                    Hostility(actor, player) > 0)
                .OrderByDescending(player => Hostility(actor, player) + (5 - player.Health) * 8)
                .ToList();

            if (actor.Hero.Id == "diaochan" && !actor.SkillUsed && actor.Hand.Count > 0 && enemies.Count >= 2)
            {
                ExecuteActiveSkill(actor, actor.Hand.OrderBy(KeepValue).First(), enemies[0], enemies[1]);
                return;
            }

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
                    Player? target = enemies.FirstOrDefault(player => ValidatePlay(actor, card, player, transform) == null);
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

            if (!actor.SkillUsed && actor.Hero.Id == "sunquan" && actor.Hand.Count > 0)
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

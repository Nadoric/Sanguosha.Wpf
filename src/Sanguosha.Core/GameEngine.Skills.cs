namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        public bool CanUseConversion(Player player, Card card)
        {
            return player.RuleId switch
            {
                "guanyu" => player.HasAbility("武圣") && card.IsRed,
                "zhaoyun" => player.HasAbility("龙胆") && (card.Kind == CardKind.Dodge || player.SkillIdentity == "re_zhaoyun" && card.Kind is CardKind.Peach or CardKind.Wine),
                "ganning" => player.HasAbility("奇袭") && !card.IsRed,
                "daqiao" => player.HasAbility("国色") && card.Suit == "♦",
                "xuhuang" => player.HasAbility("断粮") && !RedFor(player, card) && (card.IsBasic || card.IsEquipment),
                "yanliangwenchou" => player.HasAbility("双雄") && player == Current && player.DuelJudgmentRed.HasValue &&
                    card.IsRed != player.DuelJudgmentRed.Value,
                _ => false
            };
        }

        private void OnHandLost(Player player, int count = 1)
        {
            if (count <= 0) return;
            if (player.IsAlive && player.SkillIdentity == "re_luxun" && player.HasAbility("连营") && player.Hand.Count == 0) RefreshedLianying(player, count);
            QueueTuntian(player);
            player.LuoshenCards.IntersectWith(player.Hand.Select(card => card.Id));
            if (player.IsAlive && ((player.RuleId == "luxun" && player.HasAbility("连营"))) && player.Hand.Count == 0)
            {
                QueueLianying(player);
            }
        }

        private void OnEquipmentLost(Player player)
        {
            QueueTuntian(player);
            if (player.IsAlive && ((player.RuleId == "sunshangxiang" && player.HasAbility("枭姬")) || (player.RuleId == "re_sunshangxiang" && player.HasAbility("枭姬"))))
            {
                QueueXiaoji(player);
            }
        }

        private void ResolveTurnStart(Player actor)
        {
            AddLog($"第{Round}轮 · {actor.Hero.Name}的回合。");
            BatchThreePreparation(actor, () => Preparation(actor, () =>
            {
                if (((actor.RuleId == "zhanghe" && actor.HasAbility("巧变")))) ResolveQiaobianStart(actor);
                else if (((actor.RuleId == "xiahouyuan" && actor.HasAbility("神速")))) TryShensu(actor, true, () => ResolveJudgments(actor));
                else ResolveJudgments(actor);
            }));
        }

        private void ResolveDrawPhase(Player actor, bool skipPlay)
        {
            normalDrawSeat = actor.Seat;
            if (BatchThreeDraw(actor, () => FinishDrawPhase(actor, skipPlay))) return;
            if (BatchDraw(actor, () => FinishDrawPhase(actor, skipPlay))) return;
            if (TryExpansionDraw(actor, () => FinishDrawPhase(actor, skipPlay)))
            {
                return;
            }

            Draw(actor, 2);
            AddLog($"{actor.Hero.Name}摸2张牌。");

            FinishDrawPhase(actor, skipPlay);
        }

        private void FinishDrawPhase(Player actor, bool skipPlay)
        {
            normalDrawSeat = null;
            if (skipPlay) BeginDiscard(actor);
            else if (((actor.RuleId == "xiahouyuan" && actor.HasAbility("神速")))) TryShensu(actor, false, () => CompleteAction(actor));
            else BeforePlayBatchThree(actor, () => CompleteAction(actor));
        }

        private void BeginDiscard(Player actor)
        {
            if (Phase == GamePhase.GameOver) return;
            if (DrainReactions(() => BeginDiscard(actor))) return;
            if (((actor.RuleId == "zhanghe" && actor.HasAbility("巧变"))) && actor.Hand.Count > 0)
            {
                QiaobianCost(actor, "弃牌", actor.Hand.Count > actor.HandLimit + 1, skip =>
                {
                    if (skip) EndActorTurn(actor); else BeginDiscardCore(actor);
                });
                return;
            }
            BeginDiscardCore(actor);
        }

        private void BeginDiscardCore(Player actor)
        {
            actor.SetMark("xueyiBonus", ((actor.RuleId == "yuanshao" && actor.HasAbility("血裔"))) && Lord(actor) ? 2 * Players.Count(player => player.IsAlive && player != actor && player.Kingdom == "群") : 0);
            discardingActor = actor; phaseDiscards.Clear();
            if (((actor.RuleId == "lvmeng" && actor.HasAbility("克己"))) && !actor.PlayedSlash)
            {
                OptionalSkill(actor, "克己：本回合未使用或打出杀，是否跳过弃牌阶段？", true, use =>
                { if (use) { EmitSkill(actor, "克己"); EndActorTurn(actor); } else ResolveNormalDiscard(actor); });
                return;
            }
            ResolveNormalDiscard(actor);
        }

        private void ResolveNormalDiscard(Player actor)
        {
            int excess = actor.Hand.Count - actor.HandLimit;
            if (excess > 0 && actor.IsHuman)
            {
                Phase = GamePhase.Discard;
                Status = $"弃牌阶段 · 请选择并弃置 {excess} 张牌（手牌上限{actor.HandLimit}）。";
                return;
            }

            PayCards(actor, actor.Hand.OrderBy(KeepValue).Take(Math.Max(0, excess)).ToList());

            if (excess > 0)
            {
                AddLog($"{actor.Hero.Name}弃置{excess}张牌。");
            }

            EndActorTurn(actor);
        }

        private void EndActorTurn(Player actor)
        {
            if (Phase == GamePhase.GameOver) return;
            if (DrainReactions(() => EndActorTurn(actor))) return;
            actor.InPlayPhase = false;
            actor.SetMark("xueyiBonus", ((actor.RuleId == "yuanshao" && actor.HasAbility("血裔"))) && actor.Role == Role.Lord && Mode != GameMode.Team2V2 ? 2 * Players.Count(player => player.IsAlive && player != actor && player.Kingdom == "群") : 0);
            FinishGuzheng(actor, () => BatchEnd(actor, () => BatchThreeEnd(actor, () => EndActorTurnCore(actor))));
        }

        private void EndActorTurnCore(Player actor)
        {
            actor.DuelJudgmentRed = null;
            actor.ZhaXiangCount = 0;
            if (actor.IsAlive && ((actor.RuleId == "re_diaochan" && actor.HasAbility("闭月"))))
            {
                OptionalSkill(actor, "界闭月：是否在结束阶段摸牌？（空手摸两张，否则一张）", true, use =>
                {
                    if (use)
                    {
                        EmitSkill(actor, "闭月");
                        Draw(actor, actor.Hand.Count == 0 ? 2 : 1);
                    }
                    NextTurn();
                });
                return;
            }
            if (actor.IsAlive && ((actor.RuleId == "diaochan" && actor.HasAbility("闭月"))))
            {
                OptionalSkill(actor, "闭月：结束阶段是否摸一张牌？", true, use =>
                { if (use) { Draw(actor, 1); EmitSkill(actor, "闭月"); } NextTurn(); });
                return;
            }

            NextTurn();
        }

        private void LoseHealth(Player actor, int amount, Action done)
        {
            bool ownPlay = actor == Current && actor.InPlayPhase && Phase is GamePhase.Play or GamePhase.Resolving;
            Action afterLoss = () =>
            {
                if (actor.IsAlive && ((actor.RuleId == "re_huanggai" && actor.HasAbility("诈降"))))
                {
                    Draw(actor, amount * 3);
                    if (ownPlay) actor.ZhaXiangCount += amount;
                    EmitSkill(actor, "诈降");
                }
                done();
            };
            actor.Health -= amount;
            EmitCue(GameCueKind.Damaged, actor);
            AddLog($"{actor.Hero.Name}失去{amount}点体力。");
            if (actor.Health > 0)
            {
                afterLoss();
                return;
            }

            if (!TrySurvival(null, actor, afterLoss)) StartRescue(null, actor, afterLoss);
        }

        private void AfterVictimDamage(Player? source, Player victim, Card? card, int amount, Action done)
        {
            if (!victim.IsAlive || Phase == GamePhase.GameOver)
            {
                done();
                return;
            }

            if (victim.HasAbility("新生")) Xinsheng(victim, amount, () => AfterVictimDamageSkills(source, victim, card, amount, done));
            else AfterVictimDamageSkills(source, victim, card, amount, done);
        }

        private void AfterVictimDamageSkills(Player? source, Player victim, Card? card, int amount, Action done)
        {
            if (victim.SkillIdentity == "re_xiahoudun" && victim.HasAbility("刚烈")) { RefreshedGanglie(source, victim, amount, done); return; }
            if (((victim.RuleId == "re_caocao" && victim.HasAbility("奸雄"))))
            {
                OptionalSkill(victim, "界奸雄：是否获得伤害牌（若仍可获得）并摸一张牌？", true, use =>
                {
                    if (use)
                    {
                        ObtainDamageCards(victim, card);
                        Draw(victim, 1);
                        EmitSkill(victim, "奸雄");
                    }
                    done();
                });
                return;
            }

            BatchThreeVictimDamage(source, victim, card, amount, () => StandardVictimDamage(source, victim, card, amount, done));
        }

        private void RequestRepeated(Player player, ResponseKind kind, string prompt, int count, Action<bool> done)
        {
            Request(player, kind, count > 1 ? $"{prompt}（无双：还需{count}张）" : prompt, success =>
            {
                if (!success || count <= 1)
                {
                    done(success);
                }
                else
                {
                    work.Enqueue(() => RequestRepeated(player, kind, prompt, count - 1, done));
                }
            });
        }

        private bool ExecuteActiveSkill(Player actor, Card? card, Player? target, Player? secondTarget)
        {
            if (GlobalSkillMenu(actor, card, target, secondTarget)) return true;
            if (!actor.HasActiveAbility) return false;
            if (actor.HasAbility("激将") && !actor.HasAbility("仁德") && card == null) return UseJijiang(actor, target);
            if (actor.HasAbility("义绝") && actor.Mark("yijueUsed") == 0 && actor.Mark("routingWusheng") == 0)
            {
                if (actor.HasAbility("武圣") && actor.Equipment.Any(item => item.IsRed))
                {
                    Phase = GamePhase.Resolving;
                    Decide(actor, "界关羽：选择主动技能", new[] { new SkillDecisionOption("yijue", "义绝：弃牌、令对手亮牌"), new SkillDecisionOption("equip", "武圣：使用已装备的红色牌") }, "yijue", key =>
                    {
                        if (key == "yijue") { if (!ExecuteYijue(actor)) CompleteAction(actor); }
                        else { actor.SetMark("routingWusheng", 1); bool used = ExecuteActiveSkill(actor, card, target, secondTarget); actor.SetMark("routingWusheng", 0); if (!used) CompleteAction(actor); }
                    }); return true;
                }
                if (ExecuteYijue(actor)) return true;
            }
            if (actor.HasAbility("蛊惑") && BeginGuhuoPlay(actor, card, target)) return true;
            if (IsBatchThreeActive(actor)) return ExecuteBatchThreeSkill(actor, card, target, secondTarget);
            if (actor.RuleId is "guanyu" or "ganning" or "daqiao" or "xuhuang")
            {
                List<Card> choices = actor.Equipment.Where(item => CanUseConversion(actor, item)).ToList().Where(item =>
                {
                    actor.Equipment.Remove(item); actor.Hand.Add(item);
                    bool legal = ValidatePlay(actor, item, target, true) == null;
                    actor.Hand.Remove(item); actor.Equipment.Add(item); return legal;
                }).ToList();
                if (choices.Count == 0) return false;
                Phase = GamePhase.Resolving;
                Decide(actor, "装备转化：选择已装备且可合法转化使用的牌", choices.Select(item => new SkillDecisionOption(item.Id.ToString(), CardLabel(item))).ToList(), choices.MinBy(KeepValue)!.Id.ToString(), key =>
                {
                    Card selected = choices.First(item => item.Id.ToString() == key);
                    actor.Equipment.Remove(selected); actor.Hand.Add(selected); OnEquipmentLost(actor);
                    ResolveCard(actor, selected, target, true);
                }); return true;
            }

            if (actor.RuleId is "liubei" or "sunquan" or "sunshangxiang" or "zhouyu")
                return ExecuteStandardSkill(actor, card, target, secondTarget);
            if (actor.RuleId == "diaochan") return ExecuteRefreshedSkill(actor, card, target, secondTarget);
            if (actor.RuleId is "pangtong" or "taishici" or "jiangwei" or "lusu" or "zhangzhaozhanghong" or "re_sunshangxiang")
                return ExecuteBatchSkill(actor, card, target, secondTarget);
            if (actor.RuleId is "re_huanggai" or "re_diaochan" or "re_zhouyu")
            {
                return ExecuteRefreshedSkill(actor, card, target, secondTarget);
            }
            if (actor.RuleId == "dianwei")
            {
                return ExecuteQiangxi(actor, target);
            }

            if (actor.RuleId == "huanggai")
            {
                Phase = GamePhase.Resolving;
                AddLog("黄盖发动苦肉。");
                EmitSkill(actor, "苦肉");
                LoseHealth(actor, 1, () =>
                {
                    if (actor.IsAlive)
                    {
                        Draw(actor, 2);
                    }

                    CompleteAction(actor);
                });
                return true;
            }

            if (actor.RuleId == "huatuo" && !actor.SkillUsed && card != null)
            {
                target ??= actor;
                if (!target.IsAlive || target.Health >= target.MaxHealth) { Status = "青囊：请选择受伤角色。"; return false; }
                actor.SkillUsed = true; Phase = GamePhase.Resolving; DiscardFromHand(actor, card);
                EmitSkill(actor, "青囊"); HealWithChoices(target, () => CompleteAction(actor)); return true;
            }
            Status = actor.SkillsDisabled ? "断肠：武将技能已失去。" : actor.Hero.Description;
            return false;
        }
    }
}

namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        private void Decide(Player chooser, string prompt, IReadOnlyList<SkillDecisionOption> options,
            string aiChoice, Action<string> continuation)
        {
            if (DrainReactions(() => Decide(chooser, prompt, options, aiChoice, continuation))) return;
            if (!chooser.IsHuman)
            {
                continuation(options.Any(option => option.Key == aiChoice) ? aiChoice : options[0].Key);
                return;
            }

            PendingDecision = new SkillDecisionRequest(prompt, options, continuation);
            Phase = GamePhase.SelectCard;
            Status = prompt;
        }

        public bool ChooseSkillDecision(string key)
        {
            SkillDecisionRequest? request = PendingDecision;
            if (Phase != GamePhase.SelectCard || request == null ||
                !request.Options.Any(option => option.Key == key))
            {
                return false;
            }

            PendingDecision = null;
            Phase = GamePhase.Resolving;
            request.Continue(key);
            Notify();
            return true;
        }

        private void OptionalSkill(Player player, string prompt, bool aiUse, Action<bool> done)
        {
            Decide(player, prompt, new[]
            {
                new SkillDecisionOption("yes", "发动技能"),
                new SkillDecisionOption("no", "不发动")
            }, aiUse ? "yes" : "no", choice => done(choice == "yes"));
        }

        private bool TryExpansionDraw(Player actor, Action done)
        {
            if (((actor.RuleId == "yanliangwenchou" && actor.HasAbility("双雄"))))
            {
                OptionalSkill(actor, "双雄：是否放弃正常摸牌，改为判定并获得判定牌？",
                    actor.Hand.Count > 3, use =>
                    {
                        if (!use)
                        {
                            Draw(actor, 2);
                        }
                        else
                        {
                            EmitSkill(actor, "双雄");
                            Judge(actor, "双雄", card =>
                            {
                                if (card != null)
                                {
                                    actor.DuelJudgmentRed = RedFor(actor, card);
                                    if (DiscardPile.Remove(card)) Gain(actor, card);
                                }
                                done();
                            });
                            return;
                        }

                        done();
                    });
                return true;
            }

            if (((actor.RuleId == "menghuo" && actor.HasAbility("再起"))) && actor.Health < actor.MaxHealth)
            {
                int lost = actor.MaxHealth - actor.Health;
                OptionalSkill(actor, $"再起：是否放弃正常摸牌，亮出{lost}张牌（红桃回复，其余获得）？",
                    lost >= 3 || (lost == 2 && actor.Hand.Count >= 2), use =>
                    {
                        if (!use)
                        {
                            Draw(actor, 2);
                        }
                        else
                        {
                            EmitSkill(actor, "再起");
                            // Collect the entire reveal before discarding hearts, so this resolution
                            // cannot reshuffle and reveal its own just-discarded hearts a second time.
                            List<Card> revealed = new();
                            for (int index = 0; index < lost; index++)
                            {
                                Card? card = TakeTop();
                                if (card != null) revealed.Add(card);
                            }

                            foreach (Card card in revealed)
                            {
                                ShowCard(card);
                                if (card.Suit == "♥")
                                {
                                    Heal(actor);
                                    DiscardPile.Add(card);
                                }
                                else
                                {
                                    Gain(actor, card);
                                }
                            }
                            AddLog($"孟获发动再起：亮出{revealed.Count}张，红桃{revealed.Count(card => card.Suit == "♥")}张。");
                        }

                        done();
                    });
                return true;
            }

            return false;
        }

        private void ResolveSlashDefenseCore(Player actor, Player target, Card card, Action done)
        {
            if (((actor.RuleId == "re_huanggai" && actor.HasAbility("诈降"))) && actor.ZhaXiangCount > 0 && card.IsRed)
            {
                EmitSkill(actor, "诈降");
                Damage(actor, target, card, done, CardKind.Slash);
                return;
            }

            void Defend()
            {
                if (((actor.RuleId == "dongzhuo" && actor.HasAbility("肉林"))) && target.IsFemale) EmitSkill(actor, "肉林");
                else if (((target.RuleId == "dongzhuo" && target.HasAbility("肉林"))) && actor.IsFemale) EmitSkill(target, "肉林");
                RequestRepeated(target, ResponseKind.Dodge, $"{actor.Hero.Name}对你使用了杀，请打出闪。",
                    ((actor.RuleId == "lvbu" && actor.HasAbility("无双")) || (actor.RuleId == "re_lvbu" && actor.HasAbility("无双"))) || ((actor.RuleId == "dongzhuo" && actor.HasAbility("肉林"))) && target.IsFemale || ((target.RuleId == "dongzhuo" && target.HasAbility("肉林"))) && actor.IsFemale ? 2 : 1, success =>
                    {
                        if (success && ((actor.SkillIdentity == "re_zhangfei" && actor.HasAbility("咆哮"))) && !actor.SkillsDisabled) actor.SetMark("paoxiaoBoost", actor.Mark("paoxiaoBoost") + 1);
                        if (!success)
                        {
                            Damage(actor, target, card, done, CardKind.Slash);
                        }
                        else if (((actor.RuleId == "pangde" && actor.HasAbility("猛进"))) && target.Hand.Count + target.Equipment.Count > 0)
                        {
                            OptionalSkill(actor, $"猛进：是否弃置{target.Hero.Name}的一张手牌或装备？",
                                Hostility(actor, target) > 0, use =>
                                {
                                    if (use)
                                    {
                                        EmitSkill(actor, "猛进");
                                        ChooseOtherCard(actor, target, false, done);
                                    }
                                    else done();
                                });
                        }
                        else done();
                    });
            }

            if (((actor.RuleId == "huangzhong" && actor.HasAbility("烈弓"))) && actor == Current &&
                (target.Hand.Count >= actor.Health || target.Hand.Count <= actor.AttackRange))
            {
                OptionalSkill(actor, $"烈弓：是否令对{target.Hero.Name}使用的杀不能被响应？", true, use =>
                {
                    if (use)
                    {
                        EmitSkill(actor, "烈弓");
                        AddLog("黄忠发动经典烈弓，此杀不能被响应。");
                        Damage(actor, target, card, done, CardKind.Slash);
                    }
                    else Defend();
                });
            }
            else Defend();
        }

        private void AfterExpansionDamage(Player? source, Player victim, Card? card, CardKind? kind,
            int amount, bool withinOne, Action done)
        {
            void Finish() => AfterBatchThreeDamage(source, victim, card, kind, amount, () => TryLiyu(source, victim, kind, () => Beige(source, victim, kind, done)));
            AfterExpansionDamageCore(source, victim, card, kind, amount, withinOne, () =>
            {
                if (source?.IsAlive == true && source.Health <= 0 && !IsBuquAlive(source) && Phase != GamePhase.GameOver) StartRescue(source, source, Finish);
                else Finish();
            });
        }

        private void AfterExpansionDamageCore(Player? source, Player victim, Card? card, CardKind? kind,
            int amount, bool withinOne, Action done)
        {
            AfterVictimDamage(source, victim, card, amount, () =>
            {
                if (Phase == GamePhase.GameOver || source?.IsAlive != true)
                {
                    done();
                    return;
                }

                if (((source.RuleId == "weiyan" && source.HasAbility("狂骨"))) && withinOne && source.Health < source.MaxHealth)
                {
                    EmitSkill(source, "狂骨");
                    for (int index = 0; index < amount; index++) Heal(source);
                }

                if (((source.RuleId == "zhurong" && source.HasAbility("烈刃"))) && kind == CardKind.Slash && victim.IsAlive &&
                    source.Hand.Count > 0 && victim.Hand.Count > 0)
                {
                    OptionalSkill(source, $"烈刃：是否与{victim.Hero.Name}拼点？（双方各选择一张手牌）",
                        Hostility(source, victim) > 0 && source.Hand.Any(item => item.Rank >= 9), use =>
                        {
                            if (!use) { done(); return; }
                            EmitSkill(source, "烈刃");
                            CompareHands(source, victim, won =>
                            {
                                if (won && victim.Hand.Count + victim.Equipment.Count > 0)
                                    ChooseOtherCard(source, victim, true, done);
                                else done();
                            });
                        });
                }
                else done();
            });
        }

        private void ChooseOtherCard(Player actor, Player target, bool steal, Action done)
        {
            Action original = done; done = () => AfterLosses(original);
            if (actor.IsHuman)
                RequestTargetCard(target, steal, done, includeJudgment: false);
            else
            {
                RemoveTargetCard(actor, target, steal, includeJudgment: false);
                done();
            }
        }

        private void CompareHands(Player first, Player second, Action<bool> done)
        {
            void Pick(Player player, Action<Card> chosen)
            {
                Card best = player.Hand.OrderByDescending(card => card.Rank).First();
                Decide(player, $"拼点：{player.Hero.Name}请选择自己的手牌；双方选择后才公开。",
                    player.Hand.Select(card => new SkillDecisionOption(card.Id.ToString(),
                        $"{card.Suit}{card.RankText} · {card.Name}", PreviewCard: card)).ToArray(), best.Id.ToString(),
                    key => chosen(player.Hand.Single(card => card.Id.ToString() == key)));
            }

            // Both cards stay in their owners' hands (and remain hidden) until both choices exist.
            Pick(first, left => Pick(second, right =>
            {
                first.Hand.Remove(left);
                second.Hand.Remove(right);
                OrderingCards.Add(left); OrderingCards.Add(right);
                ShowCard(left);
                ShowCard(right);
                OnHandLost(first);
                OnHandLost(second);
                AfterLosses(() =>
                {
                OrderingCards.Remove(left); OrderingCards.Remove(right);
                DiscardPile.Add(left); DiscardPile.Add(right);
                AddLog($"拼点：{first.Hero.Name} {left.Suit}{left.RankText} 对 {second.Hero.Name} {right.Suit}{right.RankText}，" +
                    (left.Rank > right.Rank ? first.Hero.Name + "胜。" : first.Hero.Name + "未胜。"));
                comparisonCards.Clear(); comparisonCards.AddRange(new[] { left, right });
                done(left.Rank > right.Rank);
                });
            }));
        }

        private bool ExecuteQiangxi(Player actor, Player? target)
        {
            if (actor.SkillUsed || target == null || target == actor || !target.IsAlive ||
                Distance(actor, target) > actor.AttackRange)
            {
                Status = "强袭：出牌阶段限一次，请选择攻击范围内的一名其他角色。";
                return false;
            }

            List<Card> weapons = actor.Hand.Concat(actor.Equipment)
                .Where(card => card.Kind is CardKind.Blade or CardKind.Crossbow)
                // A sacrificed equipped weapon must not supply the range for its own cost.
                .Where(card => !actor.Equipment.Contains(card) || Distance(actor, target) <= 1).ToList();
            List<SkillDecisionOption> options = weapons.Select(card => new SkillDecisionOption(
                card.Id.ToString(), $"弃置{(actor.Equipment.Contains(card) ? "已装备" : "手牌")}{card.Name}（{card.Suit}{card.RankText}）", PreviewCard: card)).ToList();
            options.Add(new("hp", "失去1点体力"));
            options.Add(new("cancel", "取消强袭"));
            string ai = weapons.FirstOrDefault()?.Id.ToString() ?? (actor.Health > 1 ? "hp" : "cancel");
            Decide(actor, $"强袭：选择代价，对{target.Hero.Name}造成1点伤害。", options, ai, key =>
            {
                if (key == "cancel") { CompleteAction(actor); return; }
                actor.SkillUsed = true;
                EmitSkill(actor, "强袭");
                Action hit = () =>
                {
                    // Noname's cost continuation can still deal damage after a failed self-rescue,
                    // provided that the match has not already ended.
                    if (Phase != GamePhase.GameOver && target.IsAlive)
                        Damage(actor, target, null, () => CompleteAction(actor));
                };
                if (key == "hp")
                {
                    LoseHealth(actor, 1, hit);
                }
                else
                {
                    Card weapon = weapons.Single(card => card.Id.ToString() == key);
                    if (actor.Equipment.Remove(weapon))
                    {
                        DiscardPile.Add(weapon);
                        OnEquipmentLost(actor);
                    }
                    else DiscardFromHand(actor, weapon);
                    hit();
                }
            });
            return true;
        }
    }
}

namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        public bool CanUseConversion(Player player, Card card)
        {
            return player.Hero.Id switch
            {
                "guanyu" => card.IsRed,
                "zhaoyun" => card.Kind == CardKind.Dodge,
                "ganning" => !card.IsRed,
                "daqiao" => card.Suit == "♦",
                _ => false
            };
        }

        private void OnHandLost(Player player)
        {
            if (player.IsAlive && player.Hero.Id == "luxun" && player.Hand.Count == 0)
            {
                Draw(player, 1);
                AddLog("陆逊发动连营，摸一张牌。");
                EmitSkill(player, "连营");
            }
        }

        private void OnEquipmentLost(Player player)
        {
            if (player.IsAlive && player.Hero.Id == "sunshangxiang")
            {
                Draw(player, 2);
                AddLog("孙尚香发动枭姬，摸两张牌。");
                EmitSkill(player, "枭姬");
            }
        }

        private void ResolveTurnStart(Player actor)
        {
            AddLog($"第{Round}轮 · {actor.Hero.Name}的回合。");
            if (actor.Hero.Id == "zhugeliang" && deck.Count > 1)
            {
                int count = Math.Min(5, deck.Count);
                List<Card> stars = deck.TakeLast(count).OrderBy(KeepValue).ToList();
                deck.RemoveRange(deck.Count - count, count);
                deck.AddRange(stars);
                AddLog("诸葛亮发动观星，整理牌堆顶，将优先保留的牌置于顶端。");
                EmitSkill(actor, "观星");
            }

            if (actor.Hero.Id == "zhenji")
            {
                EmitSkill(actor, "洛神");
                // 上限为完整牌堆大小，防止极端存档在全黑牌堆中无限循环。
                for (int index = 0; index < 108; index++)
                {
                    Card? judgment = TakeTop();
                    if (judgment == null)
                    {
                        break;
                    }

                    ShowCard(judgment);
                    if (judgment.IsRed)
                    {
                        DiscardPile.Add(judgment);
                        AddLog($"甄姬洛神判定{judgment.Suit}{judgment.RankText}，停止判定。");
                        break;
                    }

                    actor.Hand.Add(judgment);
                    AddLog($"甄姬洛神获得黑色牌【{judgment.Name}】。");
                }
            }

            bool skipPlay = false;
            foreach (Card delayed in actor.Judgments.ToList())
            {
                actor.Judgments.Remove(delayed);
                DiscardPile.Add(delayed);
                Card? judgment = TakeTop();
                if (judgment != null)
                {
                    DiscardPile.Add(judgment);
                    ShowCard(judgment);
                    bool skipped = judgment.Suit != "♥";
                    skipPlay |= skipped;
                    AddLog($"{actor.Hero.Name}乐不思蜀判定{judgment.Suit}{judgment.RankText}，" +
                        (skipped ? "本回合跳过出牌阶段。" : "判定通过。"));
                }
            }

            if (actor.Hero.Id == "zhangliao")
            {
                EmitSkill(actor, "突袭");
                List<Player> targets = Players.Where(player => player != actor && player.IsAlive &&
                        player.Hand.Count > 0 && (Mode != GameMode.Team2V2 || player.Team != actor.Team))
                    .OrderByDescending(player => player.Hand.Count).Take(2).ToList();
                foreach (Player target in targets)
                {
                    Card card = target.Hand[random.Next(target.Hand.Count)];
                    target.Hand.Remove(card);
                    actor.Hand.Add(card);
                    OnHandLost(target);
                    AddLog($"张辽发动突袭，从{target.Hero.Name}处获得一张手牌。");
                }

                Draw(actor, 2 - targets.Count);
            }
            else
            {
                int count = actor.Hero.Id == "zhouyu" ? 3 : actor.Hero.Id == "xuchu" ? 1 : 2;
                if (actor.Hero.Id is "zhouyu" or "xuchu")
                {
                    EmitSkill(actor, actor.Hero.Id == "zhouyu" ? "英姿" : "裸衣");
                }

                Draw(actor, count);
                AddLog($"{actor.Hero.Name}摸{count}张牌。" +
                    (actor.Hero.Id == "zhouyu" ? "（英姿）" : actor.Hero.Id == "xuchu" ? "（裸衣）" : ""));
            }

            if (skipPlay)
            {
                BeginDiscard(actor);
            }
            else
            {
                CompleteAction(actor);
            }
        }

        private void BeginDiscard(Player actor)
        {
            if (actor.Hero.Id == "lvmeng" && !actor.PlayedSlash)
            {
                AddLog("吕蒙发动克己，跳过弃牌阶段。");
                EmitSkill(actor, "克己");
                EndActorTurn(actor);
                return;
            }

            int excess = actor.Hand.Count - Math.Max(0, actor.Health);
            if (excess > 0 && actor.IsHuman)
            {
                Phase = GamePhase.Discard;
                Status = $"弃牌阶段 · 请选择并弃置 {excess} 张牌（手牌上限等于体力）。";
                return;
            }

            foreach (Card card in actor.Hand.OrderBy(KeepValue).Take(Math.Max(0, excess)).ToList())
            {
                DiscardFromHand(actor, card);
            }

            if (excess > 0)
            {
                AddLog($"{actor.Hero.Name}弃置{excess}张牌。");
            }

            EndActorTurn(actor);
        }

        private void EndActorTurn(Player actor)
        {
            if (actor.IsAlive && actor.Hero.Id == "diaochan")
            {
                Draw(actor, 1);
                AddLog("貂蝉发动闭月，摸一张牌。");
                EmitSkill(actor, "闭月");
            }

            NextTurn();
        }

        private void LoseHealth(Player actor, int amount, Action done)
        {
            actor.Health -= amount;
            EmitCue(GameCueKind.Damaged, actor);
            AddLog($"{actor.Hero.Name}失去{amount}点体力。");
            if (actor.Health > 0)
            {
                done();
                return;
            }

            List<Player> rescuers = Enumerable.Range(0, Players.Count)
                .Select(offset => Players[(actor.Seat + offset) % Players.Count])
                .Where(player => player.IsAlive).ToList();
            EmitCue(GameCueKind.NearDeath, actor);
            Rescue(actor, actor, rescuers, 0, done);
        }

        private void AfterDamage(Player source, Player victim, Card card, int amount, Action done)
        {
            if (!victim.IsAlive || Phase == GamePhase.GameOver)
            {
                done();
                return;
            }

            switch (victim.Hero.Id)
            {
                case "caocao":
                    if (DiscardPile.Remove(card))
                    {
                        victim.Hand.Add(card);
                        AddLog($"曹操发动奸雄，获得【{card.Name}】。");
                        EmitSkill(victim, "奸雄");
                    }

                    break;
                case "guojia":
                    Draw(victim, amount * 2);
                    AddLog($"郭嘉发动遗计，摸{amount * 2}张牌。");
                    EmitSkill(victim, "遗计");
                    break;
                case "simayi":
                    if (source.IsAlive && source != victim && source.Hand.Count + source.Equipment.Count > 0)
                    {
                        RemoveTargetCard(victim, source, true, includeJudgment: false);
                        AddLog("司马懿发动反馈。");
                        EmitSkill(victim, "反馈");
                    }

                    break;
                case "xiahoudun":
                    EmitSkill(victim, "刚烈");
                    Card? judgment = TakeTop();
                    if (judgment != null)
                    {
                        DiscardPile.Add(judgment);
                        ShowCard(judgment);
                        AddLog($"夏侯惇刚烈判定：{judgment.Suit}{judgment.RankText}。");
                        if (judgment.Suit != "♥" && source.IsAlive && source != victim)
                        {
                            if (source.Hand.Count >= 2)
                            {
                                foreach (Card penalty in source.Hand.OrderBy(KeepValue).Take(2).ToList())
                                {
                                    DiscardFromHand(source, penalty);
                                }

                                AddLog($"{source.Hero.Name}因刚烈弃置两张手牌。");
                            }
                            else
                            {
                                LoseHealth(source, 1, done);
                                return;
                            }
                        }
                    }

                    break;
            }

            done();
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
            if (actor.Hero.Id == "huanggai")
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

            if (actor.SkillUsed || card == null)
            {
                Status = actor.SkillUsed ? "本回合已使用此技能。" : "请先选择一张手牌。";
                return false;
            }

            if (actor.Hero.Id is "liubei" or "sunshangxiang")
            {
                if (target == null || target == actor || !target.IsAlive ||
                    (actor.Hero.Id == "sunshangxiang" && target.Health == target.MaxHealth))
                {
                    Status = actor.Hero.Id == "liubei" ? "仁德：请选择一名其他角色。" : "结姻：请选择一名受伤的其他角色。";
                    return false;
                }

                actor.Hand.Remove(card);
                target.Hand.Add(card);
                OnHandLost(actor);
                Heal(actor);
                if (actor.Hero.Id == "sunshangxiang")
                {
                    Heal(target);
                }

                AddLog($"{actor.Hero.Name}发动{actor.Hero.Skill}，交给{target.Hero.Name}一张手牌。");
                EmitSkill(actor, actor.Hero.Id == "liubei" ? "仁德" : "结姻");
            }
            else if (actor.Hero.Id == "sunquan")
            {
                DiscardFromHand(actor, card);
                Draw(actor, 1);
                AddLog("孙权发动制衡，弃一张牌并摸一张牌。");
                EmitSkill(actor, "制衡");
            }
            else if (actor.Hero.Id == "huatuo")
            {
                target ??= actor;
                if (!target.IsAlive || target.Health >= target.MaxHealth)
                {
                    Status = "青囊：请选择一名受伤角色，也可不选目标治疗自己。";
                    return false;
                }

                DiscardFromHand(actor, card);
                Heal(target);
                AddLog($"华佗发动青囊，治疗{target.Hero.Name}。");
                EmitSkill(actor, "青囊");
            }
            else if (actor.Hero.Id == "diaochan")
            {
                if (target == null || secondTarget == null || target == secondTarget ||
                    target == actor || secondTarget == actor || !target.IsAlive || !secondTarget.IsAlive)
                {
                    Status = "离间：依次选择两名不同的其他角色（首版不限制性别）。";
                    return false;
                }

                DiscardFromHand(actor, card);
                actor.SkillUsed = true;
                Phase = GamePhase.Resolving;
                AddLog($"貂蝉发动离间，令{target.Hero.Name}与{secondTarget.Hero.Name}决斗。");
                EmitSkill(actor, "离间");
                Duel(target, secondTarget, card, secondTarget, target, () => CompleteAction(actor));
                return true;
            }
            else
            {
                Status = actor.Hero.Description;
                return false;
            }

            actor.SkillUsed = true;
            CompleteAction(actor);
            return true;
        }
    }
}

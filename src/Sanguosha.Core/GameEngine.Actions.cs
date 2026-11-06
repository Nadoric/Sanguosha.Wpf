namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        public int Distance(Player from, Player to)
        {
            if (from == to)
            {
                return 0;
            }

            List<Player> alive = Players.Where(player => player.IsAlive).ToList();
            int gap = Math.Abs(alive.IndexOf(from) - alive.IndexOf(to));
            int distance = Math.Min(gap, alive.Count - gap);
            if (to.Equipment.Any(card => card.Kind == CardKind.Horse))
            {
                distance++;
            }

            if (from.Hero.Id == "machao")
            {
                distance--;
            }

            return Math.Max(1, distance);
        }

        public bool CanTransform(Player player, Card card, ResponseKind kind)
        {
            return kind switch
            {
                ResponseKind.Peach => card.Kind == CardKind.Peach ||
                    (player.Hero.Id == "huatuo" && player != Current && card.IsRed),
                ResponseKind.Dodge => card.Kind == CardKind.Dodge ||
                    (player.Hero.Id == "zhaoyun" && card.Kind == CardKind.Slash) ||
                    (player.Hero.Id == "zhenji" && !card.IsRed),
                ResponseKind.Slash => card.Kind == CardKind.Slash ||
                    (player.Hero.Id == "guanyu" && card.IsRed) ||
                    (player.Hero.Id == "zhaoyun" && card.Kind == CardKind.Dodge),
                _ => false
            };
        }

        public CardKind EffectiveKind(Player player, Card card, bool transform)
        {
            if (!transform)
            {
                return card.Kind;
            }

            return player.Hero.Id switch
            {
                "ganning" => CardKind.Dismantle,
                "daqiao" => CardKind.Indulgence,
                _ => CardKind.Slash
            };
        }

        public bool NeedsTarget(CardKind kind)
        {
            return kind is CardKind.Slash or CardKind.Duel or CardKind.Dismantle or CardKind.Steal or CardKind.Indulgence;
        }

        public string? ValidatePlay(Player actor, Card card, Player? target, bool transform = false)
        {
            if (!actor.IsAlive || !actor.Hand.Contains(card))
            {
                return "这张牌已不在手中。";
            }

            if (transform && !CanUseConversion(actor, card))
            {
                return "此牌不符合武将技能的转换条件。";
            }

            CardKind kind = EffectiveKind(actor, card, transform);
            if (kind == CardKind.Dodge)
            {
                return "闪只能在响应时打出。赵云可勾选龙胆，将其当杀使用。";
            }

            if (kind == CardKind.Peach && actor.Health >= actor.MaxHealth)
            {
                return "体力已满，无需使用桃。";
            }

            if (NeedsTarget(kind))
            {
                if (target == null || target == actor || !target.IsAlive)
                {
                    return "请选择一名存活的其他角色作为目标。";
                }

                if (target.Hero.Id == "zhugeliang" && target.Hand.Count == 0 &&
                    kind is CardKind.Slash or CardKind.Duel)
                {
                    return "诸葛亮处于空城状态，不能成为杀或决斗的目标。";
                }

                if (target.Hero.Id == "luxun" && kind is CardKind.Steal or CardKind.Indulgence)
                {
                    return "陆逊的谦逊令其不能成为此牌的目标。";
                }

                if (kind == CardKind.Indulgence && target.Judgments.Count > 0)
                {
                    return "目标判定区已有乐不思蜀。";
                }

                if (kind == CardKind.Slash)
                {
                    bool unlimited = actor.Hero.Id == "zhangfei" ||
                        actor.Equipment.Any(item => item.Kind == CardKind.Crossbow);
                    if (actor.SlashCount > 0 && !unlimited)
                    {
                        return "本回合已使用过杀。咆哮或诸葛连弩可以解除次数限制。";
                    }

                    if (Distance(actor, target) > actor.AttackRange)
                    {
                        return $"目标距离{Distance(actor, target)}，超出攻击范围{actor.AttackRange}。";
                    }
                }

                if (kind == CardKind.Steal && actor.Hero.Id != "huangyueying" && Distance(actor, target) > 1)
                {
                    return "顺手牵羊只能选择距离1以内的角色。";
                }

                if (kind is CardKind.Steal or CardKind.Dismantle &&
                    target.Hand.Count + target.Equipment.Count + target.Judgments.Count == 0)
                {
                    return "目标没有可获取或弃置的牌。";
                }
            }

            return null;
        }

        public bool Play(int cardId, int? targetSeat = null, bool transform = false)
        {
            if (Phase != GamePhase.Play || !Current.IsHuman || Pending != null)
            {
                return false;
            }

            Card? card = Human.Hand.FirstOrDefault(item => item.Id == cardId);
            Player? target = Players.FirstOrDefault(player => player.Seat == targetSeat);
            if (card == null)
            {
                return false;
            }

            string? error = ValidatePlay(Human, card, target, transform);
            if (error != null)
            {
                Status = error;
                Notify();
                return false;
            }

            ResolveCard(Human, card, target, transform);
            Notify();
            return true;
        }

        private void ResolveCard(Player actor, Card card, Player? target, bool transform)
        {
            Phase = GamePhase.Resolving;
            CardKind kind = EffectiveKind(actor, card, transform);
            DiscardFromHand(actor, card);
            ShowCard(card);
            if (transform && kind != card.Kind)
            {
                EmitSkill(actor, actor.Hero.Skill);
            }

            if (actor.Hero.Id == "lvbu" && kind is CardKind.Slash or CardKind.Duel)
            {
                EmitSkill(actor, "无双");
            }

            EmitCue(GameCueKind.CardPlayed, actor, kind);
            string convertedName = new Card(0, kind, "♠", 1).Name;
            string conversion = kind != card.Kind ? $"（{actor.Hero.Skill}，视为{convertedName}）" : "";
            string targetText = NeedsTarget(kind) ? $"，目标：{target!.Hero.Name}" : "";
            AddLog($"{actor.Hero.Name}使用【{card.Name}】{conversion}{targetText}。");
            Status = $"{actor.Hero.Name}使用了{card.Name}{conversion}。";
            Action done = () => CompleteAction(actor);
            if (actor.Hero.Id == "huangyueying" && kind > CardKind.Peach && kind < CardKind.Indulgence)
            {
                Draw(actor, 1);
                EmitSkill(actor, "集智");
                AddLog("黄月英发动集智，摸一张牌。");
            }

            switch (kind)
            {
                case CardKind.Slash:
                    if (actor.Hero.Id == "zhangfei" && actor.SlashCount > 0)
                    {
                        EmitSkill(actor, "咆哮");
                    }

                    actor.SlashCount++;
                    actor.PlayedSlash = true;
                    if (actor.Hero.Id == "machao")
                    {
                        EmitSkill(actor, "铁骑");
                        Card? judgment = TakeTop();
                        if (judgment != null)
                        {
                            DiscardPile.Add(judgment);
                            ShowCard(judgment);
                            AddLog($"马超铁骑判定：{judgment.Suit}{judgment.RankText}。");
                            if (judgment.IsRed)
                            {
                                Damage(actor, target!, card, done);
                                break;
                            }
                        }
                    }

                    RequestRepeated(target!, ResponseKind.Dodge, $"{actor.Hero.Name}对你使用了杀，请打出闪。",
                        actor.Hero.Id == "lvbu" ? 2 : 1,
                        response =>
                        {
                            if (response)
                            {
                                done();
                            }
                            else
                            {
                                Damage(actor, target!, card, done);
                            }
                        });
                    break;
                case CardKind.Peach:
                    Heal(actor);
                    done();
                    break;
                case CardKind.DrawTwo:
                    Draw(actor, 2);
                    done();
                    break;
                case CardKind.Duel:
                    Duel(actor, target!, card, target!, actor, done);
                    break;
                case CardKind.Dismantle:
                case CardKind.Steal:
                    RemoveTargetCard(actor, target!, kind == CardKind.Steal);
                    done();
                    break;
                case CardKind.Barbarians:
                case CardKind.Arrows:
                    List<Player> targets = Enumerable.Range(1, Players.Count - 1)
                        .Select(offset => Players[(actor.Seat + offset) % Players.Count])
                        .Where(player => player.IsAlive)
                        .ToList();
                    ResolveArea(actor, card, targets, 0, done);
                    break;
                case CardKind.Feast:
                    foreach (Player player in Players.Where(player => player.IsAlive))
                    {
                        Heal(player);
                    }

                    done();
                    break;
                case CardKind.Indulgence:
                    DiscardPile.Remove(card);
                    target!.Judgments.Add(card);
                    done();
                    break;
                default:
                    Equip(actor, card);
                    done();
                    break;
            }
        }

        private void Equip(Player actor, Card card)
        {
            Card? previous = actor.Equipment.FirstOrDefault(item => EquipmentSlot(item) == EquipmentSlot(card));
            if (previous != null)
            {
                actor.Equipment.Remove(previous);
                DiscardPile.Add(previous);
                OnEquipmentLost(actor);
            }

            DiscardPile.Remove(card);
            actor.Equipment.Add(card);
        }

        private static int EquipmentSlot(Card card)
        {
            return card.Kind switch
            {
                CardKind.Crossbow or CardKind.Blade => 0,
                CardKind.EightTrigrams => 1,
                _ => 2
            };
        }

        private void RemoveTargetCard(Player actor, Player target, bool steal, bool includeJudgment = true)
        {
            Card card;
            bool publicCard = target.Equipment.Count > 0 || (includeJudgment && target.Judgments.Count > 0);
            if (target.Equipment.Count > 0)
            {
                card = target.Equipment[0];
                target.Equipment.Remove(card);
                OnEquipmentLost(target);
            }
            else if (includeJudgment && target.Judgments.Count > 0)
            {
                card = target.Judgments[0];
                target.Judgments.Remove(card);
            }
            else
            {
                card = target.Hand[random.Next(target.Hand.Count)];
                target.Hand.Remove(card);
                OnHandLost(target);
            }

            if (steal)
            {
                actor.Hand.Add(card);
            }
            else
            {
                DiscardPile.Add(card);
            }

            string cardText = !steal || publicCard || actor.IsHuman || target.IsHuman ? $"【{card.Name}】" : "一张手牌";
            AddLog($"{actor.Hero.Name}{(steal ? "获得" : "弃置")}{target.Hero.Name}的{cardText}。");
        }

        private void Duel(Player source, Player target, Card card, Player responder, Player other, Action done)
        {
            if (other.Hero.Id == "lvbu")
            {
                EmitSkill(other, "无双");
            }

            RequestRepeated(responder, ResponseKind.Slash, $"与{other.Hero.Name}决斗，请打出杀。",
                other.Hero.Id == "lvbu" ? 2 : 1, success =>
            {
                if (success)
                {
                    // 入队以避免长决斗链造成同步递归，并给界面留出展示时间。
                    work.Enqueue(() => Duel(source, target, card, other, responder, done));
                }
                else
                {
                    Damage(other, responder, card, done);
                }
            });
        }

        private void ResolveArea(Player actor, Card card, List<Player> targets, int index, Action done)
        {
            if (Phase == GamePhase.GameOver)
            {
                return;
            }

            if (index >= targets.Count)
            {
                done();
                return;
            }

            Player target = targets[index];
            if (!target.IsAlive)
            {
                ResolveArea(actor, card, targets, index + 1, done);
                return;
            }

            ResponseKind response = card.Kind == CardKind.Barbarians ? ResponseKind.Slash : ResponseKind.Dodge;
            Action next = () => work.Enqueue(() => ResolveArea(actor, card, targets, index + 1, done));
            Request(target, response, $"{actor.Hero.Name}使用{card.Name}，请打出{(response == ResponseKind.Slash ? "杀" : "闪")}。",
                success =>
                {
                    if (success)
                    {
                        next();
                    }
                    else
                    {
                        Damage(actor, target, card, next);
                    }
                });
        }

        private void Request(Player player, ResponseKind kind, string prompt, Action<bool> continuation)
        {
            if (kind == ResponseKind.Dodge &&
                player.Equipment.Any(card => card.Kind == CardKind.EightTrigrams))
            {
                EmitCue(GameCueKind.ArmorJudgment, player);
                Card? judgment = TakeTop();
                if (judgment != null)
                {
                    DiscardPile.Add(judgment);
                    ShowCard(judgment);
                    AddLog($"{player.Hero.Name}的八卦阵判定：{judgment.Suit}{judgment.RankText}，" +
                        (judgment.IsRed ? "视为打出闪。" : "判定失败。"));
                    if (judgment.IsRed)
                    {
                        continuation(true);
                        return;
                    }
                }
            }

            if (player.IsHuman)
            {
                Phase = GamePhase.Response;
                Status = prompt;
                Pending = new ResponseRequest(kind, prompt, player, card =>
                {
                    if (card != null)
                    {
                        DiscardFromHand(player, card);
                        player.PlayedSlash |= kind == ResponseKind.Slash;
                        ShowCard(card);
                        EmitResponse(player, card, kind);
                        AddLog($"{player.Hero.Name}打出【{card.Name}】" +
                            (card.Kind.ToString() == kind.ToString() ? "。" : $"（{player.Hero.Skill}转换）。"));
                    }
                    else
                    {
                        AddLog($"{player.Hero.Name}放弃响应。");
                    }

                    continuation(card != null);
                });
                return;
            }

            Card? response = player.Hand.FirstOrDefault(card => CanTransform(player, card, kind));
            if (response != null)
            {
                DiscardFromHand(player, response);
                player.PlayedSlash |= kind == ResponseKind.Slash;
                ShowCard(response);
                EmitResponse(player, response, kind);
                AddLog($"{player.Hero.Name}打出【{response.Name}】响应。");
            }

            continuation(response != null);
        }

        public bool Respond(int? cardId)
        {
            if (Pending == null)
            {
                return false;
            }

            ResponseRequest request = Pending;
            Card? card = cardId.HasValue
                ? request.Responder.Hand.FirstOrDefault(item => item.Id == cardId)
                : null;
            if (cardId.HasValue && (card == null || !CanTransform(request.Responder, card, request.Kind)))
            {
                Status = "请选择可响应的手牌，或点击放弃响应。";
                Notify();
                return false;
            }

            Pending = null;
            Phase = GamePhase.Resolving;
            request.Continue(card);
            Notify();
            return true;
        }

        private void Heal(Player player)
        {
            if (player.Health < player.MaxHealth)
            {
                player.Health++;
                EmitCue(GameCueKind.Healed, player);
                AddLog($"{player.Hero.Name}回复1点体力（{player.Health}/{player.MaxHealth}）。");
            }
        }

        private void Damage(Player source, Player victim, Card card, Action done)
        {
            int amount = source.Hero.Id == "xuchu" && source == Current &&
                card.Kind is CardKind.Slash or CardKind.Duel ? 2 : 1;
            victim.Health -= amount;
            EmitCue(GameCueKind.Damaged, victim);
            AddLog($"{victim.Hero.Name}受到{source.Hero.Name}造成的{amount}点伤害，剩余{victim.Health}点体力。");
            Action after = () => AfterDamage(source, victim, card, amount, done);

            if (victim.Health > 0)
            {
                after();
                return;
            }

            AddLog($"{victim.Hero.Name}濒死，等待桃救援。");
            EmitCue(GameCueKind.NearDeath, victim);
            List<Player> rescuers = Enumerable.Range(0, Players.Count)
                .Select(offset => Players[(victim.Seat + offset) % Players.Count])
                .Where(player => player.IsAlive)
                .ToList();
            Rescue(source, victim, rescuers, 0, after);
        }

        private void Rescue(Player source, Player victim, List<Player> rescuers, int index, Action done)
        {
            if (victim.Health > 0)
            {
                done();
                return;
            }

            if (index >= rescuers.Count)
            {
                Eliminate(source, victim);
                done();
                return;
            }

            Player rescuer = rescuers[index];
            if (!rescuer.IsHuman && rescuer != victim && !Allied(rescuer, victim))
            {
                Rescue(source, victim, rescuers, index + 1, done);
                return;
            }

            // 人类即便没有桃也会获得明确的救援提示，可以手动放弃。
            Request(rescuer, ResponseKind.Peach, $"{victim.Hero.Name}濒死！打出桃救援，或放弃。", success =>
            {
                if (success)
                {
                    Heal(victim);
                    Rescue(source, victim, rescuers, index, done);
                }
                else
                {
                    Rescue(source, victim, rescuers, index + 1, done);
                }
            });
        }

        private void Eliminate(Player source, Player victim)
        {
            victim.IsAlive = false;
            DiscardPile.AddRange(victim.Hand);
            DiscardPile.AddRange(victim.Equipment);
            DiscardPile.AddRange(victim.Judgments);
            victim.Hand.Clear();
            victim.Equipment.Clear();
            victim.Judgments.Clear();
            AddLog($"{victim.Hero.Name}阵亡，身份公开：{victim.RoleName}。");
            EmitCue(GameCueKind.Eliminated, victim);

            if (CheckVictory())
            {
                return;
            }

            if (Mode == GameMode.Team2V2)
            {
                return;
            }

            if (victim.Role == Role.Rebel && source.IsAlive)
            {
                Draw(source, 3);
                AddLog($"{source.Hero.Name}击败反贼，奖励摸三张牌。");
            }
            else if (victim.Role == Role.Loyalist && source.Role == Role.Lord && source.IsAlive)
            {
                DiscardPile.AddRange(source.Hand);
                DiscardPile.AddRange(source.Equipment);
                source.Hand.Clear();
                source.Equipment.Clear();
                AddLog("主公误杀忠臣，弃置所有手牌与装备。");
            }
        }

        private bool CheckVictory()
        {
            List<Player> alive = Players.Where(player => player.IsAlive).ToList();
            if (Mode == GameMode.Team2V2)
            {
                if (alive.Select(player => player.Team).Distinct().Count() > 1)
                {
                    return false;
                }

                Phase = GamePhase.GameOver;
                Pending = null;
                work.Clear();
                HumanWon = alive.Count > 0 && alive[0].Team == Human.Team;
                Result = alive.Count > 0 && alive[0].Team == 0 ? "青龙队获胜" : "朱雀队获胜";
                Status = Result;
                AddLog($"对局结束 · {Result}。");
                EmitCue(GameCueKind.MatchEnded);
                return true;
            }

            Role? winner = null;
            if (alive.All(player => player.Role != Role.Lord))
            {
                winner = alive.Count == 1 && alive[0].Role == Role.Renegade ? Role.Renegade : Role.Rebel;
            }
            else if (alive.All(player => player.Role is Role.Lord or Role.Loyalist))
            {
                winner = Role.Lord;
            }

            if (winner == null)
            {
                return false;
            }

            Phase = GamePhase.GameOver;
            Pending = null;
            work.Clear();
            HumanWon = winner == Human.Role || (winner == Role.Lord && Human.Role == Role.Loyalist);
            Result = winner switch
            {
                Role.Lord => "主公与忠臣获胜",
                Role.Renegade => "内奸独胜",
                _ => "反贼获胜"
            };
            Status = Result;
            AddLog($"对局结束 · {Result}。");
            EmitCue(GameCueKind.MatchEnded);
            return true;
        }

        public bool UseSkill(int? cardId, int? targetSeat, int? secondTargetSeat = null)
        {
            if (Phase != GamePhase.Play || !Current.IsHuman)
            {
                return false;
            }

            Card? card = Human.Hand.FirstOrDefault(item => item.Id == cardId);
            Player? target = Players.FirstOrDefault(player => player.Seat == targetSeat && player.IsAlive);
            Player? second = Players.FirstOrDefault(player => player.Seat == secondTargetSeat && player.IsAlive);
            bool success = ExecuteActiveSkill(Human, card, target, second);
            Notify();
            return success;
        }
    }
}

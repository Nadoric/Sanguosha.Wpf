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
            if (((from.RuleId == "dengai" && from.HasAbility("屯田")))) distance -= from.Zone("田").Count;
            if (((from.RuleId == "gongsunzan" && from.HasAbility("义从")))) distance--;
            if (((to.RuleId == "gongsunzan" && to.HasAbility("义从"))) && to.Health <= 2) distance++;
            if (to.Equipment.Any(card => card.Kind == CardKind.Horse))
            {
                distance++;
            }

            if (((from.RuleId == "machao" && from.HasAbility("马术")) || (from.RuleId == "pangde" && from.HasAbility("马术"))))
            {
                distance--;
            }

            return Math.Max(1, distance);
        }

        public bool CanTransform(Player player, Card card, ResponseKind kind)
        {
            if (player.HandUseBanned && player.Hand.Contains(card)) return false;
            return kind switch
            {
                ResponseKind.Peach => card.Kind == CardKind.Peach ||
                    (((player.RuleId == "huatuo" && player.HasAbility("急救"))) && player != Current && card.IsRed) || (card.Kind == CardKind.Wine && dyingPlayers.LastOrDefault() == player) || (((player.RuleId == "dongzhuo" && player.HasAbility("酒池"))) && dyingPlayers.LastOrDefault() == player && SuitFor(player, card) == "♠") || (((player.SkillIdentity == "re_zhaoyun" && player.HasAbility("龙胆"))) && !player.SkillsDisabled && card.Kind == CardKind.Wine),
                ResponseKind.Dodge => card.Kind == CardKind.Dodge ||
                    (((player.RuleId == "zhaoyun" && player.HasAbility("龙胆"))) && card.Kind == CardKind.Slash) ||
                    (((player.RuleId == "zhenji" && player.HasAbility("倾国")) || (player.RuleId == "re_zhenji" && player.HasAbility("倾国"))) && !RedFor(player, card)),
                ResponseKind.Slash => card.Kind == CardKind.Slash ||
                    (((player.RuleId == "guanyu" && player.HasAbility("武圣"))) && card.IsRed) ||
                    (((player.RuleId == "zhaoyun" && player.HasAbility("龙胆"))) && card.Kind == CardKind.Dodge),
                _ => false
            };
        }

        public CardKind EffectiveKind(Player player, Card card, bool transform)
        {
            if (!transform)
            {
                return card.Kind;
            }

            if (player.SkillIdentity == "re_zhaoyun" && !player.SkillsDisabled && card.Kind is CardKind.Peach or CardKind.Wine) return card.Kind == CardKind.Peach ? CardKind.Wine : CardKind.Peach;
            return player.RuleId switch
            {
                "ganning" => CardKind.Dismantle,
                "daqiao" => CardKind.Indulgence,
                "xuhuang" => CardKind.SupplyShortage,
                "yanliangwenchou" => CardKind.Duel,
                _ => CardKind.Slash
            };
        }

        public bool NeedsTarget(CardKind kind)
        {
            return kind is CardKind.Slash or CardKind.Duel or CardKind.Dismantle or CardKind.Steal or CardKind.Indulgence or CardKind.SupplyShortage or CardKind.FireAttack;
        }

        public string? ValidatePlay(Player actor, Card card, Player? target, bool transform = false)
        {
            if (actor.HandUseBanned) return "义绝：本回合不能使用或打出手牌。";
            if (!actor.IsAlive || !actor.Hand.Contains(card))
            {
                return "这张牌已不在手中。";
            }

            if (transform && !CanUseConversion(actor, card))
            {
                return "此牌不符合武将技能的转换条件。";
            }

            CardKind kind = EffectiveKind(actor, card, transform);
            if (kind is CardKind.Dodge or CardKind.Nullification)
            {
                return "闪只能在响应时打出。赵云可勾选龙胆，将其当杀使用。";
            }

            if (kind is CardKind.Lightning or CardKind.DrawTwo && ((actor.RuleId == "jiaxu" && actor.HasAbility("帷幕"))) && !RedFor(actor, card)) return "帷幕：不能对自己使用此黑色锦囊。";
            if (kind == CardKind.Peach && actor.Health >= actor.MaxHealth)
            {
                return "体力已满，无需使用桃。";
            }

            if (kind == CardKind.Wine && actor.Mark("wineUsed") > 0) return "本回合已使用过酒。";
            if (kind == CardKind.Lightning && (actor.Judgments.Any(item => DelayedKind(actor, item) == kind) || ((actor.RuleId == "jiaxu" && actor.HasAbility("帷幕"))) && !card.IsRed)) return "不能在自己判定区放置此闪电。";
            if (kind == CardKind.FireAttack && target?.Hand.Count == 0) return "火攻目标必须有手牌。";
            if (((target?.RuleId == "jiaxu" && target?.HasAbility("帷幕") == true)) && Trick(kind) && !card.IsRed) return "帷幕：不能成为黑色锦囊目标。";
            if (NeedsTarget(kind))
            {
                if (target == null || target == actor && kind != CardKind.FireAttack || !target.IsAlive)
                {
                    return "请选择一名存活的其他角色作为目标。";
                }

                if (((target.RuleId == "zhugeliang" && target.HasAbility("空城"))) && target.Hand.Count == 0 &&
                    kind is CardKind.Slash or CardKind.Duel)
                {
                    return "诸葛亮处于空城状态，不能成为杀或决斗的目标。";
                }

                if (((target.RuleId == "luxun" && target.HasAbility("谦逊"))) && kind is CardKind.Steal or CardKind.Indulgence)
                {
                    return "陆逊的谦逊令其不能成为此牌的目标。";
                }

                if (kind is CardKind.Indulgence or CardKind.SupplyShortage && target.Judgments.Any(item => DelayedKind(target, item) == kind))
                {
                    return "目标判定区已有乐不思蜀。";
                }

                if (kind == CardKind.Slash)
                {
                    if (actor.TianyiState < 0) return "天义未赢，本回合不能使用杀。";
                    bool unlimited = ((actor.RuleId == "zhangfei" && actor.HasAbility("咆哮"))) ||
                        actor.Equipment.Any(item => item.Kind == CardKind.Crossbow);
                    if (actor.SlashCount >= 1 + actor.ZhaXiangCount + (actor.TianyiState > 0 ? 1 : 0) && !unlimited)
                    {
                        return "本回合已使用过杀。咆哮或诸葛连弩可以解除次数限制。";
                    }

                    if (Distance(actor, target) > actor.AttackRange && actor.TianyiState <= 0 &&
                        !(actor.SkillIdentity == "re_guanyu" && actor.HasAbility("武圣") && SuitFor(actor, card) == "♦") &&
                        !(((actor.RuleId == "re_huanggai" && actor.HasAbility("诈降"))) && actor.ZhaXiangCount > 0 && card.IsRed))
                    {
                        return $"目标距离{Distance(actor, target)}，超出攻击范围{actor.AttackRange}。";
                    }
                }

                if (kind == CardKind.SupplyShortage && !actor.HasAbility("奇才") && Distance(actor, target) > (((actor.RuleId == "xuhuang" && actor.HasAbility("断粮"))) ? 2 : 1)) return "兵粮寸断超出距离。";
                if (kind == CardKind.Steal && !((actor.RuleId == "huangyueying" && actor.HasAbility("奇才"))) && Distance(actor, target) > 1)
                {
                    return "顺手牵羊只能选择距离1以内的角色。";
                }

                if (kind is CardKind.Steal or CardKind.Dismantle &&
                    AvailableTargetCards(actor, target, kind == CardKind.Steal) == 0)
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
            if (kind == CardKind.Slash) usedSlashNatures[card.Id] = transform ? DamageNature.Normal : card.Nature;
            reservedUseMaterials.Add(card.Id);
            DiscardFromHand(actor, card);
            AfterLosses(() => ResolveUsedCard(actor, card, target, transform, kind));
        }

        private void ResolveUsedCard(Player actor, Card card, Player? target, bool transform, CardKind kind)
        {
            if (kind == CardKind.IronChain) { ResolveIronChain(actor, card, () => CompleteAction(actor)); return; }
            ShowCard(card);
            if (transform && (kind != card.Kind || card.Nature != DamageNature.Normal))
            {
                EmitSkill(actor, actor.RuleId switch { "guanyu" => "武圣", "zhaoyun" => "龙胆", "ganning" => "奇袭", "daqiao" => "国色", "xuhuang" => "断粮", "yanliangwenchou" => "双雄", _ => actor.Hero.Skill });
            }

            if (((actor.RuleId == "lvbu" && actor.HasAbility("无双")) || (actor.RuleId == "re_lvbu" && actor.HasAbility("无双"))) && kind is CardKind.Slash or CardKind.Duel)
            {
                EmitSkill(actor, "无双");
            }

            EmitCue(GameCueKind.CardPlayed, actor, kind, target: target, physicalCard: card,
                nature: kind == CardKind.Slash ? usedSlashNatures.GetValueOrDefault(card.Id, DamageNature.Normal) : DamageNature.Normal);
            string convertedName = new Card(0, kind, "♠", 1).Name;
            string conversion = kind != card.Kind || transform && card.Nature != DamageNature.Normal ? $"（{actor.Hero.Skill}，视为{convertedName}）" : "";
            string targetText = NeedsTarget(kind) ? $"，目标：{target!.Hero.Name}" : "";
            AddLog($"{actor.Hero.Name}使用【{card.Name}】{conversion}{targetText}。");
            Status = $"{actor.Hero.Name}使用了{card.Name}{conversion}。";
            void ResolveDeclared()
            {
                IEnumerable<Player> targets = kind is CardKind.Arrows or CardKind.Barbarians ? Players.Where(player => player.IsAlive && player != actor) : kind == CardKind.Feast ? Players.Where(player => player.IsAlive).OrderBy(player => (player.Seat - actor.Seat + Players.Count) % Players.Count) : new[] { target ?? actor };
                BeginCardUse(actor, kind, new[] { card }, targets, finish => ResolveCardEffect(actor, card, target, kind, finish), () => CompleteAction(actor));
            }
            ResolveDeclared();
        }

        private void ResolveCardEffect(Player actor, Card card, Player? target, CardKind kind, Action finished)
        {
            Action done = finished;
            switch (kind)
            {
                case CardKind.Slash:
                    actor.SlashCount++; actor.PlayedSlash = true;
                    if (((actor.RuleId == "zhangfei" && actor.HasAbility("咆哮"))) && actor.SlashCount > 1) EmitSkill(actor, "咆哮");
                    actor.SetMark("wineSlash", actor.Mark("wine")); actor.SetMark("wine", 0);
                    DeclaredSlash(actor, target!, card, () => { actor.SetMark("wineSlash", 0); done(); });
                    break;
                case CardKind.Peach:
                    HealWithChoices(actor, done);
                    break;
                case CardKind.DrawTwo:
                    TrickEffect(actor, kind, actor, () => { Draw(actor, 2); done(); }, done);
                    break;
                case CardKind.Duel:
                    TrickEffect(actor, kind, target!, () => Duel(actor, target!, card, target!, actor, done), done);
                    break;
                case CardKind.Dismantle:
                case CardKind.Steal:
                    TrickEffect(actor, kind, target!, () => ChooseOtherCardIncludingJudgment(actor, target!, kind == CardKind.Steal, done), done);
                    break;
                case CardKind.Barbarians:
                case CardKind.Arrows:
                    List<Player> targets = Enumerable.Range(1, Players.Count - 1)
                        .Select(offset => Players[(actor.Seat + offset) % Players.Count])
                        .Where(player => player.IsAlive)
                        .ToList();
                    Player damageSource = card.Kind == CardKind.Barbarians
                        ? Players.FirstOrDefault(player => player.IsAlive && ((player.RuleId == "menghuo" && player.HasAbility("祸首")))) ?? actor
                        : actor;
                    ResolveArea(actor, card, targets, 0, () =>
                    {
                        Player? zhurong = Players.FirstOrDefault(player => player.IsAlive &&
                            ((player.RuleId == "zhurong" && player.HasAbility("巨象"))) && player != actor);
                        if (card.Kind == CardKind.Barbarians && zhurong != null && DiscardPile.Remove(card))
                        {
                            Gain(zhurong, card);
                            AddLog("祝融发动巨象，获得结算后的南蛮入侵。");
                            EmitSkill(zhurong, "巨象");
                        }

                        done();
                    }, damageSource);
                    break;
                case CardKind.Feast:
                    List<Player> healed = Players.Where(player => player.IsAlive).OrderBy(player => (player.Seat - actor.Seat + Players.Count) % Players.Count).ToList();
                    void HealNext(int index) { if (index >= healed.Count) done(); else TrickEffect(actor, kind, healed[index], () => HealWithChoices(healed[index], () => HealNext(index + 1)), () => HealNext(index + 1)); }
                    HealNext(0);
                    break;
                case CardKind.Wine:
                    actor.SetMark("wineUsed", 1); actor.SetMark("wine", 1); done(); break;
                case CardKind.FireAttack:
                    TrickEffect(actor, kind, target!, () => ResolveFireAttack(actor, target!, card, done), done); break;
                case CardKind.IronChain:
                    ResolveIronChain(actor, card, done); break;
                case CardKind.Lightning:
                    DiscardPile.Remove(card); actor.Judgments.Add(card); done(); break;
                case CardKind.Indulgence:
                case CardKind.SupplyShortage:
                    DiscardPile.Remove(card);
                    if (kind == CardKind.SupplyShortage && card.Kind != CardKind.SupplyShortage) target!.SupplyShortages.Add(card.Id);
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
            if (AvailableTargetCards(actor, target, steal, includeJudgment) == 0) return;
            List<Card> equipment = target.Equipment.Where(card => !ProtectedEquipment(actor, target, card, steal)).ToList();
            Card card;
            bool publicCard = equipment.Count > 0 || (includeJudgment && target.Judgments.Count > 0);
            if (equipment.Count > 0)
            {
                card = equipment[0];
                target.Equipment.Remove(card);
                OnEquipmentLost(target);
            }
            else if (includeJudgment && target.Judgments.Count > 0)
            {
                card = target.Judgments[0];
                target.Judgments.Remove(card); target.SupplyShortages.Remove(card.Id);
            }
            else
            {
                card = target.Hand[random.Next(target.Hand.Count)];
                target.Hand.Remove(card);
                OnHandLost(target);
            }

            if (steal)
            {
                Gain(actor, card);
            }
            else
            {
                DiscardPile.Add(card);
            }

            string cardText = !steal || publicCard || actor.IsHuman || target.IsHuman ? $"【{card.Name}】" : "一张手牌";
            AddLog($"{actor.Hero.Name}{(steal ? "获得" : "弃置")}{target.Hero.Name}的{cardText}。");
        }

        private void Duel(Player source, Player target, Card? card, Player responder, Player other, Action done)
        {
            if (((other.RuleId == "lvbu" && other.HasAbility("无双")) || (other.RuleId == "re_lvbu" && other.HasAbility("无双"))))
            {
                EmitSkill(other, "无双");
            }

            RequestRepeated(responder, ResponseKind.Slash, $"与{other.Hero.Name}决斗，请打出杀。",
                ((other.RuleId == "lvbu" && other.HasAbility("无双")) || (other.RuleId == "re_lvbu" && other.HasAbility("无双"))) ? 2 : 1, success =>
            {
                if (success)
                {
                    // 入队以避免长决斗链造成同步递归，并给界面留出展示时间。
                    work.Enqueue(() => Duel(source, target, card, other, responder, done));
                }
                else
                {
                    Damage(other, responder, card, done, CardKind.Duel);
                }
            });
        }

        private void ResolveArea(Player actor, Card card, List<Player> targets, int index, Action done, Player damageSource)
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
                ResolveArea(actor, card, targets, index + 1, done, damageSource);
                return;
            }

            if (card.Kind == CardKind.Barbarians && ((target.RuleId == "menghuo" && target.HasAbility("祸首")) || (target.RuleId == "zhurong" && target.HasAbility("巨象"))))
            {
                EmitSkill(target, ((target.RuleId == "menghuo" && target.HasAbility("祸首"))) ? "祸首" : "巨象");
                AddLog($"{target.Hero.Name}的锁定技使南蛮入侵对其无效。");
                work.Enqueue(() => ResolveArea(actor, card, targets, index + 1, done, damageSource));
                return;
            }

            ResponseKind response = card.Kind == CardKind.Barbarians ? ResponseKind.Slash : ResponseKind.Dodge;
            Action next = () => work.Enqueue(() => ResolveArea(actor, card, targets, index + 1, done, damageSource));
            TrickEffect(actor, card.Kind, target, () => Request(target, response, $"{actor.Hero.Name}使用{card.Name}，请打出{(response == ResponseKind.Slash ? "杀" : "闪")}。",
                success =>
                {
                    if (success)
                    {
                        next();
                    }
                    else
                    {
                        Damage(damageSource, target, card, next, card.Kind);
                    }
                }), next);
        }

        private void Request(Player player, ResponseKind kind, string prompt, Action<bool> continuation, bool allowHujia = true)
        {
            if (DrainReactions(() => Request(player, kind, prompt, continuation, allowHujia))) return;
            if (allowHujia && kind == ResponseKind.Slash && (((player.RuleId == "liubei" && player.HasAbility("激将"))) || ((player.RuleId == "liushan" && player.HasAbility("激将"))) && player.Awakened) && player.Role == Role.Lord && Mode != GameMode.Team2V2)
            {
                OptionalSkill(player, "激将：是否请其他蜀角色代打杀？", true, use =>
                {
                    if (!use) Request(player, kind, prompt, continuation, false);
                    else AskJijiang(player, success => { if (success) continuation(true); else Request(player, kind, prompt, continuation, false); });
                }); return;
            }
            if (allowHujia && kind == ResponseKind.Dodge && ((player.RuleId == "caocao" && player.HasAbility("护驾")) || (player.RuleId == "re_caocao" && player.HasAbility("护驾"))) &&
                player.Role == Role.Lord && Mode != GameMode.Team2V2 &&
                Players.Any(other => other.IsAlive && other != player && other.Kingdom == "魏"))
            {
                RequestHujia(player, prompt, continuation);
                return;
            }

            if (kind == ResponseKind.Dodge && (player.Equipment.Any(card => card.Kind == CardKind.EightTrigrams) || ((player.RuleId == "wolong" && player.HasAbility("八阵"))) && !player.Equipment.Any(card => EquipmentSlot(card) == 1)))
            {
                OptionalSkill(player, "八卦阵：是否判定？红色视为打出闪。", true, use =>
                {
                    if (!use) { RequestPlain(player, kind, prompt, continuation); return; }
                    if (((player.RuleId == "wolong" && player.HasAbility("八阵"))) && !player.Equipment.Any(card => EquipmentSlot(card) == 1)) EmitSkill(player, "八阵");
                    EmitCue(GameCueKind.ArmorJudgment, player);
                    Judge(player, "八卦阵", judgment =>
                    {
                        if (judgment != null && RedFor(player, judgment)) CompleteResponse(player, kind, true, continuation);
                        else RequestPlain(player, kind, prompt, continuation);
                    });
                });
                return;
            }
            RequestPlain(player, kind, prompt, continuation);
        }

        private void RequestPlain(Player player, ResponseKind kind, string prompt, Action<bool> continuation)
        {
            if (CanGuhuo(player)) { GuhuoResponse(player, kind, prompt, continuation); return; }
            List<Card> equipmentResponses = player.Equipment.Where(card => CanTransform(player, card, kind) &&
                (((player.RuleId == "guanyu" && player.HasAbility("武圣"))) && kind == ResponseKind.Slash || ((player.RuleId == "huatuo" && player.HasAbility("急救"))) && kind == ResponseKind.Peach)).ToList();
            if (equipmentResponses.Count > 0)
            {
                List<Card> choices = player.Hand.Where(card => CanTransform(player, card, kind)).Concat(equipmentResponses).ToList();
                List<SkillDecisionOption> options = choices.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card) + (player.Equipment.Contains(card) ? "（装备转化）" : ""))).ToList();
                options.Add(new("no", "放弃响应"));
                Decide(player, prompt, options, choices.MinBy(KeepValue)!.Id.ToString(), key =>
                {
                    if (key == "no") CompletePhysicalResponse(player, kind, null, continuation);
                    else { Card card = choices.First(card => card.Id.ToString() == key); lastResponseCard = card; PayCard(player, card); ShowCard(card); EmitResponse(player, card, kind); CompletePhysicalResponse(player, kind, card, continuation); }
                });
                return;
            }
            if (player.IsHuman)
            {
                Phase = GamePhase.Response;
                Status = prompt;
                Pending = new ResponseRequest(kind, prompt, player, card =>
                {
                    lastResponseCard = card;
                    if (card != null)
                    {
                        DiscardFromHand(player, card);
                        player.PlayedSlash |= kind == ResponseKind.Slash && player == Current && player.InPlayPhase;
                        ShowCard(card);
                        EmitResponse(player, card, kind);
                        AddLog($"{player.Hero.Name}打出【{card.Name}】" +
                            (card.Kind.ToString() == kind.ToString() ? "。" : $"（{player.Hero.Skill}转换）。"));
                    }
                    else
                    {
                        AddLog($"{player.Hero.Name}放弃响应。");
                    }

                    CompletePhysicalResponse(player, kind, card, continuation);
                });
                return;
            }

            Card? response = player.Hand.FirstOrDefault(card => CanTransform(player, card, kind));
            lastResponseCard = response;
            if (response != null)
            {
                DiscardFromHand(player, response);
                player.PlayedSlash |= kind == ResponseKind.Slash && player == Current && player.InPlayPhase;
                ShowCard(response);
                EmitResponse(player, response, kind);
                AddLog($"{player.Hero.Name}打出【{response.Name}】响应。");
            }

            CompletePhysicalResponse(player, kind, response, continuation);
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
                EmitCue(GameCueKind.Healed, player, amount: 1);
                AddLog($"{player.Hero.Name}回复1点体力（{player.Health}/{player.MaxHealth}）。");
            }
        }

        private void Damage(Player source, Player victim, Card? card, Action done, CardKind? usedKind = null)
        {
            if (DrainReactions(() => Damage(source, victim, card, done, usedKind))) return;
            CardKind? kind = usedKind ?? card?.Kind;
            if (((victim.RuleId == "huaxiong" && victim.HasAbility("耀武"))) && source.IsAlive && kind == CardKind.Slash && card?.IsRed == true)
            {
                EmitSkill(victim, "耀武");
                List<SkillDecisionOption> options = new() { new("draw", "摸一张牌") };
                if (source.Health < source.MaxHealth)
                {
                    options.Add(new("heal", "回复1点体力"));
                }

                Decide(source, "华雄 · 耀武：伤害来源选择摸牌或回复体力", options,
                    source.Health < source.MaxHealth ? "heal" : "draw", choice =>
                    {
                        if (choice == "heal") HealWithChoices(source, () => ApplyDamage(source, victim, card, kind, done));
                        else { Draw(source, 1); ApplyDamage(source, victim, card, kind, done); }
                    });
                return;
            }

            ApplyDamage(source, victim, card, kind, done);
        }

        private void ApplyDamage(Player source, Player victim, Card? card, CardKind? kind, Action done)
        {
            int amount = (source.LuoyiActive && kind is CardKind.Slash or CardKind.Duel ? 2 : 1) + (kind == CardKind.Slash ? source.Mark("wineSlash") : 0);
            if (((source.SkillIdentity == "re_zhangfei" && source.HasAbility("咆哮"))) && !source.SkillsDisabled && kind == CardKind.Slash) { amount += source.Mark("paoxiaoBoost"); source.SetMark("paoxiaoBoost", 0); }
            if (kind == CardKind.Slash) amount += (cardUses.LastOrDefault(use => use.Actor == source && use.Kind == CardKind.Slash)?.DamageBonus ?? 0) + source.Mark("tiejiDamage") + (card != null && SuitFor(source, card) == "♥" ? victim.Mark($"yijueFrom{source.Seat}") : 0);
            DamageNature nature = kind == CardKind.Slash && card != null ? usedSlashNatures.GetValueOrDefault(card.Id, card.Nature) : DamageNature.Normal;
            ResolveDamageEvent(source, victim, card, kind, amount, nature, false, true, done);
        }

        private void Rescue(Player? source, Player victim, List<Player> rescuers, int index, Action done)
        {
            if (victim.Health > 0 || IsBuquAlive(victim))
            {
                done();
                return;
            }

            if (index >= rescuers.Count)
            {
                EliminateWithTriggers(source, victim, done);
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
                    HealWithChoices(victim, () =>
                    {
                        if (((victim.SkillIdentity == "sunquan" && victim.HasAbility("救援"))) && victim.Role == Role.Lord && Mode != GameMode.Team2V2 && rescuer != victim && rescuer.Kingdom == "吴")
                            HealWithChoices(victim, () => Rescue(source, victim, rescuers, index, done));
                        else Rescue(source, victim, rescuers, index, done);
                    });
                }
                else
                {
                    Rescue(source, victim, rescuers, index + 1, done);
                }
            });
        }

        private void Eliminate(Player? source, Player victim)
        {
            if (!victim.IsAlive) return; // One resolved death, one cue/reward/count.
            if (((victim.RuleId == "caiwenji" && victim.HasAbility("断肠"))) && source != victim && source?.IsAlive == true) { source.SkillsDisabled = true; source.Incarnations.Clear(); source.IncarnationHeroId = null; source.IncarnationSkill = null; DiscardPile.AddRange(source.Zone("田")); source.Zone("田").Clear(); source.LuoyiActive = false; source.TianyiState = 0; source.LuoshenCards.Clear();
                DiscardPile.AddRange(source.SpecialCards); source.SpecialCards.Clear(); EmitSkill(victim, "断肠"); }
            victim.IsAlive = false; victim.Incarnations.Clear(); victim.IncarnationHeroId = null; victim.IncarnationSkill = null;
            DiscardPile.AddRange(victim.NamedCards); victim.NamedZones.Clear();
            DiscardPile.AddRange(victim.SpecialCards); victim.SpecialCards.Clear(); victim.SupplyShortages.Clear(); victim.LuoshenCards.Clear();
            DiscardPile.AddRange(victim.Hand);
            DiscardPile.AddRange(victim.Equipment);
            DiscardPile.AddRange(victim.Judgments);
            victim.Hand.Clear();
            victim.Equipment.Clear();
            victim.Judgments.Clear();
            AddLog($"{victim.Hero.Name}阵亡，身份公开：{victim.RoleName}。");
            EliminationInfo elimination = RecordElimination(source, victim);
            EmitCue(GameCueKind.Eliminated, victim, source: source, elimination: elimination);

            if (CheckVictory())
            {
                return;
            }

            if (Mode == GameMode.Team2V2)
            {
                return;
            }

            if (victim.Role == Role.Rebel && source?.IsAlive == true)
            {
                Draw(source, 3);
                AddLog($"{source.Hero.Name}击败反贼，奖励摸三张牌。");
            }
            else if (victim.Role == Role.Loyalist && source?.Role == Role.Lord && source.IsAlive)
            {
                DiscardPile.AddRange(source.Hand);
                DiscardPile.AddRange(source.Equipment);
                int lostHand = source.Hand.Count; int lostEquipment = source.Equipment.Count;
                source.Hand.Clear(); source.Equipment.Clear();
                OnHandLost(source, lostHand);
                for (int index = 0; index < lostEquipment; index++) OnEquipmentLost(source);
                AddLog("主公误杀忠臣，弃置所有手牌与装备。");
            }
        }

        private bool CheckVictory()
        {
            List<Player> alive = Players.Where(player => player.IsAlive).ToList();
            if (alive.Any(player => player.Health <= 0 && !IsBuquAlive(player))) return false;
            if (Mode == GameMode.Team2V2)
            {
                if (alive.Select(player => player.Team).Distinct().Count() > 1)
                {
                    return false;
                }

                Phase = GamePhase.GameOver;
                Pending = null;
                PendingCardChoice = null;
                PendingDecision = null;
                work.Clear(); reactions.Clear(); DiscardPile.AddRange(OrderingCards); OrderingCards.Clear();
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
            PendingCardChoice = null;
            PendingDecision = null;
            work.Clear(); reactions.Clear(); DiscardPile.AddRange(OrderingCards); OrderingCards.Clear();
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
            string oldStatus = Status;
            bool success = ExecuteActiveSkill(Human, card, target, second);
            if (!success && Status == oldStatus) Status = "技能条件不满足：" + Human.Hero.Description;
            Notify();
            return success;
        }
    }
}

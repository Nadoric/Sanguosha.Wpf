namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private static string SuitFor(Player owner, Card card) => ((owner.RuleId == "xiaoqiao" && owner.HasAbility("红颜"))) && card.Suit == "♠" ? "♥" : card.Suit;
    private static bool RedFor(Player owner, Card card) => SuitFor(owner, card) is "♥" or "♦";
    private static IEnumerable<Card> He(Player player) => player.Hand.Concat(player.Equipment);
    private static string CardLabel(Card card) => $"{card.Suit}{card.RankText}【{card.Name}】";

    private void PayCard(Player owner, Card card)
    {
        if (owner.Hand.Contains(card)) DiscardFromHand(owner, card);
        else if (owner.Equipment.Remove(card)) { DiscardPile.Add(card); OnEquipmentLost(owner); }
    }

    private void ChooseOwnCards(Player owner, string prompt, int minimum, int maximum, bool equipment,
        Action<List<Card>> done, Func<Card, bool>? filter = null)
    {
        if (DrainReactions(() => ChooseOwnCards(owner, prompt, minimum, maximum, equipment, done, filter))) return;
        List<Card> selected = new();
        List<Card> candidates = (equipment ? He(owner) : owner.Hand).Where(filter ?? (_ => true)).ToList();
        maximum = Math.Min(maximum, candidates.Count);
        if (candidates.Count < minimum) { done(selected); return; }
        void Pick()
        {
            if (selected.Count >= maximum) { done(selected); return; }
            List<SkillDecisionOption> options = candidates.Where(card => !selected.Contains(card))
                .Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card) + (owner.Equipment.Contains(card) ? "（装备区）" : ""))).ToList();
            if (selected.Count >= minimum) options.Add(new("done", "完成选择"));
            string ai = selected.Count >= minimum && !(prompt.StartsWith("界仁德") && selected.Count < Math.Min(2, maximum)) ? "done" : candidates.Except(selected).MinBy(KeepValue)!.Id.ToString();
            Decide(owner, $"{prompt}（已选{selected.Count}，需{minimum}至{maximum}张）", options, ai, key =>
            {
                if (key == "done") done(selected);
                else { selected.Add(candidates.First(card => card.Id.ToString() == key)); Pick(); }
            });
        }
        Pick();
    }

    private void DiscardOwnCards(Player owner, string prompt, int count, bool equipment, Action done)
    {
        ChooseOwnCards(owner, prompt, Math.Min(count, (equipment ? He(owner) : owner.Hand).Count()), count, equipment, cards =>
        { PayCards(owner, cards); done(); });
    }

    private void ChoosePlayer(Player owner, string prompt, IEnumerable<Player> candidates, bool optional,
        Func<Player, double> score, Action<Player?> done)
    {
        List<Player> players = candidates.Where(player => player.IsAlive).ToList();
        if (players.Count == 0) { done(null); return; }
        List<SkillDecisionOption> options = players.Select(player => new SkillDecisionOption(player.Seat.ToString(), $"{player.Seat + 1}号 · {player.Hero.Name}")).ToList();
        if (optional) options.Add(new("no", "不发动 / 完成"));
        Player best = players.MaxBy(score)!;
        Decide(owner, prompt, options, optional && score(best) <= 0 ? "no" : best.Seat.ToString(), key =>
            done(key == "no" ? null : players.First(player => player.Seat.ToString() == key)));
    }

    // The current judgment is always in the discard zone, including while a replacement decision is open.
    // Results are passed only after every eligible replacement and 天妒 has resolved.
    private void Judge(Player judged, string reason, Action<Card?> done)
    {
        if (DrainReactions(() => Judge(judged, reason, done))) return;
        Card? result = TakeTop();
        if (result == null) { done(null); return; }
        judgmentMaterials.Add(result.Id); DiscardPile.Add(result); ShowCard(result);
        List<Player> replacers = Enumerable.Range(0, Players.Count).Select(offset => Players[(CurrentSeat + offset) % Players.Count])
            .Where(player => player.IsAlive && ((player.RuleId == "simayi" && player.HasAbility("鬼才")) || (player.RuleId == "re_simayi" && player.HasAbility("鬼才")) || (player.RuleId == "zhangjiao" && player.HasAbility("鬼道")))).ToList();
        void Finish() { judgmentMaterials.Remove(result.Id); done(result); }
        void Replace(int index)
        {
            if (index >= replacers.Count)
            {
                AddLog($"{judged.Hero.Name}·{reason}判定：{SuitFor(judged, result)}{result.RankText}。");
                if (((judged.RuleId == "guojia" && judged.HasAbility("天妒"))) && DiscardPile.Contains(result))
                    OptionalSkill(judged, $"天妒：是否获得{reason}的判定牌{CardLabel(result)}？", true, use =>
                    { if (use && DiscardPile.Remove(result)) { Gain(judged, result); EmitSkill(judged, "天妒"); } Songwei(judged, result, Finish); });
                else Songwei(judged, result, Finish);
                return;
            }
            Player chooser = replacers[index];
            List<Card> cards = (((chooser.RuleId == "re_simayi" && chooser.HasAbility("鬼才")) || (chooser.RuleId == "zhangjiao" && chooser.HasAbility("鬼道"))) ? He(chooser) : chooser.Hand).Where(card => !((chooser.RuleId == "zhangjiao" && chooser.HasAbility("鬼道"))) || !RedFor(chooser, card)).ToList();
            if (cards.Count == 0) { Replace(index + 1); return; }
            List<SkillDecisionOption> options = cards.Select(card => new SkillDecisionOption(card.Id.ToString(), CardLabel(card))).ToList();
            options.Add(new("no", "不改判"));
            // Basic AI changes only a clearly beneficial delayed/armor judgment; it never sees hidden opponent cards.
            double Value(Card card) => reason switch { "乐不思蜀" => SuitFor(judged, card) == "♥" ? 1 : -1, "兵粮寸断" => SuitFor(judged, card) == "♣" ? 1 : -1, "屯田" => SuitFor(judged, card) != "♥" ? 1 : -1, "雷击" => SuitFor(judged, card) == "♠" ? -1 : 1, "闪电" => SuitFor(judged, card) == "♠" && card.Rank is >= 2 and <= 9 ? -1 : 1, "八卦阵" or "洛神" => RedFor(judged, card) == (reason == "八卦阵") ? 1 : -1, _ => 0 };
            Card best = cards.OrderBy(KeepValue).MaxBy(card => Value(card) * (Allied(chooser, judged) ? 1 : -1))!;
            double improvement = (Value(best) - Value(result)) * (Allied(chooser, judged) ? 1 : -1);
            Decide(chooser, $"{(((chooser.RuleId == "zhangjiao" && chooser.HasAbility("鬼道"))) ? "鬼道" : "鬼才")}：{judged.Hero.Name}·{reason}判定{CardLabel(result)}，可替换", options,
                improvement > 0 ? best.Id.ToString() : "no", key =>
                {
                    if (key != "no") { Card card = cards.First(card => card.Id.ToString() == key); PayCard(chooser, card); if (((chooser.RuleId == "zhangjiao" && chooser.HasAbility("鬼道"))) && DiscardPile.Remove(result)) Gain(chooser, result); judgmentMaterials.Remove(result.Id); result = card; judgmentMaterials.Add(result.Id); ShowCard(card); EmitSkill(chooser, ((chooser.RuleId == "zhangjiao" && chooser.HasAbility("鬼道"))) ? "鬼道" : "鬼才"); }
                    Replace(index + 1);
                });
        }
        Replace(0);
    }

    private void Guanxing(Player actor, Action done)
    {
        int desired = ((actor.SkillIdentity == "re_zhugeliang" && actor.HasAbility("空城"))) ? (Players.Count(player => player.IsAlive) < 4 ? 3 : 5) : Math.Min(5, Players.Count(player => player.IsAlive));
        if (deck.Count < desired && DiscardPile.Count > 0)
        {
            List<Card> refill = DiscardPile.Where(card => !InFlight(card)).ToList(); foreach (Card card in refill) DiscardPile.Remove(card); Shuffle(refill); deck.InsertRange(0, refill);
            AddLog("观星前牌堆不足，洗混弃牌堆补至现有牌堆下方。");
        }
        int count = Math.Min(desired, deck.Count);
        if (count == 0) { done(); return; }
        List<Card> remaining = deck.TakeLast(count).Reverse().ToList();
        List<Card> top = new(), bottom = new();
        void Pick()
        {
            if (remaining.Count == 0)
            {
                foreach (Card card in top.Concat(bottom)) deck.Remove(card);
                deck.InsertRange(0, bottom.AsEnumerable().Reverse()); deck.AddRange(top.AsEnumerable().Reverse());
                if (((actor.SkillIdentity == "re_zhugeliang" && actor.HasAbility("空城")))) actor.SetMark("guanxingBottom", top.Count == 0 ? 1 : 0);
                EmitSkill(actor, "观星"); AddLog($"{actor.Hero.Name}观星：{top.Count}张置顶，{bottom.Count}张置底。"); done(); return;
            }
            List<SkillDecisionOption> options = remaining.SelectMany(card => new[] {
                new SkillDecisionOption($"top:{card.Id}", $"置顶 · {CardLabel(card)}"),
                new SkillDecisionOption($"bottom:{card.Id}", $"置底 · {CardLabel(card)}") }).ToList();
            Card best = remaining.MaxBy(KeepValue)!;
            Decide(actor, "观星：按未来摸牌先后逐张选择牌及顶/底位置（仅你可见）", options, $"top:{best.Id}", key =>
            { string[] parts = key.Split(':'); Card card = remaining.First(card => card.Id == int.Parse(parts[1])); remaining.Remove(card); (parts[0] == "top" ? top : bottom).Add(card); Pick(); });
        }
        Pick();
    }

    private void Luoshen(Player actor, int remaining, Action done)
    {
        if (remaining <= 0) { done(); return; }
        OptionalSkill(actor, "洛神：是否进行一次判定？黑色可获得并继续。", true, use =>
        {
            if (!use) { done(); return; }
            EmitSkill(actor, "洛神");
            Judge(actor, "洛神", card =>
            {
                if (card == null || RedFor(actor, card)) { done(); return; }
                if (DiscardPile.Remove(card)) { Gain(actor, card); if (((actor.RuleId == "re_zhenji" && actor.HasAbility("洛神")))) actor.LuoshenCards.Add(card.Id); }
                work.Enqueue(() => Luoshen(actor, remaining - 1, done));
            });
        });
    }

    private CardKind DelayedKind(Player player, Card card) => player.SupplyShortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage ? CardKind.SupplyShortage : card.Kind == CardKind.Lightning ? CardKind.Lightning : CardKind.Indulgence;
    private void PassLightning(Player actor, Card lightning)
    {
        Player? next = Enumerable.Range(1, Players.Count).Select(offset => Players[(actor.Seat + offset) % Players.Count]).FirstOrDefault(player => player.IsAlive && !player.Judgments.Any(item => DelayedKind(player, item) == CardKind.Lightning) && !(((player.RuleId == "jiaxu" && player.HasAbility("帷幕"))) && !lightning.IsRed));
        if (next != null && DiscardPile.Remove(lightning)) { next.Judgments.Add(lightning); AddLog($"闪电传至{next.Hero.Name}的判定区。"); }
    }
    private void ResolveJudgments(Player actor, bool skipPlay = false, bool skipDraw = false, Action<bool, bool>? finished = null, HashSet<int>? seen = null)
    {
        seen ??= new(); Card? delayed = actor.Judgments.LastOrDefault(card => !seen.Contains(card.Id));
        if (delayed == null)
        {
            if (finished != null) { finished(skipPlay, skipDraw); return; }
            if (skipDraw) FinishDrawPhase(actor, skipPlay); else ResolveDrawPhase(actor, skipPlay); return;
        }
        seen.Add(delayed.Id); CardKind kind = DelayedKind(actor, delayed);
        judgmentMaterials.Add(delayed.Id);
        actor.Judgments.Remove(delayed); actor.SupplyShortages.Remove(delayed.Id); DiscardPile.Add(delayed);
        void Continue(bool play, bool draw) { judgmentMaterials.Remove(delayed.Id); ResolveJudgments(actor, play, draw, finished, seen); }
        Counterspell(actor, kind, actor, false, CurrentSeat, 0, negated =>
        {
            if (negated) { if (kind == CardKind.Lightning) PassLightning(actor, delayed); Continue(skipPlay, skipDraw); return; }
            Qianxun(actor, () => Judge(actor, new Card(0, kind, "", 0).Name, card =>
            {
                if (kind == CardKind.Lightning)
                {
                    if (card != null && SuitFor(actor, card) == "♠" && card.Rank is >= 2 and <= 9) ElementalDamage(null, actor, delayed, 3, DamageNature.Thunder, () => { if (actor.IsAlive) Continue(skipPlay, skipDraw); else { judgmentMaterials.Remove(delayed.Id); NextTurn(); } });
                    else { PassLightning(actor, delayed); Continue(skipPlay, skipDraw); } return;
                }
                bool failed = card != null && SuitFor(actor, card) != (kind == CardKind.SupplyShortage ? "♣" : "♥");
                Continue(skipPlay || failed && kind == CardKind.Indulgence, skipDraw || failed && kind == CardKind.SupplyShortage);
            }));
        });
    }
}

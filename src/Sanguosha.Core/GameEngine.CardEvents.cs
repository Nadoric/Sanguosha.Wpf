namespace Sanguosha.Core;

public enum CardUseStage { Declared, TargetEffect, Counterspell, EffectCanceled, Finished }
public sealed record CardUseEvent(int UseId, CardUseStage Stage, int SourceSeat, CardKind Kind, IReadOnlyList<int> MaterialIds, IReadOnlyList<int> TargetSeats, int? EffectTarget = null);
internal sealed record CardUseFrame(int Id, Player Actor, CardKind Kind, List<Card> Materials, List<Player> Targets) { public HashSet<int> Excluded { get; } = new(); public int DamageBonus { get; set; } }

public sealed partial class GameEngine
{
    public int PhysicalCardCount { get; private set; } = 119;
    public List<CardUseEvent> CardEvents { get; } = new();
    private readonly List<CardUseFrame> cardUses = new();
    private readonly Queue<int> extraTurns = new();
    private int? extraTurnOrigin;
    private int useNumber;
    private readonly HashSet<int> reservedUseMaterials = new();
    private readonly HashSet<int> judgmentMaterials = new();
    private bool InFlight(Card card) => reservedUseMaterials.Contains(card.Id) || judgmentMaterials.Contains(card.Id) || cardUses.Any(frame => frame.Materials.Contains(card));
    private readonly List<Player> dyingPlayers = new();
    private static bool Trick(CardKind kind) => new Card(0, kind, "", 0).IsTrick;
    private static bool Delayed(CardKind kind) => kind is CardKind.Indulgence or CardKind.SupplyShortage or CardKind.Lightning;
    private void Publish(CardUseFrame frame, CardUseStage stage, Player? target = null)
    {
        CardEvents.Add(new(frame.Id, stage, frame.Actor.Seat, frame.Kind, frame.Materials.Select(card => card.Id).ToArray(), frame.Targets.Select(player => player.Seat).ToArray(), target?.Seat));
        if (CardEvents.Count > 4096) CardEvents.RemoveRange(0, 1024);
    }
    private void BeginCardUse(Player actor, CardKind kind, IEnumerable<Card> materials, IEnumerable<Player> targets, Action<Action> resolve, Action done)
    {
        List<Player> chosenTargets = targets.ToList();
        void Declare()
        {
            CardUseFrame frame = new(++useNumber, actor, kind, materials.ToList(), chosenTargets); cardUses.Add(frame); foreach (Card material in frame.Materials) reservedUseMaterials.Remove(material.Id); Publish(frame, CardUseStage.Declared);
            OnCardDeclared(frame, () => resolve(() => { Publish(frame, CardUseStage.Finished); cardUses.Remove(frame); AfterLosses(done); }));
        }
        if (kind == CardKind.Slash && actor.TianyiState > 0 && chosenTargets.Count == 1)
            ChoosePlayer(actor, "天义：可为此杀增加另一名目标，或不增加", Players.Where(player => player != actor && !chosenTargets.Contains(player) && !(((player.RuleId == "zhugeliang" && player.HasAbility("空城"))) && player.Hand.Count == 0)), true,
                player => Hostility(actor, player), extra => { if (extra != null) chosenTargets.Add(extra); Declare(); });
        else Declare();
    }
    private List<Card> DamageMaterials(Card? representative)
    {
        CardUseFrame? frame = cardUses.LastOrDefault(use => representative != null && (use.Materials.Contains(representative) || representative.Id < 0));
        return frame?.Materials ?? (representative?.Id > 0 ? new() { representative } : new());
    }
    private void ObtainDamageCards(Player player, Card? representative)
    {
        List<Card> material = DamageMaterials(representative);
        if (material.Count > 0 && material.All(DiscardPile.Contains)) { foreach (Card card in material) DiscardPile.Remove(card); Gain(player, material); }
    }
    private void VirtualCard(Player actor, CardKind kind, List<Card> materials, List<Player> targets, string skill, Action done, bool counterable = true)
    {
        foreach (Card card in materials) reservedUseMaterials.Add(card.Id);
        List<Card> named = materials.Where(actor.NamedCards.Contains).ToList();
        PayCards(actor, materials.Except(named));
        foreach (Card card in named) foreach (List<Card> zone in actor.NamedZones.Values) zone.Remove(card);
        foreach (Card card in materials) { if (!DiscardPile.Contains(card)) DiscardPile.Add(card); ShowCard(card); }
        Card virtualCard = new(-(++useNumber), kind, materials.Count == 1 ? materials[0].Suit : "", 0);
        AfterLosses(() => BeginCardUse(actor, kind, materials, targets, finish =>
        {
            EmitSkill(actor, skill); EmitCue(GameCueKind.CardPlayed, actor, kind, target: targets.FirstOrDefault(), physicalCard: materials.FirstOrDefault());
            if (kind is CardKind.Arrows or CardKind.Barbarians)
                ResolveArea(actor, virtualCard, targets, 0, finish, actor);
            else if (kind == CardKind.Steal) TrickEffect(actor, kind, targets[0], () => ChooseOtherCardIncludingJudgment(actor, targets[0], true, finish), finish);
            else if (kind == CardKind.Duel) { if (counterable) TrickEffect(actor, kind, targets[0], () => Duel(actor, targets[0], virtualCard, targets[0], actor, finish), finish); else Duel(actor, targets[0], virtualCard, targets[0], actor, finish); }
            else if (kind == CardKind.FireAttack) TrickEffect(actor, kind, targets[0], () => ResolveFireAttack(actor, targets[0], virtualCard, finish), finish);
            else if (kind == CardKind.Wine) ResolveCardEffect(actor, virtualCard, actor, kind, finish);
            else finish();
        }, done));
    }
    private void ChooseOtherCardIncludingJudgment(Player actor, Player target, bool steal, Action done)
    {
        if (actor.IsHuman) RequestTargetCard(target, steal, () => AfterLosses(done));
        else { RemoveTargetCard(actor, target, steal); AfterLosses(done); }
    }
    private void TrickEffect(Player actor, CardKind kind, Player target, Action effect, Action canceled)
    {
        if (!target.IsAlive || Phase == GamePhase.GameOver) { canceled(); return; }
        CardUseFrame? frame = cardUses.LastOrDefault(); if (frame != null) Publish(frame, CardUseStage.TargetEffect, target);
        if (frame?.Excluded.Contains(target.Seat) == true) { Publish(frame, CardUseStage.EffectCanceled, target); canceled(); return; }
        if (((target.RuleId == "jiaxu" && target.HasAbility("帷幕"))) && cardUses.LastOrDefault()?.Materials is { Count: > 0 } cards && cards.All(card => !card.IsRed)) { EmitSkill(target, "帷幕"); canceled(); return; }
        void Counter() => Counterspell(actor, kind, target, false, CurrentSeat, 0, negated =>
        {
            if (negated) { if (frame != null) Publish(frame, CardUseStage.EffectCanceled, target); canceled(); }
            else effect();
        });
        if (actor != target && !Delayed(kind) && frame?.Targets.Count == 1) Qianxun(target, Counter); else Counter();
    }
    private void Counterspell(Player actor, CardKind kind, Player target, bool negated, int seat, int passed, Action<bool> done)
    {
        if (passed >= Players.Count || Phase == GamePhase.GameOver) { done(negated); return; }
        Player responder = Players[seat]; int next = (seat + 1) % Players.Count;
        List<Card> candidates = responder.Hand.Where(card => !responder.HandUseBanned && ( card.Kind == CardKind.Nullification || ((responder.RuleId == "wolong" && responder.HasAbility("看破"))) && !RedFor(responder, card))).ToList();
        if (!responder.IsAlive || candidates.Count == 0 && !CanGuhuo(responder)) { Counterspell(actor, kind, target, negated, next, passed + 1, done); return; }
        bool harmful = kind is not (CardKind.DrawTwo or CardKind.Feast or CardKind.IronChain);
        bool wantsNegated = harmful ? Allied(responder, target) : !Allied(responder, target);
        List<SkillDecisionOption> options = candidates.Select(card => new SkillDecisionOption(card.Id.ToString(), "使用无懈 · " + CardLabel(card))).ToList(); if (CanGuhuo(responder)) options.Add(new("guhuo", "蛊惑声明无懈可击")); options.Add(new("no", "不使用无懈可击"));
        Decide(responder, $"{(negated ? "反无懈" : "无懈可击")}：{actor.Hero.Name}的{new Card(0, kind, "", 0).Name}对{target.Hero.Name}（当前{(negated ? "已被抵消" : "将生效")}）", options,
            wantsNegated != negated && candidates.Count > 0 ? candidates.MinBy(KeepValue)!.Id.ToString() : "no", key =>
        {
            if (key == "no") { Counterspell(actor, kind, target, negated, next, passed + 1, done); return; }
            if (key == "guhuo") { GuhuoCounterspell(responder, actor, kind, target, negated, seat, passed, done); return; }
            Card response = candidates.First(card => card.Id.ToString() == key); reservedUseMaterials.Add(response.Id); PayCard(responder, response); if (response.Kind != CardKind.Nullification) EmitSkill(responder, "看破");
            ShowCard(response); EmitCue(GameCueKind.CardPlayed, responder, CardKind.Nullification, target: target, physicalCard: response);
            AfterLosses(() => BeginCardUse(responder, CardKind.Nullification, new[] { response }, new[] { target }, finish => { Publish(cardUses.Last(), CardUseStage.Counterspell, target); finish(); }, () => Counterspell(actor, kind, target, !negated, next, 0, done)));
        });
    }
    private void ResolveFireAttack(Player actor, Player target, Card card, Action done)
    {
        if (target.Hand.Count == 0 || !actor.IsAlive) { done(); return; }
        Decide(target, "火攻：展示一张手牌", target.Hand.Select(item => new SkillDecisionOption(item.Id.ToString(), CardLabel(item))).ToList(), target.Hand.MinBy(KeepValue)!.Id.ToString(), key =>
        {
            Card revealed = target.Hand.First(item => item.Id.ToString() == key); ShowCard(revealed); AddLog($"火攻展示：{target.Hero.Name}的{CardLabel(revealed)}。");
            List<Card> costs = actor.Hand.Where(item => SuitFor(actor, item) == SuitFor(target, revealed)).ToList();
            List<SkillDecisionOption> options = costs.Select(item => new SkillDecisionOption(item.Id.ToString(), "弃置 · " + CardLabel(item))).ToList(); options.Add(new("no", "不弃牌，结束火攻"));
            Decide(actor, "火攻：可弃同花色手牌，造成1点火焰伤害", options, costs.Count > 0 && Hostility(actor, target) > 0 ? costs.MinBy(KeepValue)!.Id.ToString() : "no", cost =>
            { if (cost == "no") done(); else { PayCard(actor, costs.First(item => item.Id.ToString() == cost)); AfterLosses(() => ElementalDamage(actor, target, card, 1, DamageNature.Fire, done)); } });
        });
    }
    // A recast is not a card use: no 集智/奋威/无懈 trigger. Choose targets before publishing use.
    private void ResolveIronChain(Player actor, Card card, Action done, bool alreadyPaid = true, string? skill = null)
    {
        void Pay() { if (!alreadyPaid) PayCard(actor, card); ShowCard(card); if (skill != null) EmitSkill(actor, skill); }
        Decide(actor, "铁索连环：重铸或选择一至两名目标", new[] { new SkillDecisionOption("recast", "重铸，摸一张牌"), new SkillDecisionOption("chain", "选择横置/重置目标") }, "recast", key =>
        {
            if (key == "recast") { reservedUseMaterials.Remove(card.Id); Pay(); AfterLosses(() => { Draw(actor, 1); done(); }); return; }
            List<Player> legal = Players.Where(player => player.IsAlive && !(((player.RuleId == "jiaxu" && player.HasAbility("帷幕"))) && !card.IsRed)).ToList();
            ChoosePlayer(actor, "铁索第一目标", legal, false, player => player.Chained ? -Hostility(actor, player) : Hostility(actor, player), first =>
                ChoosePlayer(actor, "铁索第二目标（可不选）", legal.Where(player => player != first), true, player => player.Chained ? -Hostility(actor, player) : Hostility(actor, player), second =>
                {
                    List<Player> targets = new() { first! }; if (second != null) targets.Add(second); reservedUseMaterials.Add(card.Id); Pay();
                    EmitCue(GameCueKind.CardPlayed, actor, CardKind.IronChain, target: first, physicalCard: card);
                    AfterLosses(() => BeginCardUse(actor, CardKind.IronChain, new[] { card }, targets, finish =>
                    { void Apply(int index) { if (index == targets.Count) finish(); else TrickEffect(actor, CardKind.IronChain, targets[index], () => { targets[index].Chained = !targets[index].Chained; Apply(index + 1); }, () => Apply(index + 1)); } Apply(0); }, done));
                }));
        });
    }
    private void StartVirtualDuel(Player actor, Player target, Action done, bool counterable = true) => VirtualCard(actor, CardKind.Duel, new(), new() { target }, "决斗", done, counterable);
    private bool ProtectedEquipment(Player actor, Player target, Card card, bool steal) => !steal && actor != target && ((target.SkillIdentity == "re_huangyueying" && target.HasAbility("奇才"))) && !target.SkillsDisabled && EquipmentSlot(card) == 1;
    private int AvailableTargetCards(Player actor, Player target, bool steal, bool judgments = true) => target.Hand.Count + target.Equipment.Count(card => !ProtectedEquipment(actor, target, card, steal)) + (judgments ? target.Judgments.Count : 0);
    public bool CanActivateSkill(Player actor) => Phase == GamePhase.Play && actor == Current && actor.IsAlive &&
        (HasGlobalActive(actor) || actor.HasActiveAbility && (!actor.SkillUsed || actor.RuleId is "huanggai" or "liubei" or "pangtong" or "zhangzhaozhanghong" or "guanyu" or "ganning" or "daqiao" or "xuhuang") && (actor.HasAbility("蛊惑") && actor.Hand.Count > 0 || actor.HasAbility("义绝") && actor.Mark("yijueUsed") == 0 || IsBatchThreeActive(actor) || actor.RuleId is "liubei" or "sunquan" or "sunshangxiang" or "huatuo" or "diaochan" or "huanggai" or "dianwei" or "re_huanggai" or "re_diaochan" or "re_zhouyu" or "zhouyu" or "pangtong" or "taishici" or "lusu" or "jiangwei" or "zhangzhaozhanghong" or "re_sunshangxiang" or "guanyu" or "ganning" or "daqiao" or "xuhuang"));
    private void ConfigureDynamicHandLimits()
    {
        foreach (Player player in Players) player.LordHandBonus = () => ((player.RuleId == "yuanshao" && player.HasAbility("血裔"))) && Lord(player) ? 2 * Players.Count(other => other.IsAlive && other != player && other.Kingdom == "群") : 0;
    }
    private void ResetBatchThreeTurn(Player actor)
    {
        foreach (Player player in Players) { player.SetMark("turnSilence", 0); player.SetMark("qingnangTarget", 0); player.SetMark("jijiangDraw", 0); player.SetMark("handBonus", 0); player.SetMark("wangzunPenalty", 0); }
        foreach (string key in new[] { "wine", "wineUsed", "wineSlash", "handBonus", "wangzunPenalty", "panfengUsed", "jijieUsed", "fangquan", "guanxingBottom", "qingnangOff", "zhibaUsed", "huangtianUsed", "paoxiaoBoost", "yijueUsed", "guhuoAiCount" }) actor.SetMark(key, 0);
        foreach (string key in actor.Marks.Keys.Where(key => key.StartsWith("rendeTo")).ToList()) actor.SetMark(key, 0);
    }
}

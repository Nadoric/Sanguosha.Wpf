namespace Sanguosha.Core;

public sealed partial class GameEngine
{
    private bool CanGuhuo(Player actor) => actor.IsAlive && actor.HasAbility("蛊惑") && !actor.HandUseBanned && actor.Hand.Count > 0;
    private static IEnumerable<Card> GuhuoClaims() => Enum.GetValues<CardKind>().Where(kind => { Card card = new(0, kind, "", 0); return !card.IsEquipment && !card.IsDelayed; })
        .SelectMany(kind => kind == CardKind.Slash ? Enum.GetValues<DamageNature>().Select(nature => new Card(0, kind, "", 0, nature)) : new[] { new Card(0, kind, "", 0) });
    private static string ClaimKey(Card card) => $"{card.Kind}:{card.Nature}";
    private string? ValidateGuhuo(Player actor, Card material, Card claim, Player? target)
    {
        if (!CanGuhuo(actor) || !actor.Hand.Contains(material)) return "蛊惑需要可使用的一张手牌。";
        // Target selection sees an unknown suit; the actual suit is restored only after declaration, just as old_guhuo backup/precontent does.
        int index = actor.Hand.IndexOf(material); Card temporary = claim with { Id = material.Id };
        actor.Hand[index] = temporary;
        try { return ValidatePlay(actor, temporary, target); } finally { actor.Hand[index] = material; }
    }
    private bool BeginGuhuoPlay(Player actor, Card? selected, Player? target)
    {
        if (!CanGuhuo(actor)) return false;
        Phase = GamePhase.Resolving;
        void ChooseClaim(Card material)
        {
            List<Card> claims = GuhuoClaims().Where(claim => claim.Kind is not (CardKind.Dodge or CardKind.Nullification))
                .Where(claim => NeedsTarget(claim.Kind) ? Players.Any(player => ValidateGuhuo(actor, material, claim, player) == null) : ValidateGuhuo(actor, material, claim, actor) == null).ToList();
            var options = claims.Select(claim => new SkillDecisionOption(ClaimKey(claim), "声明 · " + claim.Name)).ToList(); options.Add(new("no", "取消声明"));
            Card? preferred = claims.FirstOrDefault(claim => claim.Kind == CardKind.Peach && actor.Health < actor.MaxHealth) ?? claims.FirstOrDefault(claim => claim.Kind == material.Kind && claim.Nature == material.Nature);
            Decide(actor, "蛊惑：声明一种基本牌或非延时锦囊", options, preferred != null ? ClaimKey(preferred) : "no", key =>
            {
                if (key == "no") { CompleteAction(actor); return; }
                Card claim = claims.First(claim => ClaimKey(claim) == key);
                if (NeedsTarget(claim.Kind))
                    ChoosePlayer(actor, "蛊惑：选择声明牌的合法目标", Players.Where(player => ValidateGuhuo(actor, material, claim, player) == null), false, player => Hostility(actor, player), chosen => PlayGuhuo(actor, material, claim, new() { chosen! }, () => CompleteAction(actor)));
                else if (claim.Kind == CardKind.IronChain)
                    ChoosePlayer(actor, "蛊惑铁索：第一目标（虚拟牌不可重铸）", Players, false, player => player.Chained ? -Hostility(actor, player) : Hostility(actor, player), first =>
                        ChoosePlayer(actor, "蛊惑铁索：可选另一目标", Players.Where(player => player != first), true, player => player.Chained ? -Hostility(actor, player) : Hostility(actor, player), second =>
                        { var targets = new List<Player> { first! }; if (second != null) targets.Add(second); PlayGuhuo(actor, material, claim, targets, () => CompleteAction(actor)); }));
                else
                {
                    List<Player> targets = claim.Kind is CardKind.Arrows or CardKind.Barbarians ? Players.Where(player => player.IsAlive && player != actor).ToList() : claim.Kind == CardKind.Feast ? Players.Where(player => player.IsAlive).OrderBy(player => (player.Seat - actor.Seat + Players.Count) % Players.Count).ToList() : new() { actor };
                    PlayGuhuo(actor, material, claim, targets, () => CompleteAction(actor));
                }
            });
        }
        if (selected != null && actor.Hand.Contains(selected)) ChooseClaim(selected);
        else ChooseOwnCards(actor, "蛊惑：秘密选择一张手牌作为声明材料", 1, 1, false, cards => ChooseClaim(cards[0]));
        return true;
    }
    private void GuhuoAttempt(Player actor, Card material, Card claim, List<Player> targets, Action<bool, Card> done)
    {
        Card declared = claim with { Id = -(++useNumber), Suit = SuitFor(actor, material), Rank = material.Rank };
        actor.Hand.Remove(material); OrderingCards.Add(material); reservedUseMaterials.Add(material.Id); PublishHand(actor, false, new[] { material }); OnHandLost(actor); EmitSkill(actor, "蛊惑");
        AddLog($"{actor.Hero.Name}以一张暗置手牌声明【{declared.Name}】{(targets.Count > 0 ? "，目标：" + string.Join("、", targets.Select(player => player.Hero.Name)) : "")}。");
        List<Player> challengers = new();
        List<Player> observers = Enumerable.Range(0, Players.Count).Select(offset => Players[(CurrentSeat + offset) % Players.Count]).Where(player => player.IsAlive && player != actor && player.Health > 0).ToList();
        void Ask(int index)
        {
            if (index >= observers.Count) { Reveal(); return; }
            Player observer = observers[index];
            // AI sees the declaration and public state only, never material.Kind / Suit.
            string ai = observer.Health > 1 && Hostility(observer, actor) > 0 && random.Next(3) == 0 ? "question" : "trust";
            Decide(observer, $"蛊惑：{actor.Hero.Name}声明【{declared.Name}】，是否质疑？", new[] { new SkillDecisionOption("trust", "不质疑"), new SkillDecisionOption("question", "质疑：真则失去1体力，假则摸1") }, ai, key =>
            { if (key == "question") challengers.Add(observer); AddLog($"{observer.Hero.Name}{(key == "question" ? "质疑" : "不质疑")}蛊惑。"); Ask(index + 1); });
        }
        void Reveal()
        {
            bool truthful = material.Kind == claim.Kind && (claim.Kind != CardKind.Slash || material.Nature == claim.Nature);
            bool effective = challengers.Count == 0 || truthful && SuitFor(actor, material) == "♥";
            if (challengers.Count > 0) { ShowCard(material); AddLog($"蛊惑揭示{CardLabel(material)}：{(truthful ? "真牌" : "假牌")}，声明{(effective ? "生效" : "作废")}。"); }
            void Finish()
            {
                OrderingCards.Remove(material); DiscardPile.Add(material);
                if (!effective || !actor.IsAlive) reservedUseMaterials.Remove(material.Id);
                AfterLosses(() => done(effective && actor.IsAlive, declared));
            }
            void Consequence(int index)
            {
                if (Phase == GamePhase.GameOver) { OrderingCards.Remove(material); if (!DiscardPile.Contains(material)) DiscardPile.Add(material); reservedUseMaterials.Remove(material.Id); return; }
                if (index >= challengers.Count) { Finish(); return; }
                Player observer = challengers[index];
                if (!observer.IsAlive) { Consequence(index + 1); return; }
                if (truthful) LoseHealth(observer, 1, () => Consequence(index + 1));
                else { Draw(observer, 1); AfterLosses(() => Consequence(index + 1)); }
            }
            Consequence(0);
        }
        AfterLosses(() => Ask(0));
    }
    private void PlayGuhuo(Player actor, Card material, Card claim, List<Player> targets, Action done, bool countSlash = true)
    {
        GuhuoAttempt(actor, material, claim, targets, (effective, declared) =>
        {
            if (!effective) { done(); return; }
            usedSlashNatures[declared.Id] = declared.Nature;
            EmitCue(GameCueKind.CardPlayed, actor, declared.Kind, target: targets.FirstOrDefault(), physicalCard: declared, nature: declared.Nature);
            BeginCardUse(actor, declared.Kind, new[] { material }, targets, finish =>
            {
                if (declared.Kind == CardKind.IronChain)
                {
                    void Chain(int index) { if (index == targets.Count) finish(); else TrickEffect(actor, CardKind.IronChain, targets[index], () => { targets[index].Chained = !targets[index].Chained; Chain(index + 1); }, () => Chain(index + 1)); } Chain(0);
                }
                else if (declared.Kind == CardKind.Slash && !countSlash) { actor.PlayedSlash |= actor == Current && actor.InPlayPhase; DeclaredSlash(actor, targets[0], declared, finish); }
                else ResolveCardEffect(actor, declared, targets.FirstOrDefault(), declared.Kind, finish);
            }, done);
        });
    }
    private void GuhuoResponse(Player player, ResponseKind kind, string prompt, Action<bool> continuation)
    {
        List<Card> normal = player.Hand.Where(card => CanTransform(player, card, kind)).ToList();
        List<Card> claims = GuhuoClaims().Where(claim => kind == ResponseKind.Slash ? claim.Kind == CardKind.Slash : kind == ResponseKind.Dodge ? claim.Kind == CardKind.Dodge : claim.Kind == CardKind.Peach || claim.Kind == CardKind.Wine && dyingPlayers.LastOrDefault() == player).ToList();
        List<SkillDecisionOption> options = normal.Select(card => new SkillDecisionOption("direct:" + card.Id, "直接响应 · " + CardLabel(card))).ToList();
        if (CanGuhuo(player)) options.AddRange(claims.Select(claim => new SkillDecisionOption(ClaimKey(claim), "蛊惑声明 · " + claim.Name)));
        options.Add(new("no", "放弃响应"));
        string ai = normal.Count > 0 ? "direct:" + normal.MinBy(KeepValue)!.Id : CanGuhuo(player) ? ClaimKey(claims[0]) : "no";
        Decide(player, prompt + "（可直接响应或蛊惑）", options, ai, key =>
        {
            if (key == "no") { lastResponseCard = null; CompletePhysicalResponse(player, kind, null, continuation); return; }
            if (key.StartsWith("direct:", StringComparison.Ordinal))
            { Card card = normal.First(card => "direct:" + card.Id == key); PayCard(player, card); ShowCard(card); EmitResponse(player, card, kind); CompletePhysicalResponse(player, kind, card, continuation); return; }
            Card claim = claims.First(claim => ClaimKey(claim) == key);
            ChooseOwnCards(player, "蛊惑响应：秘密选择一张手牌", 1, 1, false, cards => GuhuoAttempt(player, cards[0], claim, new(), (effective, declared) =>
            {
                if (!effective) { work.Enqueue(() => GuhuoResponse(player, kind, prompt, continuation)); return; }
                player.PlayedSlash |= kind == ResponseKind.Slash && player == Current && player.InPlayPhase;
                EmitResponse(player, declared, kind); reservedUseMaterials.Remove(cards[0].Id); CompletePhysicalResponse(player, kind, cards[0], continuation);
            }));
        });
    }
    private void GuhuoCounterspell(Player responder, Player actor, CardKind kind, Player target, bool negated, int seat, int passed, Action<bool> done)
    {
        ChooseOwnCards(responder, "蛊惑无懈：秘密选择一张手牌", 1, 1, false, cards =>
            GuhuoAttempt(responder, cards[0], new(0, CardKind.Nullification, "", 0), new() { target }, (effective, declared) =>
            {
                if (!effective) { work.Enqueue(() => Counterspell(actor, kind, target, negated, seat, passed, done)); return; }
                EmitCue(GameCueKind.CardPlayed, responder, CardKind.Nullification, target: target, physicalCard: declared);
                AfterLosses(() => BeginCardUse(responder, CardKind.Nullification, cards, new[] { target }, finish => { Publish(cardUses.Last(), CardUseStage.Counterspell, target); finish(); },
                    () => Counterspell(actor, kind, target, !negated, (seat + 1) % Players.Count, 0, done)));
            }));
    }
}

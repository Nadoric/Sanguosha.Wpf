using System.Reflection;
using System.Text.Json;
using Sanguosha.Core;

namespace Sanguosha.Verify;
internal static partial class Program
{
    private static Card Public8(SavedGame state, CardKind kind, CardZone zone, int seat = 1)
    {
        Card card = Move(state, kind, seat);
        state.Players[seat].Hand.Remove(card);
        (zone == CardZone.Equipment ? state.Players[seat].Equipment : state.Players[seat].Judgments).Add(card);
        return card;
    }

    private static void VerifyPatch8Choices()
    {
        CardKind[] equipmentKinds = { CardKind.Blade, CardKind.EightTrigrams, CardKind.Horse };
        CardKind[] judgmentKinds = { CardKind.Indulgence, CardKind.SupplyShortage, CardKind.Lightning };
        foreach (CardKind trickKind in new[] { CardKind.Steal, CardKind.Dismantle })
        foreach (CardKind chosenKind in equipmentKinds.Concat(judgmentKinds))
        {
            SavedGame state = Rig3("machao", "guanyu");
            Card trick = Move(state, trickKind, 0);
            Card[] hidden = new[] { Move(state, CardKind.Slash, 1), Move(state, CardKind.Dodge, 1), Move(state, CardKind.Peach, 1) };
            List<Card> equipment = equipmentKinds.Select(kind => Public8(state, kind, CardZone.Equipment)).ToList();
            List<Card> judgments = judgmentKinds.Select(kind => Public8(state, kind, CardZone.Judgment)).ToList();
            GameEngine game = Restore(state);
            Check(game.Play(trick.Id, 1) && game.Phase == GamePhase.SelectCard, $"Both tricks wait for a human zone/card selection: {trickKind}/{chosenKind}; {game.Status}");
            TargetCardRequest request = game.PendingCardChoice!;
            TargetCardOption[] hand = request.Options.Where(option => option.Zone == CardZone.Hand).ToArray();
            Check(hand.Length == 3 && hand.All(option => option.CardId == null) &&
                hand.Select(option => option.HandPosition).SequenceEqual(new int?[] { 0, 1, 2 }), "Three anonymous hand backs expose only positions");
            Check(hand.All(option => !hidden.Any(card => option.Label.Contains(card.Name))), "Hand labels do not reveal card names");
            Check(request.Options.Count(option => option.Zone == CardZone.Equipment) == 3 &&
                request.Options.Count(option => option.Zone == CardZone.Judgment) == 3, "All legal public-zone cards individually selectable");
            Check(!game.ChooseTargetCard(CardZone.Hand, hidden[0].Id, 0) &&
                !game.ChooseTargetCard(CardZone.Hand, null, -1) && !game.ChooseTargetCard(CardZone.Hand, null, 3) &&
                !game.ChooseTargetCard(CardZone.Equipment, equipment[0].Id, 0), "Reject hidden identities, forged slots and cross-zone input");
            Card chosen = equipment.Concat(judgments).Single(card => card.Kind == chosenKind);
            CardZone zone = equipment.Contains(chosen) ? CardZone.Equipment : CardZone.Judgment;
            Check(game.ChooseTargetCard(zone, chosen.Id), "Choose the exact requested public card, not an auto-priority card");
            Check(trickKind == CardKind.Steal ? game.Human.Hand.Contains(chosen) : game.DiscardPile.Contains(chosen), "Exact selected public card reaches the proper destination");
            Check(game.Players[1].Hand.SequenceEqual(hidden) &&
                game.Players[1].Equipment.SequenceEqual(equipment.Where(card => card != chosen)) &&
                game.Players[1].Judgments.SequenceEqual(judgments.Where(card => card != chosen)), "All unchosen cards and zones remain unchanged");
            Check(game.PendingCardChoice == null && game.Phase == GamePhase.Play && !game.ChooseTargetCard(zone, chosen.Id), "Single resolution resumes play and rejects duplicate input");
            CheckAllCards(game);
        }

        foreach (CardKind trickKind in new[] { CardKind.Steal, CardKind.Dismantle })
        for (int position = 0; position < 3; position++)
        {
            SavedGame state = Rig3("machao", "guanyu");
            Card trick = Move(state, trickKind, 0);
            Card[] hidden = new[] { Move(state, CardKind.Slash, 1), Move(state, CardKind.Dodge, 1), Move(state, CardKind.Peach, 1) };
            GameEngine game = Restore(state);
            Check(game.Play(trick.Id, 1), "Open a hidden-card selection");
            TargetCardRequest request = game.PendingCardChoice!;
            // Verification-only reflection confirms exact stable mapping; production UI cannot read this internal property.
            IReadOnlyList<int> mapping = (IReadOnlyList<int>)typeof(TargetCardRequest)
                .GetProperty("HiddenHandIds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(request)!;
            Card expected = hidden.Single(card => card.Id == mapping[position]);
            string visible = JsonSerializer.Serialize(request.Options);
            Check(!visible.Contains("HiddenHandIds") && request.Options.All(option => option.CardId == null), "Public option payload contains no hand identities");
            game.Advance();
            Check(ReferenceEquals(request, game.PendingCardChoice), "Waiting does not reroll hidden slot mapping");
            Check(game.ChooseTargetCard(CardZone.Hand, null, position), "Resolve the clicked face-down position");
            Check(!game.Players[1].Hand.Contains(expected) && (trickKind == CardKind.Steal ? game.Human.Hand.Contains(expected) : game.DiscardPile.Contains(expected)),
                "Clicked slot resolves its original physical card, not a fresh random draw");
            Check(game.Players[1].Hand.SequenceEqual(hidden.Where(card => card != expected)), "Other hidden cards remain untouched");
            CheckAllCards(game);
        }

        // Native / converted judgment identity and marker cleanup.
        SavedGame converted = Rig3("machao", "guanyu");
        Card remove = Move(converted, CardKind.Dismantle, 0);
        Card shortage = Take3(converted, 1, card => card.IsBasic && !card.IsRed);
        converted.Players[1].Hand.Remove(shortage);
        converted.Players[1].Judgments.Add(shortage);
        converted.Players[1] = converted.Players[1] with { SupplyShortages = new() { shortage.Id } };
        GameEngine g = Restore(converted);
        Check(g.Play(remove.Id, 1) && g.PendingCardChoice!.Options.Single().Label.Contains("兵粮寸断"), "Converted delayed trick has its effective judgment identity");
        Check(g.ChooseTargetCard(CardZone.Judgment, shortage.Id) && !g.Players[1].SupplyShortages.Contains(shortage.Id), "Selecting converted shortage removes its annotation exactly once");
        CheckAllCards(g);

        foreach (string hero in new[] { "simayi", "re_simayi" })
        {
            SavedGame state = Rig3(hero, "guanyu");
            Move(state, CardKind.Dodge, 1);
            Card weapon = Public8(state, CardKind.Blade, CardZone.Equipment);
            Public8(state, CardKind.Indulgence, CardZone.Judgment);
            g = Restore(state); bool completed = false;
            Call(g, "Damage", g.Players[1], g.Human, null, (Action)(() => completed = true), null);
            Decision(g, "yes");
            Check(g.PendingCardChoice != null && g.PendingCardChoice.Options.All(option => option.Zone != CardZone.Judgment), "Fankui shares chooser but cannot take judgment cards");
            Check(g.ChooseTargetCard(CardZone.Equipment, weapon.Id) && g.Human.Hand.Contains(weapon) && completed, "Fankui transfers chosen equipment and resumes damage continuation");
            CheckAllCards(g);
        }

        foreach (string hero in new[] { "zhangliao", "re_zhangliao" })
        {
            SavedGame state = Rig3(hero, "guanyu");
            Card first = Move(state, CardKind.Dodge, 1), second = Move(state, CardKind.Peach, 2);
            Public8(state, CardKind.Blade, CardZone.Equipment);
            g = Restore(state); Call(g, "ResolveDrawPhase", g.Human, false);
            Decision(g, "1"); Decision(g, "2");
            Check(g.PendingCardChoice?.TargetSeat == 1 && g.PendingCardChoice.Options.All(option => option.Zone == CardZone.Hand), "Tuxi explicitly chooses hidden hand only, not equipment");
            Check(g.ChooseTargetCard(CardZone.Hand, null, 0) && g.PendingCardChoice?.TargetSeat == 2, "Tuxi waits for first target slot, then requests second");
            Check(g.ChooseTargetCard(CardZone.Hand, null, 0) && g.Human.Hand.Contains(first) && g.Human.Hand.Contains(second) && g.Human.Hand.Count == 2, "Standard/refreshed Tuxi resolve two hidden positions, not extra normal draws");
            CheckAllCards(g);
        }
        Console.WriteLine("Patch8: exact public-card / hidden-slot choices, judgment markers, Fankui, Tuxi and shared continuations verified.");
    }
}

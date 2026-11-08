using Sanguosha.Core;

namespace Sanguosha.Verify
{
    internal static partial class Program
    {
        private static void CheckCardTotal(GameEngine game)
        {
            int total = game.DeckCount + game.DiscardPile.Count + game.Players.Sum(player =>
                player.Hand.Count + player.Equipment.Count + player.Judgments.Count);
            Check(total == game.PhysicalCardCount, "Card resolution must conserve all 108 physical cards");
        }

        private static void VerifyTargetCardEffects()
        {
            foreach (CardKind kind in new[] { CardKind.Steal, CardKind.Dismantle })
            {
                foreach (CardZone zone in Enum.GetValues<CardZone>())
                {
                    SavedGame snapshot = Rig("zhangfei", "guanyu");
                    EmptyHand(snapshot, 0);
                    Card trick = Move(snapshot, kind, 0);
                    Card hand = Move(snapshot, CardKind.Slash, 1);
                    Card equipment = Move(snapshot, CardKind.Blade, 1);
                    Card judgment = Move(snapshot, CardKind.Indulgence, 1);
                    snapshot.Players[1].Hand.Remove(equipment);
                    snapshot.Players[1].Equipment.Add(equipment);
                    snapshot.Players[1].Hand.Remove(judgment);
                    snapshot.Players[1].Judgments.Add(judgment);
                    GameEngine game = Restore(snapshot);
                    Check(game.Play(trick.Id, 1), $"{kind} can target a character with cards");
                    Check(game.Phase == GamePhase.SelectCard && !game.CanSave,
                        "Target card selection suspends play and saving");
                    TargetCardRequest request = game.PendingCardChoice!;
                    Check(request.Options.Count == 3, "All three target zones can be chosen");
                    Check(request.Options.Single(item => item.Zone == CardZone.Hand).CardId == null,
                        "Opponent's hand identities remain hidden");
                    Check(!game.ChooseTargetCard(CardZone.Equipment, -1), "Reject forged card selection");
                    game.Advance();
                    Check(game.PendingCardChoice == request, "AI cannot advance during selection");
                    Card selected = zone switch
                    {
                        CardZone.Equipment => equipment,
                        CardZone.Judgment => judgment,
                        _ => hand
                    };
                    TargetCardOption option = request.Options.Single(item => item.Zone == zone);
                    Check(game.ChooseTargetCard(zone, option.CardId), $"Resolve {kind} from {zone}");
                    Check(game.PendingCardChoice == null && game.Phase == GamePhase.Play,
                        "Selection returns to the actor's play phase");
                    Check(!game.Players[1].Hand.Contains(selected) &&
                        !game.Players[1].Equipment.Contains(selected) &&
                        !game.Players[1].Judgments.Contains(selected), "Selected physical card leaves target");
                    Check(kind == CardKind.Steal
                        ? game.Human.Hand.Contains(selected) && !game.DiscardPile.Contains(selected)
                        : game.DiscardPile.Contains(selected) && !game.Human.Hand.Contains(selected),
                        "Steal gains the selected card; dismantle discards it");
                    Check(game.Human.Hand.Count == (kind == CardKind.Steal ? 1 : 0),
                        "Steal consumes one trick and gains one card; net hand count is unchanged");
                    Check(game.DiscardPile.Contains(trick), "Used trick is discarded");
                    Check(game.Status.Contains(selected.Name), "Result names the obtained or discarded card");
                    Check(!game.ChooseTargetCard(zone, option.CardId), "Cannot resolve a choice twice");
                    CheckCardTotal(game);
                }
            }

            SavedGame state = Rig("zhangfei", "sunshangxiang");
            Card steal = Move(state, CardKind.Steal, 0);
            Card blade = Move(state, CardKind.Blade, 1);
            state.Players[1].Hand.Remove(blade);
            state.Players[1].Equipment.Add(blade);
            GameEngine tested = Restore(state);
            tested.Play(steal.Id, 1);
            tested.ChooseTargetCard(CardZone.Equipment, blade.Id);
            Check(tested.Human.Hand.Contains(blade) && tested.Players[1].Hand.Count == 2,
                "Stealing equipment both transfers it and triggers Xiaoji");
            CheckCardTotal(tested);

            state = Rig("zhangfei", "luxun");
            Card dismantle = Move(state, CardKind.Dismantle, 0);
            Card last = Move(state, CardKind.Slash, 1);
            tested = Restore(state);
            tested.Play(dismantle.Id, 1);
            tested.ChooseTargetCard(CardZone.Hand);
            Check(tested.DiscardPile.Contains(last) && tested.Players[1].Hand.Count == 1,
                "Dismantling Lu Xun's last hand card triggers Lianying without cancelling discard");
            CheckCardTotal(tested);

            state = Rig("zhangfei", "guanyu");
            steal = Move(state, CardKind.Steal, 0);
            tested = Restore(state);
            Check(!tested.Play(steal.Id, 1) && tested.Human.Hand.Contains(steal),
                "Empty targets do not consume the trick");
            Move(state, CardKind.Slash, 2);
            tested = Restore(state);
            Check(!tested.Play(steal.Id, 2), "Steal respects distance one");
            state.Players[0] = state.Players[0] with
            {
                HeroId = "huangyueying",
                MaxHealth = 4,
                Health = 4
            };
            tested = Restore(state);
            Check(tested.Play(steal.Id, 2), "Qicai removes steal's distance limit");
            tested.ChooseTargetCard(CardZone.Hand);
            CheckCardTotal(tested);
        }

        private static void VerifyCardEffects()
        {
            foreach (CardKind kind in new[] { CardKind.Peach, CardKind.DrawTwo, CardKind.Duel })
            {
                SavedGame state = Rig("zhangfei", "guanyu");
                EmptyHand(state, 0);
                Card card = Move(state, kind, 0);
                state.Players[0] = state.Players[0] with { Health = state.Players[0].MaxHealth - 1 };
                GameEngine game = Restore(state);
                int targetHealth = game.Players[1].Health;
                Check(game.Play(card.Id, kind == CardKind.Duel ? 1 : null), $"Play {kind}");
                Drain(game);
                Check(kind switch
                {
                    CardKind.Peach => game.Human.Health == game.Human.MaxHealth,
                    CardKind.DrawTwo => game.Human.Hand.Count == 2,
                    _ => game.Players[1].Health == targetHealth - 1
                }, $"{kind} applies its actual effect");
                CheckCardTotal(game);
            }

            foreach (CardKind kind in new[] { CardKind.Barbarians, CardKind.Arrows, CardKind.Feast })
            {
                SavedGame state = Rig("zhangfei", "guanyu");
                Card card = Move(state, kind, 0);
                for (int seat = 0; seat < 4; seat++)
                {
                    state.Players[seat] = state.Players[seat] with { Health = 2 };
                }

                if (kind != CardKind.Feast)
                {
                    Move(state, kind == CardKind.Barbarians ? CardKind.Slash : CardKind.Dodge, 1);
                }

                GameEngine game = Restore(state);
                Check(game.Play(card.Id), $"Play area card {kind}");
                Drain(game);
                Check(game.Players[1].Health == (kind == CardKind.Feast ? 3 : 2),
                    "Feast heals; appropriate response prevents area damage");
                Check(game.Players.Skip(2).All(player => player.Health ==
                    (kind == CardKind.Feast ? 3 : 1)), "Area card resolves for every other living player");
                Check(game.Human.Health == (kind == CardKind.Feast ? 3 : 2),
                    "Area attacks exclude source; feast includes source");
                CheckCardTotal(game);
            }

            foreach (CardKind kind in new[]
                { CardKind.Crossbow, CardKind.Blade, CardKind.EightTrigrams, CardKind.Horse })
            {
                SavedGame state = Rig("zhangfei", "guanyu");
                Card card = Move(state, kind, 0);
                GameEngine game = Restore(state);
                Check(game.Play(card.Id) && game.Human.Equipment.Contains(card) &&
                    !game.DiscardPile.Contains(card), $"{kind} enters equipment zone");
                CheckCardTotal(game);
            }

            SavedGame snapshot = Rig("liubei", "guanyu");
            Card crossbow = Move(snapshot, CardKind.Crossbow, 0);
            Card first = Move(snapshot, CardKind.Slash, 0);
            Card second = Move(snapshot, CardKind.Slash, 0);
            GameEngine tested = Restore(snapshot);
            tested.Play(crossbow.Id);
            Check(tested.Play(first.Id, 1) && tested.Play(second.Id, 1),
                "Crossbow permits multiple slashes without Zhang Fei");
            snapshot = Rig("liubei", "guanyu");
            Card weapon = Move(snapshot, CardKind.Blade, 0);
            Card slash = Move(snapshot, CardKind.Slash, 0);
            tested = Restore(snapshot);
            tested.Play(weapon.Id);
            Check(tested.Play(slash.Id, 2), "Blade's range permits attacking a distance-two target");

            foreach (bool red in new[] { true, false })
            {
                snapshot = Rig("liubei", "guanyu");
                slash = Move(snapshot, CardKind.Slash, 0);
                Card armor = Move(snapshot, CardKind.EightTrigrams, 1);
                snapshot.Players[1].Hand.Remove(armor);
                snapshot.Players[1].Equipment.Add(armor);
                Card judgment = snapshot.Deck.First(card => card.IsRed == red);
                snapshot.Deck.Remove(judgment);
                snapshot.Deck.Add(judgment);
                tested = Restore(snapshot);
                tested.Play(slash.Id, 1);
                Check(tested.Players[1].Health == (red ? 4 : 3),
                    "Eight Trigrams red judgment dodges, black judgment requires a real dodge");
                Check(tested.DiscardPile.Contains(judgment), "Armor judgment card is discarded");
                CheckCardTotal(tested);
            }

            snapshot = Rig("liubei", "guanyu");
            Card delayed = Move(snapshot, CardKind.Indulgence, 0);
            tested = Restore(snapshot);
            Check(tested.Play(delayed.Id, 1) && tested.Players[1].Judgments.Contains(delayed),
                "Indulgence moves the actual card to target's judgment zone");
            tested.EndTurn();
            if (tested.Phase == GamePhase.Discard)
            {
                tested.Discard(tested.Human.Hand.Take(tested.Human.Hand.Count - tested.Human.Health)
                    .Select(card => card.Id));
            }

            tested.Advance();
            Check(tested.Players[1].Judgments.Count == 0 && tested.DiscardPile.Contains(delayed),
                "Indulgence is judged and consumed at target's next turn start");
            Check(tested.Logs.Any(log => log.Text.Contains("乐不思蜀判定")),
                "Delayed trick records its judgment result");
            CheckCardTotal(tested);

            foreach (bool heart in new[] { true, false })
            {
                snapshot = Rig("liubei", "guanyu");
                EmptyHand(snapshot, 0);
                delayed = Move(snapshot, CardKind.Indulgence, 0);
                Card judgment = snapshot.Deck.First(card => (card.Suit == "♥") == heart);
                snapshot.Deck.Remove(judgment);
                snapshot.Deck.Add(judgment);
                tested = Restore(snapshot);
                tested.Play(delayed.Id, 1);
                tested.EndTurn();
                tested.Advance();
                Check(tested.CurrentSeat == (heart ? 1 : 2),
                    "Indulgence heart preserves play; other suits skip play before advancing");
                CheckCardTotal(tested);
            }

            snapshot = Rig("liubei", "guanyu");
            crossbow = Move(snapshot, CardKind.Crossbow, 0);
            weapon = Move(snapshot, CardKind.Blade, 0);
            Card horse = Move(snapshot, CardKind.Horse, 1);
            snapshot.Players[1].Hand.Remove(horse);
            snapshot.Players[1].Equipment.Add(horse);
            slash = Move(snapshot, CardKind.Slash, 0);
            tested = Restore(snapshot);
            Check(tested.Distance(tested.Human, tested.Players[1]) == 2 &&
                !tested.Play(slash.Id, 1), "Defensive horse increases incoming distance");
            tested.Play(crossbow.Id);
            tested.Play(weapon.Id);
            Check(tested.Human.Equipment.Count == 1 && tested.Human.Equipment.Contains(weapon) &&
                tested.DiscardPile.Contains(crossbow), "Replacing a weapon discards the old slot occupant");
            Check(tested.Play(slash.Id, 1), "Long-range weapon reaches a target with a defensive horse");
            CheckCardTotal(tested);
        }
    }
}

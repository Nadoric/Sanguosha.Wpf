using System.Text.Json;
using Sanguosha.Core;

namespace Sanguosha.Verify
{
    internal static class Program
    {
        private static int assertions;

        private static void Main()
        {
            VerifyInitialState();
            VerifyRosterAndModes();
            VerifyAttackAndDodge();
            VerifyDistanceAndLimits();
            VerifyConversion();
            VerifyRescueAndVictory();
            VerifySkillsAndPersistence();
            VerifySkillInteractions();
            VerifyCueEvents();
            SimulateMatches();
            Console.WriteLine($"PASS · {assertions:N0} assertions; 400 complete seeded matches across 25 heroes / 4 modes.");
        }

        private static GameEngine Fresh(string hero = "liubei")
        {
            GameEngine game = new(42);
            game.Start(hero);
            game.Advance();
            return game;
        }

        private static SavedGame Snapshot(GameEngine game)
        {
            return JsonSerializer.Deserialize<SavedGame>(game.Save())!;
        }

        private static GameEngine Restore(SavedGame snapshot)
        {
            return GameEngine.Load(JsonSerializer.Serialize(snapshot), 42);
        }

        private static Card Move(SavedGame snapshot, CardKind kind, int seat)
        {
            List<List<Card>> zones = new() { snapshot.Deck, snapshot.Discard };
            zones.AddRange(snapshot.Players.SelectMany(player => new[] { player.Hand, player.Equipment, player.Judgments }));
            foreach (List<Card> zone in zones)
            {
                Card? card = zone.FirstOrDefault(item => item.Kind == kind);
                if (card != null)
                {
                    zone.Remove(card);
                    snapshot.Players[seat].Hand.Add(card);
                    return card;
                }
            }

            throw new Exception($"Cannot find {kind}");
        }

        private static void EmptyHand(SavedGame snapshot, int seat)
        {
            snapshot.Deck.AddRange(snapshot.Players[seat].Hand);
            snapshot.Players[seat].Hand.Clear();
            snapshot.Deck.AddRange(snapshot.Players[seat].Equipment);
            snapshot.Players[seat].Equipment.Clear();
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        private static void VerifyInitialState()
        {
            GameEngine game = Fresh();
            Check(game.Players.Count == 4, "Four seats");
            Check(game.Human.MaxHealth == 5, "Lord health bonus");
            Check(game.Human.Hand.Count == 6, "Initial four cards plus two drawn");
            Check(game.Players.Select(player => player.Role).Distinct().Count() == 4, "Unique roles");
            Check(game.CanSave, "Save is available during own action");
        }

        private static void VerifyRosterAndModes()
        {
            Check(Heroes.All.Count == 25, "Exactly 25 heroes");
            Check(Heroes.All.Count(hero => hero.Kingdom == "魏") == 7, "Wei roster 7");
            Check(Heroes.All.Count(hero => hero.Kingdom == "蜀") == 7, "Shu roster 7");
            Check(Heroes.All.Count(hero => hero.Kingdom == "吴") == 8, "Wu roster 8");
            Check(Heroes.All.Count(hero => hero.Kingdom == "群") == 3, "Qun roster 3");
            foreach (GameMode mode in Enum.GetValues<GameMode>())
            {
                GameEngine game = new(15);
                game.Start("caocao", Role.Lord, mode);
                game.Advance();
                int expected = mode == GameMode.Identity8 ? 8 : mode == GameMode.Identity5 ? 5 : 4;
                Check(game.Players.Count == expected, $"Seat count for {mode}");
                Check(game.Players.Select(player => player.Hero.Id).Distinct().Count() == expected, "No duplicate hero");
                Check(game.Human.MaxHealth == (mode == GameMode.Team2V2 ? 4 : 5), "Mode-specific health");
                Check(GameEngine.Load(game.Save()).Mode == mode, "Save preserves mode");
                if (mode == GameMode.Team2V2)
                {
                    Check(game.Players[0].Team == game.Players[2].Team &&
                        game.Players[1].Team == game.Players[3].Team, "Interleaved teams");
                }
            }
        }

        private static void VerifyAttackAndDodge()
        {
            SavedGame snapshot = Snapshot(Fresh());
            EmptyHand(snapshot, 1);
            Card slash = Move(snapshot, CardKind.Slash, 0);
            Card dodge = Move(snapshot, CardKind.Dodge, 1);
            GameEngine game = Restore(snapshot);
            int health = game.Players[1].Health;
            Check(game.Play(slash.Id, 1), "Slash accepted");
            Check(game.Players[1].Health == health, "AI dodge prevents damage");
            Check(game.DiscardPile.Contains(dodge), "Dodge is consumed");
            Check(game.Human.SlashCount == 1, "Slash quota consumed");

            snapshot = Snapshot(Fresh());
            EmptyHand(snapshot, 1);
            slash = Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            health = game.Players[1].Health;
            game.Play(slash.Id, 1);
            Check(game.Players[1].Health == health - 1, "Unanswered slash deals one damage");
        }

        private static void VerifyDistanceAndLimits()
        {
            SavedGame snapshot = Snapshot(Fresh());
            Card slash = Move(snapshot, CardKind.Slash, 0);
            Card blade = Move(snapshot, CardKind.Blade, 0);
            Move(snapshot, CardKind.Slash, 0);
            GameEngine game = Restore(snapshot);
            int count = game.Human.Hand.Count;
            Check(!game.Play(slash.Id, 2), "Distance two cannot be attacked with range one");
            Check(game.Human.Hand.Count == count, "Illegal action does not consume a card");
            Check(game.Play(blade.Id), "Equip blade");
            Check(game.Human.AttackRange == 3, "Blade increases range");
            Check(game.Play(slash.Id, 2), "Distant target can be attacked with blade");
            Card another = game.Human.Hand.First(card => card.Kind == CardKind.Slash);
            Check(!game.Play(another.Id, 1), "Second slash prohibited without skill or crossbow");

            snapshot = Snapshot(Fresh("zhangfei"));
            Move(snapshot, CardKind.Slash, 0);
            Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            foreach (Card attack in game.Human.Hand.Where(card => card.Kind == CardKind.Slash).Take(2).ToList())
            {
                Check(game.Play(attack.Id, 1), "Zhang Fei may use multiple slashes");
            }
        }

        private static void VerifyConversion()
        {
            GameEngine game = Fresh("guanyu");
            Card red = new(999, CardKind.Dodge, "♥", 2);
            Check(game.CanTransform(game.Human, red, ResponseKind.Slash), "Guan Yu converts red hand card");
            Check(!game.CanTransform(game.Human, red with { Suit = "♠" }, ResponseKind.Slash), "Black dodge cannot be converted by Guan Yu");
            game = Fresh("zhaoyun");
            Check(game.CanTransform(game.Human, new Card(998, CardKind.Slash, "♠", 7), ResponseKind.Dodge), "Zhao Yun slash as dodge");
            Check(game.CanTransform(game.Human, red, ResponseKind.Slash), "Zhao Yun dodge as slash");
        }

        private static void VerifyRescueAndVictory()
        {
            SavedGame snapshot = Snapshot(Fresh("zhangfei"));
            int rebel = snapshot.Players.FindIndex(player => player.Role == Role.Rebel);
            int loyal = snapshot.Players.FindIndex(player => player.Role == Role.Loyalist);
            int renegade = snapshot.Players.FindIndex(player => player.Role == Role.Renegade);
            EmptyHand(snapshot, rebel);
            EmptyHand(snapshot, loyal);
            EmptyHand(snapshot, renegade);
            snapshot.Players[rebel] = snapshot.Players[rebel] with { Health = 1 };
            Card blade = Move(snapshot, CardKind.Blade, 0);
            Card slash = Move(snapshot, CardKind.Slash, 0);
            Card peach = Move(snapshot, CardKind.Peach, 0);
            GameEngine game = Restore(snapshot);
            game.Play(blade.Id);
            game.Play(slash.Id, rebel);
            Check(game.Pending?.Kind == ResponseKind.Peach, "Human receives dying rescue prompt");
            Check(!game.CanSave, "Cannot save with pending response");
            Check(game.Respond(peach.Id), "Human can save another player with peach");
            Check(game.Players[rebel].IsAlive && game.Players[rebel].Health == 1, "Peach rescues victim");
            Check(game.Phase == GamePhase.Play, "Return to action after rescue");

            snapshot = Snapshot(game);
            EmptyHand(snapshot, rebel);
            snapshot.Players[renegade] = snapshot.Players[renegade] with { IsAlive = false, Health = 0 };
            slash = Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            game.Play(slash.Id, rebel);
            game.Respond(null);
            Check(game.Phase == GamePhase.GameOver, "Victory checked after final enemy dies");
            Check(game.HumanWon && game.Result == "主公与忠臣获胜", "Lord side victory");
        }

        private static void VerifySkillsAndPersistence()
        {
            SavedGame snapshot = Snapshot(Fresh());
            snapshot.Players[0] = snapshot.Players[0] with { Health = 3 };
            GameEngine game = Restore(snapshot);
            int hand = game.Human.Hand.Count;
            Check(game.UseSkill(game.Human.Hand[0].Id, 1), "Rende gives card");
            Check(game.Human.Health == 4 && game.Human.Hand.Count == hand - 1, "Rende heals and transfers");
            Check(!game.UseSkill(game.Human.Hand[0].Id, 1), "Active skill once per turn");
            string saved = game.Save();
            GameEngine loaded = GameEngine.Load(saved);
            Check(loaded.Human.Hand.SequenceEqual(game.Human.Hand), "Save preserves hand order");
            Check(loaded.Human.SkillUsed && loaded.Human.Health == 4, "Save preserves skill and health");
            Check(loaded.DeckCount == game.DeckCount, "Save preserves deck");
            bool rejected = false;
            try
            {
                snapshot = Snapshot(game);
                snapshot.Deck.Add(snapshot.Deck[0]);
                Restore(snapshot);
            }
            catch (JsonException)
            {
                rejected = true;
            }

            Check(rejected, "Corrupt duplicated-card save rejected");
        }

        private static bool SameSide(Player first, Player second)
        {
            return first == second ||
                (first.Role is Role.Lord or Role.Loyalist && second.Role is Role.Lord or Role.Loyalist);
        }

        private static void VerifyCueEvents()
        {
            GameEngine game = new(42);
            List<GameCue> cues = new();
            game.CueRaised += cues.Add;
            game.Start("guanyu");
            game.Advance();
            Check(cues.Count(cue => cue.Kind == GameCueKind.MatchStarted) == 1, "One start cue per match");
            Check(cues.Count(cue => cue.Kind == GameCueKind.TurnStarted) == 1, "One cue per started turn");

            SavedGame snapshot = Rig("guanyu", "liubei");
            Card redDodge = snapshot.Deck.First(card => card.Kind == CardKind.Dodge && card.IsRed);
            snapshot.Deck.Remove(redDodge);
            snapshot.Players[0].Hand.Add(redDodge);
            game = Restore(snapshot);
            cues.Clear();
            game.CueRaised += cues.Add;
            Check(!game.Play(redDodge.Id, 2, true), "Illegal converted slash rejected");
            Check(cues.Count == 0, "Illegal action must not emit playback cues");
            Check(game.Play(redDodge.Id, 1, true), "Converted slash accepted");
            Check(cues.Any(cue => cue.Kind == GameCueKind.SkillActivated &&
                cue.HeroId == "guanyu" && cue.Skill == "武圣"), "Correct hero skill voice cue");
            Check(cues.Any(cue => cue.Kind == GameCueKind.CardPlayed && cue.Card == CardKind.Slash),
                "Conversion uses effective card sound rather than physical dodge sound");
            Check(cues.Any(cue => cue.Kind == GameCueKind.Damaged && cue.HeroId == "liubei"),
                "Damage cue belongs to damage victim");

            GameEngine loaded = GameEngine.Load(game.Save(), 42);
            List<GameCue> restored = new();
            loaded.CueRaised += restored.Add;
            loaded.Advance();
            Check(restored.Count == 0, "Loading must not replay historical voice cues");
        }

        private static SavedGame Rig(string actor, string victim)
        {
            SavedGame snapshot = Snapshot(Fresh(actor));
            List<string> ids = new() { actor, victim };
            ids.AddRange(Heroes.All.Select(hero => hero.Id).Where(id => !ids.Contains(id)).Take(2));
            for (int seat = 0; seat < 4; seat++)
            {
                int maximum = Heroes.Find(ids[seat]).Health + (seat == 0 ? 1 : 0);
                snapshot.Players[seat] = snapshot.Players[seat] with
                {
                    HeroId = ids[seat],
                    Health = maximum,
                    MaxHealth = maximum
                };
                if (seat != 0)
                {
                    EmptyHand(snapshot, seat);
                }
            }

            return snapshot;
        }

        private static void Drain(GameEngine game)
        {
            for (int step = 0; step < 50 && game.Phase == GamePhase.Resolving; step++)
            {
                game.Advance();
            }
        }

        private static void VerifySkillInteractions()
        {
            SavedGame snapshot = Rig("zhangfei", "caocao");
            Card slash = Move(snapshot, CardKind.Slash, 0);
            GameEngine game = Restore(snapshot);
            game.Play(slash.Id, 1);
            Check(game.Players[1].Hand.Contains(slash) && !game.DiscardPile.Contains(slash), "Jianxiong moves damage card without duplication");

            snapshot = Rig("xuchu", "guojia");
            slash = Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            game.Play(slash.Id, 1);
            Check(game.Players[1].Health == 1, "Luoyi increases slash damage to two");
            Check(game.Players[1].Hand.Count == 4, "Yiji draws twice per point of damage");

            snapshot = Rig("machao", "guanyu");
            Card horse = Move(snapshot, CardKind.Horse, 1);
            snapshot.Players[1].Hand.Remove(horse);
            snapshot.Players[1].Equipment.Add(horse);
            game = Restore(snapshot);
            Check(game.Distance(game.Human, game.Players[1]) == 1, "Mashu offsets defensive horse before distance lower bound");

            snapshot = Rig("lvbu", "guanyu");
            slash = Move(snapshot, CardKind.Slash, 0);
            Move(snapshot, CardKind.Dodge, 1);
            game = Restore(snapshot);
            game.Play(slash.Id, 1);
            Drain(game);
            Check(game.Players[1].Health == 3, "One dodge is insufficient against Wushuang");

            snapshot = Rig("lvbu", "guanyu");
            slash = Move(snapshot, CardKind.Slash, 0);
            Move(snapshot, CardKind.Dodge, 1);
            Move(snapshot, CardKind.Dodge, 1);
            game = Restore(snapshot);
            game.Play(slash.Id, 1);
            Drain(game);
            Check(game.Players[1].Health == 4, "Two dodges prevent Wushuang damage");

            snapshot = Rig("luxun", "guanyu");
            EmptyHand(snapshot, 0);
            slash = Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            game.Play(slash.Id, 1);
            Check(game.Human.Hand.Count == 1, "Lianying replenishes last used hand card");

            snapshot = Rig("zhangfei", "zhugeliang");
            slash = Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            Check(!game.Play(slash.Id, 1), "Kongcheng rejects slash when empty-handed");

            snapshot = Rig("daqiao", "guanyu");
            Card diamond = snapshot.Deck.First(card => card.Suit == "♦" && card.Kind != CardKind.Indulgence);
            snapshot.Deck.Remove(diamond);
            snapshot.Players[0].Hand.Add(diamond);
            game = Restore(snapshot);
            Check(game.Play(diamond.Id, 1, true), "Guose converts diamond into delayed trick");
            Check(game.Players[1].Judgments.Contains(diamond), "Converted delayed trick retains physical card identity");
            GameEngine loaded = GameEngine.Load(game.Save());
            Check(loaded.Players[1].Judgments.Contains(diamond), "Save roundtrip accepts converted judgment-zone card");

            game = Fresh("lvmeng");
            int hand = game.Human.Hand.Count;
            game.EndTurn();
            Check(game.Phase != GamePhase.Discard && game.Human.Hand.Count == hand, "Keji preserves over-limit hand without slash");

            game = Fresh("huanggai");
            hand = game.Human.Hand.Count;
            int health = game.Human.Health;
            Check(game.UseSkill(null, null), "Kurou needs no hand card");
            Check(game.Human.Health == health - 1 && game.Human.Hand.Count == hand + 2, "Kurou trades health for two cards");
            Check(game.UseSkill(null, null), "Kurou can repeat in one turn");

            game = new GameEngine(42);
            game.Start("zhangfei", Role.Lord, GameMode.Team2V2);
            game.Advance();
            snapshot = Snapshot(game);
            for (int seat = 1; seat < 4; seat++)
            {
                EmptyHand(snapshot, seat);
                string id = seat == 1 ? "guanyu" : seat == 2 ? "liubei" : "caocao";
                snapshot.Players[seat] = snapshot.Players[seat] with
                {
                    HeroId = id,
                    Health = seat % 2 == 1 ? 1 : 4,
                    MaxHealth = 4
                };
            }

            slash = Move(snapshot, CardKind.Slash, 0);
            Card secondSlash = Move(snapshot, CardKind.Slash, 0);
            game = Restore(snapshot);
            game.Play(slash.Id, 1);
            game.Respond(null);
            Check(game.Phase == GamePhase.Play, "2v2 continues after first enemy death");
            game.Play(secondSlash.Id, 3);
            game.Respond(null);
            Check(game.Phase == GamePhase.GameOver && game.HumanWon && game.Result == "青龙队获胜", "2v2 victory requires eliminating entire enemy team");
        }

        private static bool TryHumanSkill(GameEngine game)
        {
            Player human = game.Human;
            if (human.Hero.Id == "huanggai" && human.Health >= 3)
            {
                return game.UseSkill(null, null);
            }

            if (human.SkillUsed || human.Hand.Count == 0)
            {
                return false;
            }

            bool Friend(Player player)
            {
                return game.Mode == GameMode.Team2V2 ? human.Team == player.Team : SameSide(human, player);
            }

            Player? friend = game.Players.FirstOrDefault(player => player.IsAlive && Friend(player) &&
                player.Health < player.MaxHealth && (human.Hero.Id == "huatuo" || player != human));
            int card = human.Hand[^1].Id;
            if (human.Hero.Id == "sunquan")
            {
                return game.UseSkill(card, null);
            }

            if (human.Hero.Id is "liubei" or "sunshangxiang" or "huatuo" && friend != null)
            {
                return game.UseSkill(card, friend.Seat);
            }

            if (human.Hero.Id == "diaochan")
            {
                List<Player> targets = game.Players.Where(player => player.IsAlive && !Friend(player)).Take(2).ToList();
                if (targets.Count == 2)
                {
                    return game.UseSkill(card, targets[0].Seat, targets[1].Seat);
                }
            }

            return false;
        }

        private static void SimulateMatches()
        {
            int longest = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                GameEngine game = new(seed);
                game.Start(Heroes.All[seed % 25].Id, (Role)(seed / 100), (GameMode)(seed / 25 % 4));
                int steps = 0;
                bool restored = false;
                while (game.Phase != GamePhase.GameOver && steps++ < 20000)
                {
                    Check(game.DeckCount + game.DiscardPile.Count +
                        game.Players.Sum(player => player.Hand.Count + player.Equipment.Count + player.Judgments.Count) == 108,
                        $"Card conservation seed {seed}, step {steps}");
                    if (game.Pending != null)
                    {
                        ResponseRequest request = game.Pending;
                        bool rescueFriend = request.Kind != ResponseKind.Peach ||
                            game.Players.Any(player => player.IsAlive && player.Health <= 0 &&
                                (game.Mode == GameMode.Team2V2 ? game.Human.Team == player.Team : SameSide(game.Human, player)));
                        Card? response = rescueFriend
                            ? game.Human.Hand.FirstOrDefault(card => game.CanTransform(game.Human, card, request.Kind))
                            : null;
                        game.Respond(response?.Id);
                    }
                    else if (game.Phase == GamePhase.Play)
                    {
                        if (!restored)
                        {
                            game = GameEngine.Load(game.Save(), seed);
                            restored = true;
                        }

                        if (TryHumanSkill(game))
                        {
                            continue;
                        }

                        bool played = false;
                        foreach (Card card in game.Human.Hand.ToList())
                        {
                            bool transform = card.Kind != CardKind.Slash &&
                                game.CanUseConversion(game.Human, card) && card.Kind != CardKind.Peach;
                            CardKind kind = game.EffectiveKind(game.Human, card, transform);
                            Player? target = game.Players.FirstOrDefault(player => player.IsAlive &&
                                (game.Mode == GameMode.Team2V2 ? game.Human.Team != player.Team : !SameSide(game.Human, player)) &&
                                game.ValidatePlay(game.Human, card, player, transform) == null);
                            if (game.NeedsTarget(kind) && target == null)
                            {
                                continue;
                            }

                            if (game.ValidatePlay(game.Human, card, target, transform) == null)
                            {
                                played = game.Play(card.Id, target?.Seat, transform);
                                if (played)
                                {
                                    break;
                                }
                            }
                        }

                        if (!played)
                        {
                            game.EndTurn();
                        }
                    }
                    else if (game.Phase == GamePhase.Discard)
                    {
                        int count = game.Human.Hand.Count - game.Human.Health;
                        Check(game.Discard(game.Human.Hand.Take(count).Select(card => card.Id).ToList()), "Discard accepted");
                    }
                    else
                    {
                        game.Advance();
                    }
                }

                Check(game.Phase == GamePhase.GameOver, $"Simulation {seed} terminated within limit");
                Check(game.Pending == null, "No pending prompt after game over");
                longest = Math.Max(longest, steps);
            }

            Console.WriteLine($"Simulation: all 400 matches completed; longest {longest} actions.");
        }
    }
}

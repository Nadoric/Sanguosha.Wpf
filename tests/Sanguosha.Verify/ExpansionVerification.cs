using System.Text.Json;
using Sanguosha.Core;

namespace Sanguosha.Verify
{
    internal static partial class Program
    {
        private static SavedGame ExpansionRig(string actor, string victim, string third = "liubei", string fourth = "sunquan")
        {
            SavedGame snapshot = Snapshot(Fresh());
            string[] ids = { actor, victim, third, fourth };
            for (int seat = 0; seat < 4; seat++)
            {
                EmptyHand(snapshot, seat);
                Hero hero = Heroes.Find(ids[seat]);
                int maximum = hero.Health + (seat == 0 ? 1 : 0);
                snapshot.Players[seat] = snapshot.Players[seat] with
                {
                    HeroId = hero.Id, Health = maximum, MaxHealth = maximum,
                    SkillUsed = false, SlashCount = 0, DuelJudgmentRed = null
                };
            }
            return snapshot;
        }

        private static void VerifyExpansion()
        {
            VerifyLiegongAndMashu();
            VerifyKuanggu();
            VerifyQiangxi();
            VerifyYaowu();
            VerifyShuangxiongAndZaiqi();
            VerifyBarbarianGenerals();
            VerifyLieren();
        }

        private static void VerifyLiegongAndMashu()
        {
            foreach (bool enable in new[] { false, true })
            {
                SavedGame snapshot = ExpansionRig("huangzhong", "caocao");
                Card slash = Move(snapshot, CardKind.Slash, 0);
                Card dodge = Move(snapshot, CardKind.Dodge, 1);
                GameEngine game = Restore(snapshot);
                int health = game.Players[1].Health;
                Check(game.Play(slash.Id, 1), "Huangzhong slash accepted");
                Check(game.PendingDecision != null && !game.CanSave, "Liegong explicitly optional and blocks save");
                Check(!game.ChooseSkillDecision("invalid"), "Reject forged skill decision");
                Check(game.ChooseSkillDecision(enable ? "yes" : "no"), "Liegong decision accepted");
                Check(game.Players[1].Health == health - (enable ? 1 : 0), "Classic Liegong bypasses dodge only when chosen");
                Check(game.Players[1].Hand.Contains(dodge) == enable, "Bypass does not consume target dodge");
            }

            SavedGame state = ExpansionRig("pangde", "caocao");
            Card attack = Move(state, CardKind.Slash, 0);
            Move(state, CardKind.Dodge, 1);
            Card weapon = Move(state, CardKind.Blade, 1);
            state.Players[1].Hand.Remove(weapon);
            state.Players[1].Equipment.Add(weapon);
            GameEngine tested = Restore(state);
            Check(tested.Distance(tested.Human, tested.Players[2]) == 1, "Pangde Mashu reduces circular distance");
            tested.Play(attack.Id, 1);
            Check(tested.PendingDecision != null, "Mengjin optional after successful dodge");
            tested.ChooseSkillDecision("yes");
            Check(tested.PendingCardChoice?.Options.All(option => option.Zone != CardZone.Judgment) == true,
                "Mengjin only hand/equipment, not judgment");
            Check(tested.ChooseTargetCard(CardZone.Equipment, weapon.Id), "Mengjin allows chosen public equipment");
            Check(tested.DiscardPile.Contains(weapon), "Mengjin discards target weapon");
        }

        private static void VerifyKuanggu()
        {
            foreach (bool near in new[] { true, false })
            {
                SavedGame snapshot = ExpansionRig("weiyan", "caocao");
                snapshot.Players[0] = snapshot.Players[0] with { Health = 2 };
                if (!near)
                {
                    Card blade = Move(snapshot, CardKind.Blade, 0);
                    snapshot.Players[0].Hand.Remove(blade);
                    snapshot.Players[0].Equipment.Add(blade);
                }
                Card slash = Move(snapshot, CardKind.Slash, 0);
                GameEngine game = Restore(snapshot);
                game.Play(slash.Id, near ? 1 : 2);
                Check(game.Human.Health == (near ? 3 : 2), "Kuanggu uses distance at damage time, not attack range");
            }
        }

        private static void VerifyQiangxi()
        {
            SavedGame snapshot = ExpansionRig("dianwei", "caocao");
            Card blade = Move(snapshot, CardKind.Blade, 0);
            snapshot.Players[0].Hand.Remove(blade);
            snapshot.Players[0].Equipment.Add(blade);
            GameEngine game = Restore(snapshot);
            int health = game.Players[1].Health;
            Check(game.UseSkill(null, 1), "Qiangxi opens cost choice without selected card");
            Check(game.PendingDecision!.Options.Any(option => option.Key == blade.Id.ToString()), "Equipped weapon offered as cost");
            game.ChooseSkillDecision(blade.Id.ToString());
            Check(game.Players[1].Health == health - 1 && game.Human.Health == 5, "Weapon Qiangxi causes one damage without health cost");
            Check(game.DiscardPile.Contains(blade) && !game.Players[1].Hand.Contains(blade), "Qiangxi is no-card damage: Jianxiong cannot gain weapon cost");
            Check(!game.UseSkill(null, 1), "Qiangxi once per play phase");

            game = Restore(snapshot);
            Check(game.UseSkill(null, 2), "Qiangxi can use equipped weapon range with HP cost");
            Check(game.PendingDecision!.Options.All(option => option.Key != blade.Id.ToString()), "Sacrificed weapon cannot grant its own range");
            game.ChooseSkillDecision("cancel");
            Check(!game.Human.SkillUsed && game.Human.Equipment.Contains(blade), "Cancel costs no quota or card");
            game.UseSkill(null, 2);
            game.ChooseSkillDecision("hp");
            Check(game.Human.Health == 4 && game.Players[2].Health == 3, "HP Qiangxi trades one HP for no-card damage");
        }

        private static void VerifyYaowu()
        {
            SavedGame snapshot = ExpansionRig("guanyu", "huaxiong");
            snapshot.Players[0] = snapshot.Players[0] with { Health = 3 };
            Card red = snapshot.Deck.First(card => card.IsRed && card.Kind != CardKind.Slash);
            snapshot.Deck.Remove(red);
            snapshot.Players[0].Hand.Add(red);
            GameEngine game = Restore(snapshot);
            Check(game.Play(red.Id, 1, true), "Red converted slash accepted");
            Check(game.PendingDecision != null, "Yaowu triggers for red converted Slash");
            Check(game.Players[1].Health == 6, "Yaowu choice occurs before damage");
            game.ChooseSkillDecision("heal");
            Check(game.Human.Health == 4 && game.Players[1].Health == 5, "Yaowu source heals before Huaxiong loses HP");

            game = Restore(snapshot);
            int hand = game.Human.Hand.Count;
            game.Play(red.Id, 1, true);
            game.ChooseSkillDecision("draw");
            Check(game.Human.Hand.Count == hand, "Yaowu wounded source may choose draw instead of recover");
        }

        private static void VerifyShuangxiongAndZaiqi()
        {
            GameEngine game = new(51);
            game.Start("yanliangwenchou");
            game.Advance();
            Check(game.PendingDecision != null, "Shuangxiong draw replacement is optional");
            game.ChooseSkillDecision("yes");
            Check(game.Human.Hand.Count == 5 && game.Human.DuelJudgmentRed.HasValue, "Shuangxiong gains exactly one judgment card instead of normal draw");
            foreach (Card card in game.Human.Hand)
                Check(game.CanUseConversion(game.Human, card) == (card.IsRed != game.Human.DuelJudgmentRed), "Only opposite color converts to Duel");
            GameEngine loaded = GameEngine.Load(game.Save(), 51);
            Check(loaded.Human.DuelJudgmentRed == game.Human.DuelJudgmentRed, "Save/load preserves Shuangxiong color");
            loaded.Human.Health = loaded.Human.MaxHealth;
            loaded.EndTurn();
            Check(loaded.Human.DuelJudgmentRed == null, "Shuangxiong color expires at end of turn");

            game = new(52);
            game.Start("yanliangwenchou");
            game.Advance();
            game.ChooseSkillDecision("no");
            Check(game.Human.Hand.Count == 6 && game.Human.DuelJudgmentRed == null, "Declining Shuangxiong preserves normal draw");

            game = new(53);
            game.Start("menghuo");
            game.Human.Health = 2;
            game.Advance();
            Check(game.PendingDecision != null, "Wounded Menghuo may choose Zaiqi");
            game.ChooseSkillDecision("yes");
            int hearts = game.TableCards.Count(card => card.Suit == "♥");
            Check(game.TableCards.Count == 3, "Zaiqi reveals lost-health count");
            Check(game.Human.Health == 2 + hearts && game.Human.Hand.Count == 7 - hearts, "Zaiqi hearts heal/discard, others enter hand");
            Check(game.DeckCount + game.DiscardPile.Count + game.Players.Sum(p => p.Hand.Count + p.Equipment.Count + p.Judgments.Count) == game.PhysicalCardCount,
                "Zaiqi preserves all physical cards");
        }

        private static void VerifyBarbarianGenerals()
        {
            SavedGame snapshot = ExpansionRig("caocao", "menghuo", "zhurong", "liubei");
            Card nanman = Move(snapshot, CardKind.Barbarians, 0);
            GameEngine game = Restore(snapshot);
            int menghuoHp = game.Players[1].Health, zhurongHp = game.Players[2].Health;
            List<GameCue> cues = new();
            game.CueRaised += cues.Add;
            game.Play(nanman.Id);
            Drain(game);
            Check(game.Players[1].Health == menghuoHp && game.Players[2].Health == zhurongHp, "Huoshou and Juxiang ignore Barbarians");
            Check(cues.Any(cue => cue.Kind == GameCueKind.Damaged && cue.HeroId == "liubei" && cue.SourceSeat == 1), "Huoshou replaces the damage source, not the card user");
            Check(game.Players[2].Hand.Contains(nanman), "Juxiang gains other user's physical Barbarians after full resolution");
            Check(!game.DiscardPile.Contains(nanman), "Juxiang does not duplicate the physical card");
        }

        private static void VerifyLieren()
        {
            foreach (bool win in new[] { true, false })
            {
                SavedGame snapshot = ExpansionRig("zhurong", "zhouyu");
                Card slash = Move(snapshot, CardKind.Slash, 0);
                // Move two non-Dodge cards of known ranks from the remaining deck.
                Card own = snapshot.Deck.First(card => card.Rank == (win ? 13 : 1) && card.Kind != CardKind.Dodge);
                snapshot.Deck.Remove(own); snapshot.Players[0].Hand.Add(own);
                Card opponent = snapshot.Deck.First(card => card.Rank == (win ? 1 : 13) && card.Kind != CardKind.Dodge);
                snapshot.Deck.Remove(opponent); snapshot.Players[1].Hand.Add(opponent);
                Card equipment = Move(snapshot, CardKind.Blade, 1);
                snapshot.Players[1].Hand.Remove(equipment); snapshot.Players[1].Equipment.Add(equipment);
                GameEngine game = Restore(snapshot);
                game.Play(slash.Id, 1);
                Check(game.PendingDecision != null, "Lieren optional after Slash damage");
                game.ChooseSkillDecision("yes");
                Check(game.PendingDecision!.Options.All(option => option.Key == own.Id.ToString()), "Pindian only exposes own choices");
                game.ChooseSkillDecision(own.Id.ToString());
                Check(game.DiscardPile.Contains(own) && game.DiscardPile.Contains(opponent), "Both Pindian cards discarded after simultaneous reveal");
                if (win)
                {
                    Check(game.PendingCardChoice != null, "Winning Lieren opens hand/equipment gain choice");
                    game.ChooseTargetCard(CardZone.Equipment, equipment.Id);
                    Check(game.Human.Hand.Contains(equipment), "Winning Lieren gains selected equipment");
                }
                else Check(game.PendingCardChoice == null && game.Players[1].Equipment.Contains(equipment), "Losing Lieren gains nothing");
            }
        }
    }
}

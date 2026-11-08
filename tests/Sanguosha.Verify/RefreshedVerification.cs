using Sanguosha.Core;

namespace Sanguosha.Verify
{
    internal static partial class Program
    {
        private static void VerifyRefreshedAndMountain()
        {
            VerifyRefreshedKurou();
            VerifyRefreshedJianxiongAndHujia();
            VerifyRefreshedZhouyu();
            VerifyRefreshedDiaochan();
            VerifyQiaobianStages();
        }

        private static void VerifyRefreshedKurou()
        {
            SavedGame snapshot = ExpansionRig("re_huanggai", "zhouyu");
            Card cost = Move(snapshot, CardKind.Dodge, 0);
            Card slash = snapshot.Deck.First(card => card.Kind == CardKind.Slash && card.IsRed);
            snapshot.Deck.Remove(slash); snapshot.Players[0].Hand.Add(slash);
            Move(snapshot, CardKind.Dodge, 1);
            GameEngine game = Restore(snapshot);
            int hand = game.Human.Hand.Count;
            Check(game.UseSkill(null, null), "Refreshed Kurou offers hand/equipment cost");
            game.ChooseSkillDecision(cost.Id.ToString());
            Check(game.Human.Health == 4 && game.Human.Hand.Count == hand + 2, "Refreshed Kurou discards one, loses HP, Zhaxiang draws three");
            Check(game.Human.ZhaXiangCount == 1 && game.Human.SkillUsed, "Zhaxiang bonus and once-per-phase Kurou state");
            Check(!game.UseSkill(null, null), "Refreshed Kurou cannot repeat");
            Check(game.ValidatePlay(game.Human, slash, game.Players[2]) == null, "Zhaxiang red slash ignores distance");
            GameEngine loaded = GameEngine.Load(game.Save());
            Check(loaded.Human.ZhaXiangCount == 1, "Save/load preserves active Zhaxiang quota");
            int targetHp = loaded.Players[1].Health;
            loaded.Play(slash.Id, 1);
            Check(loaded.Players[1].Health == targetHp - 1 && loaded.Players[1].Hand.Any(card => card.Kind == CardKind.Dodge), "Zhaxiang red slash bypasses dodge");
        }

        private static void VerifyRefreshedJianxiongAndHujia()
        {
            SavedGame snapshot = ExpansionRig("liubei", "re_caocao", "guanyu", "sunquan");
            Card slash = Move(snapshot, CardKind.Slash, 0);
            GameEngine game = Restore(snapshot);
            game.Play(slash.Id, 1);
            Check(game.Players[1].Hand.Count == 2 && game.Players[1].Hand.Contains(slash), "Refreshed Jianxiong gains damage card plus one draw");

            snapshot = ExpansionRig("dianwei", "re_caocao");
            game = Restore(snapshot);
            game.UseSkill(null, 1);
            game.ChooseSkillDecision("hp");
            Check(game.Players[1].Hand.Count == 1, "Refreshed Jianxiong still draws once for no-card damage");

            snapshot = ExpansionRig("re_caocao", "guanyu", "xuchu", "lvbu");
            snapshot.Players[1] = snapshot.Players[1] with { Role = Role.Rebel };
            snapshot.Players[2] = snapshot.Players[2] with { Role = Role.Loyalist };
            snapshot.Players[3] = snapshot.Players[3] with { Role = Role.Renegade };
            Move(snapshot, CardKind.Slash, 1);
            Card helperDodge = Move(snapshot, CardKind.Dodge, 2);
            // Give the attacking AI predictable non-trick draws.
            foreach (Card card in snapshot.Deck.Where(card => card.Kind == CardKind.Dodge).Take(2).ToList())
            { snapshot.Deck.Remove(card); snapshot.Deck.Add(card); }
            game = Restore(snapshot);
            int health = game.Human.Health;
            game.EndTurn();
            for (int step = 0; step < 10 && game.PendingDecision == null; step++) game.Advance();
            Check(game.PendingDecision?.Prompt.Contains("护驾") == true, "Lord may explicitly request Hujia");
            game.ChooseSkillDecision("yes");
            Check(game.Human.Health == health && !game.Players[2].Hand.Contains(helperDodge), "Wei loyalist supplies the lord's dodge");
            Check(game.Human.HujiaDrawUsed && game.Human.Hand.Count == 1, "Refreshed Hujia once-per-turn ally-dodge draw is implemented");
        }

        private static void VerifyRefreshedZhouyu()
        {
            GameEngine game = Fresh("re_zhouyu");
            Check(game.Human.Hand.Count == 7, "Refreshed Yingzi draws three");
            SavedGame snapshot = Snapshot(game);
            snapshot.Players[0] = snapshot.Players[0] with { Health = 2 };
            game = Restore(snapshot);
            Check(game.Human.HandLimit == game.Human.MaxHealth, "Refreshed Yingzi hand limit uses maximum HP");
            game.EndTurn();
            Check(game.Discard(game.Human.Hand.Take(game.Human.Hand.Count - game.Human.MaxHealth).Select(card => card.Id).ToList()), "Discard respects refreshed Yingzi limit");

            snapshot = ExpansionRig("re_zhouyu", "caocao");
            Card gift = Move(snapshot, CardKind.Dodge, 0);
            game = Restore(snapshot);
            game.UseSkill(gift.Id, 1);
            Check(game.DiscardPile.Contains(gift) && game.Players[1].Hand.Count == 0, "Refanjian recipient may reveal/discard matching suit");
            Check(game.Players[1].Health == 4, "Refanjian discard option does not lose HP");
        }

        private static void VerifyRefreshedDiaochan()
        {
            SavedGame snapshot = ExpansionRig("re_diaochan", "zhenji", "guanyu", "caocao");
            Card cost = Move(snapshot, CardKind.Blade, 0);
            snapshot.Players[0].Hand.Remove(cost); snapshot.Players[0].Equipment.Add(cost);
            GameEngine game = Restore(snapshot);
            Check(!game.UseSkill(null, 1, 2), "Refreshed Lijian rejects female targets");
            Check(game.UseSkill(null, 2, 3), "Refreshed Lijian accepts two male targets and equipment cost");
            game.ChooseSkillDecision(cost.Id.ToString());
            Check(game.DiscardPile.Contains(cost) && game.Human.SkillUsed, "Refreshed Lijian discards selected equipment once");
            Check(!game.Players[2].Hand.Contains(cost) && !game.Players[3].Hand.Contains(cost), "Lijian no-card Duel does not award its cost as damage card");

            snapshot = ExpansionRig("re_diaochan", "caocao");
            game = Restore(snapshot);
            game.EndTurn();
            Check(game.PendingDecision?.Prompt.Contains("闭月") == true, "Refreshed Biyue remains optional");
            game.ChooseSkillDecision("yes");
            Check(game.Human.Hand.Count == 2, "Empty-hand refreshed Biyue draws two");
        }

        private static void VerifyQiaobianStages()
        {
            GameEngine? game = null;
            Card? indulgence = null;
            for (int seed = 0; seed < 200; seed++)
            {
                GameEngine candidate = new(seed); candidate.Start("zhanghe");
                Card? card = candidate.Human.Hand.FirstOrDefault(card => card.Kind == CardKind.Indulgence);
                if (card != null) { game = candidate; indulgence = card; break; }
            }
            Check(game != null && indulgence != null, "Found seeded Qiaobian judgment fixture");
            game!.Human.Hand.Remove(indulgence!); game.Human.Judgments.Add(indulgence!);
            game.Advance();
            Check(game.PendingDecision?.Prompt.Contains("判定") == true, "Qiaobian offers judgment skip");
            game.ChooseSkillDecision(game.PendingDecision!.Options[0].Key);
            game.ChooseSkillDecision("no"); // normal draw
            game.ChooseSkillDecision("no"); // normal play
            Check(game.Phase == GamePhase.Play && game.Human.Judgments.Contains(indulgence!), "Skipping judgment retains delayed trick for next turn");
            game.EndTurn();
            Check(game.PendingDecision?.Prompt.Contains("弃牌") == true, "Qiaobian offers discard-stage skip");
            game.ChooseSkillDecision(game.PendingDecision!.Options[0].Key);
            Check(game.Phase == GamePhase.Resolving && game.Current != game.Human, "Paid Qiaobian skips discard and ends turn");

            game = new(500); game.Start("zhanghe"); game.Advance();
            game.ChooseSkillDecision("no");
            game.ChooseSkillDecision(game.PendingDecision!.Options[0].Key); // skip draw
            int before = game.Human.Hand.Count;
            string first = game.PendingDecision!.Options.First(option => option.Key != "stop").Key;
            game.ChooseSkillDecision(first);
            Check(game.PendingCardChoice?.Options.All(option => option.Zone == CardZone.Hand) == true,
                "Qiaobian requests a face-down hand position before moving to the next target");
            TargetCardOption firstSlot = game.PendingCardChoice!.Options[0];
            Check(game.ChooseTargetCard(firstSlot.Zone, firstSlot.CardId, firstSlot.HandPosition), "Qiaobian first hidden slot chosen");
            Check(game.PendingDecision!.Options.All(option => option.Key != first), "Qiaobian draw cannot pick same target twice");
            game.ChooseSkillDecision(game.PendingDecision.Options.First(option => option.Key != "stop").Key);
            TargetCardOption secondSlot = game.PendingCardChoice!.Options[0];
            Check(game.ChooseTargetCard(secondSlot.Zone, secondSlot.CardId, secondSlot.HandPosition), "Qiaobian second hidden slot chosen");
            game.ChooseSkillDecision("no"); // normal play
            Check(game.Human.Hand.Count == before + 2, "Qiaobian replaces draw with one hidden card from each of two players");

            Card? equipment = null;
            Player? owner = null;
            for (int seed = 0; seed < 200; seed++)
            {
                GameEngine candidate = new(seed); candidate.Start("zhanghe");
                Player? source = candidate.Players.Skip(1).FirstOrDefault(player => player.Hand.Any(card => card.IsEquipment));
                if (source == null) continue;
                equipment = source.Hand.First(card => card.IsEquipment);
                source.Hand.Remove(equipment); source.Equipment.Add(equipment);
                game = candidate; owner = source; break;
            }
            Check(equipment != null && owner != null, "Found Qiaobian field-move fixture");
            game.Advance();
            game.ChooseSkillDecision("no"); // normal judgment
            game.ChooseSkillDecision("no"); // normal draw
            game.ChooseSkillDecision(game.PendingDecision!.Options[0].Key); // skip play
            game.ChooseSkillDecision(equipment!.Id.ToString());
            Check(game.PendingDecision!.Options.Any(option => option.Key == "0"), "Qiaobian can move equipment into own empty slot");
            game.ChooseSkillDecision("0");
            Check(game.Human.Equipment.Contains(equipment) && !owner!.Equipment.Contains(equipment), "Qiaobian moves exactly one public field card");
            game.ChooseSkillDecision("no");
            Check(game.DeckCount + game.DiscardPile.Count + game.Players.Sum(player => player.Hand.Count + player.Equipment.Count + player.Judgments.Count) == game.PhysicalCardCount,
                "Qiaobian field move and equipment-loss callbacks preserve all cards");
        }
    }
}

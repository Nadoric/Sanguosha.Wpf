namespace Sanguosha.Core
{
    public sealed partial class GameEngine
    {
        private void RequestTargetCard(Player target, bool steal, Action continuation, bool includeJudgment = true, bool includeEquipment = true)
        {
            List<TargetCardOption> options = new();
            // Shuffle once when opening: slots are stable within a request, unrelated to hand order.
            int[] hidden = target.Hand.Select(card => card.Id).ToArray();
            for (int index = hidden.Length - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (hidden[index], hidden[other]) = (hidden[other], hidden[index]);
            }
            for (int position = 0; position < hidden.Length; position++)
                options.Add(new TargetCardOption(CardZone.Hand, null, $"手牌 · 第{position + 1}张（牌面隐藏）", position));

            foreach (Card card in target.Equipment.Where(card => includeEquipment && !ProtectedEquipment(Human, target, card, steal)))
            {
                options.Add(new TargetCardOption(CardZone.Equipment, card.Id,
                    $"装备 · {card.Suit}{card.RankText} {card.Name}"));
            }

            foreach (Card card in target.Judgments.Where(_ => includeJudgment))
            {
                options.Add(new TargetCardOption(CardZone.Judgment, card.Id,
                    $"判定 · {(target.SupplyShortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage ? "兵粮寸断" : card.Kind == CardKind.Lightning ? "闪电" : "乐不思蜀")}（{card.Suit}{card.RankText}）"));
            }

            if (options.Count == 0) { continuation(); return; }
            PendingCardChoice = new TargetCardRequest(target.Seat, steal, options, continuation) { HiddenHandIds = hidden };
            Phase = GamePhase.SelectCard;
            Status = $"请选择{(steal ? "获得" : "弃置")}{target.Hero.Name}的一张牌。";
        }

        private void ChooseOtherHandCard(Player actor, Player target, Action done)
        {
            if (!actor.IsAlive || !target.IsAlive || target.Hand.Count == 0 || Phase == GamePhase.GameOver)
            { done(); return; }
            if (actor.IsHuman)
                RequestTargetCard(target, true, () => AfterLosses(done), includeJudgment: false, includeEquipment: false);
            else
            {
                Card card = target.Hand[random.Next(target.Hand.Count)];
                target.Hand.Remove(card);
                Gain(actor, card);
                OnHandLost(target);
                AfterLosses(done);
            }
        }

        public bool ChooseTargetCard(CardZone zone, int? cardId = null, int? handPosition = null)
        {
            TargetCardRequest? request = PendingCardChoice;
            if (Phase != GamePhase.SelectCard || request == null ||
                !request.Options.Any(option => option.Zone == zone && option.CardId == cardId &&
                    (zone == CardZone.Hand ? handPosition == null || option.HandPosition == handPosition : handPosition == null)))
            {
                return false;
            }

            Player target = Players[request.TargetSeat];
            List<Card> cards = zone switch
            {
                CardZone.Equipment => target.Equipment,
                CardZone.Judgment => target.Judgments,
                _ => target.Hand
            };
            // Old explicit random-hand calls remain compatible; Desktop supplies the clicked slot.
            int? selectedId = cardId;
            if (zone == CardZone.Hand)
            {
                if (request.HiddenHandIds.Count == 0) return false;
                int position = handPosition ?? random.Next(request.HiddenHandIds.Count);
                if (position < 0 || position >= request.HiddenHandIds.Count) return false;
                selectedId = request.HiddenHandIds[position];
            }
            Card? card = cards.FirstOrDefault(item => item.Id == selectedId);
            if (card == null)
            {
                return false;
            }

            PendingCardChoice = null;
            Phase = GamePhase.Resolving;
            cards.Remove(card);
            if (zone == CardZone.Judgment) target.SupplyShortages.Remove(card.Id);
            if (request.Steal)
            {
                Gain(Human, card);
            }
            else
            {
                DiscardPile.Add(card);
            }

            if (zone == CardZone.Hand)
            {
                OnHandLost(target);
            }
            else if (zone == CardZone.Equipment)
            {
                OnEquipmentLost(target);
            }

            string result = $"你{(request.Steal ? "获得" : "弃置")}{target.Hero.Name}的" +
                $"【{card.Name}】（{card.Suit}{card.RankText}）" +
                (request.Steal ? "，已加入手牌。" : "，已置入弃牌堆。");
            AddLog(result);
            ShowCard(card);
            request.Continue();
            if (PendingDecision == null && PendingCardChoice == null && Pending == null)
            {
                Status = result;
            }
            Notify();
            return true;
        }
    }
}

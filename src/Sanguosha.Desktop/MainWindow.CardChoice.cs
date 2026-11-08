using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        private object? cardChoiceContext;

        private void FocusChoice(Button button, object context)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (ReferenceEquals(cardChoiceContext, context) && CardChoiceOverlay.Visibility == Visibility.Visible)
                    button.Focus();
            }));
        }

        private void RefreshCardChoice()
        {
            object? context = (object?)game.PendingDecision ?? game.PendingCardChoice;
            if (context != null && ReferenceEquals(context, cardChoiceContext)) return;
            cardChoiceContext = context;
            CardChoiceOverlay.Visibility = game.PendingCardChoice == null && game.PendingDecision == null
                ? Visibility.Collapsed
                : Visibility.Visible;
            CardChoiceButtons.Children.Clear();
            if (game.PendingDecision is SkillDecisionRequest decision)
            {
                CardChoiceTitle.Text = decision.Prompt;
                CardChoiceHelp.Text = "请点击一项完成技能选择；拼点仅显示你自己的手牌，双方选择后才公开。";
                foreach (SkillDecisionOption option in decision.Options)
                {
                    Button button = new()
                    {
                        Content = option.Label,
                        Tag = option.Key,
                        Margin = new Thickness(0, 5, 0, 5),
                        MinHeight = 42,
                        HorizontalContentAlignment = HorizontalAlignment.Left
                    };
                    button.Click += (_, _) =>
                    {
                        if (ReferenceEquals(game.PendingDecision, decision) && game.ChooseSkillDecision(option.Key))
                        {
                            audio.PlayEffect("card");
                            ClearSelection();
                        }
                    };
                    CardChoiceButtons.Children.Add(button);
                    if (CardChoiceButtons.Children.Count == 1) FocusChoice(button, decision);
                }
                return;
            }

            CardChoiceHelp.Text = "点击一张牌完成结算：手牌只显示背面位置，不公开身份；装备、判定区的牌可逐张明选。必须完成选择，Esc 不会代选或取消已使用的牌。";
            if (game.PendingCardChoice is not TargetCardRequest request) return;
            CardChoiceTitle.Text = game.Status;
            Player target = game.Players[request.TargetSeat];
            foreach (CardZone zone in Enum.GetValues<CardZone>())
            {
                TargetCardOption[] options = request.Options.Where(option => option.Zone == zone).ToArray();
                if (options.Length == 0) continue;
                string name = zone switch { CardZone.Hand => "手牌 · 背面选择", CardZone.Equipment => "装备区 · 明牌", _ => "判定区 · 明牌" };
                CardChoiceButtons.Children.Add(new TextBlock
                {
                    Text = $"{name}  /  {options.Length} 张", Foreground = Art.Color("#D8C48E"),
                    FontSize = 14, Margin = new Thickness(0, 12, 0, 6)
                });
                WrapPanel row = new();
                CardChoiceButtons.Children.Add(row);
                foreach (TargetCardOption option in options)
                {
                    StackPanel content = new();
                    if (zone == CardZone.Hand)
                    {
                        // Neutral InkGold card back; no hidden id, kind, suit, rank or identifying tooltip.
                        content.Children.Add(new Border
                        {
                            Width = 84, Height = 118, CornerRadius = new CornerRadius(5),
                            Background = new LinearGradientBrush(Color.FromRgb(45, 29, 32), Color.FromRgb(17, 27, 38), 45),
                            BorderBrush = Art.Color("#B39A64"), BorderThickness = new Thickness(1),
                            Child = new Border
                            {
                                Margin = new Thickness(5), BorderBrush = Art.Color("#665638"), BorderThickness = new Thickness(1),
                                Child = new TextBlock { Text = "暗", FontFamily = new FontFamily("KaiTi, SimSun"),
                                    FontSize = 34, Foreground = Art.Color("#C7AE79"),
                                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                            }
                        });
                    }
                    else
                    {
                        Card card = target.Equipment.Concat(target.Judgments).Single(item => item.Id == option.CardId);
                        content.Children.Add(new Image
                        {
                            Source = zone == CardZone.Judgment
                                ? OfficialAssets.CardFace(JudgmentFace(target, card)) : OfficialAssets.CardFace(card),
                            Width = 84, Height = 118
                        });
                    }
                    content.Children.Add(new TextBlock
                    {
                        Text = option.Label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                        FontSize = 11, Margin = new Thickness(0, 6, 0, 0), MinHeight = 32
                    });
                    Button button = new()
                    {
                        Content = content, Tag = option, Width = 124, Margin = new Thickness(0, 0, 8, 8),
                        Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        ToolTip = option.Label
                    };
                    button.Click += (sender, args) =>
                    {
                        // A queued click from an old request must not resolve the next target's same-numbered slot.
                        if (ReferenceEquals(game.PendingCardChoice, request)) TargetCardChoice_Click(sender, args);
                    };
                    row.Children.Add(button);
                    if (ReferenceEquals(option, request.Options[0])) FocusChoice(button, request);
                }
            }
        }

        private static CardKind JudgmentFace(Player target, Card card) =>
            target.SupplyShortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage ? CardKind.SupplyShortage :
            card.Kind == CardKind.Lightning ? CardKind.Lightning : CardKind.Indulgence;

        private void TargetCardChoice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: TargetCardOption option } &&
                game.ChooseTargetCard(option.Zone, option.CardId, option.HandPosition))
            {
                audio.PlayEffect("card");
                ClearSelection();
            }
        }
    }
}

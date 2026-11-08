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
                if (ReferenceEquals(cardChoiceContext, context) &&
                    CardChoiceOverlay.Visibility == Visibility.Visible)
                {
                    button.Focus();
                }
            }));
        }

        private void RefreshCardChoice()
        {
            object? context = (object?)game.PendingDecision ?? game.PendingCardChoice;
            if (context != null && ReferenceEquals(context, cardChoiceContext))
            {
                return;
            }

            cardChoiceContext = context;
            CardChoiceOverlay.Visibility = context == null ? Visibility.Collapsed : Visibility.Visible;
            CardChoiceButtons.Children.Clear();
            CardChoiceActions.Children.Clear();
            if (context == null)
            {
                return;
            }

            CardChoiceScroll.ScrollToTop();
            CardChoiceWindow.Width = 1000;
            if (game.PendingDecision is SkillDecisionRequest decision)
            {
                BuildSkillDialog(decision);
            }
            else if (game.PendingCardChoice is TargetCardRequest request)
            {
                BuildTargetCardDialog(request);
            }

            Button? first = DialogButtons().FirstOrDefault();
            if (first != null)
            {
                FocusChoice(first, context);
            }
        }

        private void BuildSkillDialog(SkillDecisionRequest decision)
        {
            string title = decision.Prompt.Split(new[] { '：', ':' }, 2)[0];
            CardChoiceTitle.Text = title.Length <= 20 ? title : "武将技能";
            CardChoicePrompt.Text = decision.Prompt;
            CardChoicePortrait.Background = Art.Portrait(game.Human.Hero);
            bool cards = decision.Options.Any(option => option.PreviewCard != null);
            bool heroes = decision.Options.Any(option => option.PreviewHeroId != null);
            CardChoiceBadge.Text = cards ? "选择卡牌" : heroes ? "选择武将" : "技能选择";
            CardChoiceHelp.Text = cards
                ? "点击牌面完成选择；仅展示本次允许查看的卡牌。"
                : heroes ? "请选择本次技能的目标武将。" : "请选择一项，完成本次技能结算。";
            CardChoiceSummary.Text = $"共 {decision.Options.Count} 个选项  ·  Tab 切换，Enter 确认";
            if (!cards && !heroes && decision.Options.Count <= 4)
            {
                CardChoiceWindow.Width = 800;
            }

            WrapPanel choices = new();
            CardChoiceButtons.Children.Add(choices);
            foreach (SkillDecisionOption option in decision.Options)
            {
                bool action = option.Key is "yes" or "no" or "cancel" or "done";
                Button button;
                if (action)
                {
                    button = new Button
                    {
                        Content = new TextBlock
                        {
                            Text = option.Label,
                            Foreground = Art.Color("#3E291A"),
                            FontFamily = new FontFamily("KaiTi, SimSun"),
                            FontSize = 16,
                            LineHeight = 17,
                            MaxHeight = 34,
                            MaxWidth = 260,
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center
                        },
                        Width = Math.Clamp(option.Label.Length * 16 + 28, 150, 290),
                        Style = (Style)FindResource(option.Key is "yes" or "done"
                            ? "DialogConfirmButton" : "DialogCancelButton")
                    };
                    CardChoiceActions.Children.Add(button);
                }
                else if (option.PreviewCard is Card card)
                {
                    button = CreateDialogTile(BuildDialogCard(card), option.Label);
                    choices.Children.Add(button);
                }
                else if (option.PreviewHeroId is string heroId)
                {
                    Border portrait = new()
                    {
                        Width = 96,
                        Height = 136,
                        Background = Art.Portrait(Heroes.Find(heroId)),
                        BorderBrush = Art.Color("#8D6F42"),
                        BorderThickness = new Thickness(1)
                    };
                    button = CreateDialogTile(portrait, option.Label);
                    choices.Children.Add(button);
                }
                else if (option.Key is "♠" or "♥" or "♣" or "♦")
                {
                    button = CreateSuitChoice(option.Key);
                    choices.Children.Add(button);
                }
                else
                {
                    button = new Button
                    {
                        Content = new TextBlock
                        {
                            Text = option.Label,
                            Foreground = Art.Color("#49331D"),
                            FontFamily = new FontFamily("KaiTi, SimSun"),
                            FontSize = 19,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 365,
                            TextAlignment = TextAlignment.Center
                        },
                        Style = (Style)FindResource("DialogOptionButton")
                    };
                    choices.Children.Add(button);
                }

                button.Tag = option.Key;
                button.ToolTip = option.Label;
                BindDialogChoice(button, decision, () => game.ChooseSkillDecision(option.Key));
            }
        }

        private void BuildTargetCardDialog(TargetCardRequest request)
        {
            Player target = game.Players[request.TargetSeat];
            CardChoiceTitle.Text = request.Steal ? "获得卡牌" : "弃置卡牌";
            CardChoiceBadge.Text = $"{target.Hero.Name} · 卡牌选择";
            CardChoicePortrait.Background = Art.Portrait(target.Hero);
            CardChoicePrompt.Text = game.Status;
            CardChoiceHelp.Text = "手牌背面选取；装备与判定牌可查看牌面。";
            CardChoiceSummary.Text = $"共 {request.Options.Count} 张可选牌  ·  点击确认，Tab 切换";
            AddCardZone(CardChoiceButtons, request, CardZone.Hand);

            CardZone[] publicZones = new[] { CardZone.Equipment, CardZone.Judgment }
                .Where(zone => request.Options.Any(option => option.Zone == zone)).ToArray();
            Grid publicCards = new();
            CardChoiceButtons.Children.Add(publicCards);
            for (int index = 0; index < publicZones.Length; index++)
            {
                publicCards.ColumnDefinitions.Add(new ColumnDefinition());
                StackPanel section = new() { Margin = new Thickness(0, 0, 10, 0) };
                Grid.SetColumn(section, index);
                publicCards.Children.Add(section);
                AddCardZone(section, request, publicZones[index]);
            }
        }

        private void AddCardZone(Panel parent, TargetCardRequest request, CardZone zone)
        {
            TargetCardOption[] options = request.Options.Where(option => option.Zone == zone).ToArray();
            if (options.Length == 0)
            {
                return;
            }

            string name = zone switch
            {
                CardZone.Hand => "手牌区",
                CardZone.Equipment => "装备区",
                _ => "判定区"
            };
            parent.Children.Add(new Border
            {
                Height = 27,
                Margin = new Thickness(0, 0, 8, 9),
                Background = new ImageBrush(OfficialInterface.DialogSection),
                Child = new TextBlock
                {
                    Text = $"{name}  ·  {options.Length} 张",
                    Foreground = Art.Color("#FBE4B7"),
                    FontFamily = new FontFamily("KaiTi, SimSun"),
                    FontSize = 16,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            WrapPanel row = new() { Tag = zone };
            parent.Children.Add(row);
            Player target = game.Players[request.TargetSeat];
            foreach (TargetCardOption option in options)
            {
                FrameworkElement face;
                string label;
                if (zone == CardZone.Hand)
                {
                    // 牌背仅绑定公开位置，不读取卡牌ID、花色、点数或种类。
                    face = new Border
                    {
                        Width = 96,
                        Height = 136,
                        Child = new Image { Source = OfficialInterface.CardBack, Stretch = Stretch.Fill }
                    };
                    label = $"手牌 {option.HandPosition + 1}";
                }
                else
                {
                    Card card = target.Equipment.Concat(target.Judgments)
                        .Single(item => item.Id == option.CardId);
                    CardKind kind = zone == CardZone.Judgment ? JudgmentFace(target, card) : card.Kind;
                    face = BuildDialogCard(card with { Kind = kind });
                    label = new Card(0, kind, card.Suit, card.Rank).Name;
                }

                Button button = CreateDialogTile(face, label);
                button.Tag = option;
                button.ToolTip = option.Label;
                BindDialogChoice(button, request, () =>
                    game.ChooseTargetCard(option.Zone, option.CardId, option.HandPosition));
                row.Children.Add(button);
            }
        }

        private static Border BuildDialogCard(Card card)
        {
            Grid face = new() { Width = 135, Height = 190 };
            face.Children.Add(new Image { Source = OfficialAssets.CardFace(card), Stretch = Stretch.Fill });
            face.Children.Add(new Image
            {
                Source = OfficialAssets.Rank(card),
                Width = 23,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 3, 0, 0)
            });
            face.Children.Add(new Image
            {
                Source = OfficialAssets.Suit(card),
                Width = 23,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 29, 0, 0)
            });
            return new Border
            {
                Width = 96,
                Height = 136,
                Child = new Viewbox { Child = face }
            };
        }

        private Button CreateDialogTile(FrameworkElement face, string label)
        {
            StackPanel content = new();
            content.Children.Add(face);
            content.Children.Add(new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                Foreground = Art.Color("#49331D"),
                FontSize = 11,
                LineHeight = 15,
                Margin = new Thickness(0, 7, 0, 0),
                MinHeight = 30,
                MaxHeight = 45
            });
            return new Button
            {
                Content = content,
                Style = (Style)FindResource("DialogCardButton"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
        }

        private Button CreateSuitChoice(string suit)
        {
            string name = suit switch
            {
                "♠" => "黑桃",
                "♥" => "红桃",
                "♣" => "梅花",
                _ => "方块"
            };
            StackPanel content = new();
            content.Children.Add(new TextBlock
            {
                Text = suit,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 48,
                Foreground = Art.Color(suit is "♥" or "♦" ? "#A73B29" : "#46351F"),
                TextAlignment = TextAlignment.Center
            });
            content.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 15,
                Foreground = Art.Color("#49331D"),
                TextAlignment = TextAlignment.Center
            });
            return new Button
            {
                Content = content,
                Style = (Style)FindResource("DialogOptionButton"),
                MinWidth = 0,
                Width = 150,
                Height = 110
            };
        }

        private void BindDialogChoice(Button button, object context, Func<bool> choose)
        {
            button.Click += (_, _) =>
            {
                // 防止旧弹窗的排队点击误触后续技能或同编号手牌位置。
                if (!ReferenceEquals(cardChoiceContext, context))
                {
                    return;
                }

                if (choose())
                {
                    audio.PlayEffect("card");
                    ClearSelection();
                }
            };
        }

        private static IEnumerable<T> DialogElements<T>(Panel parent) where T : FrameworkElement
        {
            foreach (FrameworkElement child in parent.Children.OfType<FrameworkElement>())
            {
                if (child is T match)
                {
                    yield return match;
                }

                if (child is Panel panel)
                {
                    foreach (T nested in DialogElements<T>(panel))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private IEnumerable<Button> DialogButtons()
        {
            return DialogElements<Button>(CardChoiceButtons)
                .Concat(CardChoiceActions.Children.OfType<Button>());
        }

        private static CardKind JudgmentFace(Player target, Card card)
        {
            if (target.SupplyShortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage)
            {
                return CardKind.SupplyShortage;
            }

            return card.Kind == CardKind.Lightning ? CardKind.Lightning : CardKind.Indulgence;
        }
    }
}

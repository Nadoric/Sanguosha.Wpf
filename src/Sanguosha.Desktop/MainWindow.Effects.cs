using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        private sealed record BattleEffect(FrameworkElement Visual, Storyboard Animation, Canvas Host, bool IsKill, bool IsStart);

        private readonly List<BattleEffect> battleEffects = new();
        private readonly Dictionary<string, DateTimeOffset> skillEffectTimes = new();
        private bool visualEffectsEnabled = true;

        public string VisualEffectsLabel => visualEffectsEnabled ? "特效：开" : "特效：关";

        private void VisualEffects_Click(object sender, RoutedEventArgs e)
        {
            visualEffectsEnabled = !visualEffectsEnabled;
            audio.KillEffectsEnabled = visualEffectsEnabled;
            ClearBattleEffects();
            RaiseAll();
        }

        private void ClearBattleEffects()
        {
            ResetKillSequence();
            audio.StopKillAnnouncements();
            foreach (BattleEffect effect in battleEffects.ToArray())
            {
                RemoveBattleEffect(effect);
            }

            skillEffectTimes.Clear();
        }

        private void RemoveBattleEffect(BattleEffect effect)
        {
            effect.Animation.Remove(effect.Host);
            effect.Host.Children.Remove(effect.Visual);
            battleEffects.Remove(effect);
            UpdateKillSkipButton();
        }

        private static Color EffectColor(string color)
        {
            return (Color)ColorConverter.ConvertFromString(color);
        }

        private static void Animate(
            Storyboard storyboard,
            DependencyObject target,
            DependencyProperty property,
            double from,
            double to,
            double seconds,
            double delay = 0)
        {
            DoubleAnimation animation = new(from, to, TimeSpan.FromSeconds(seconds))
            {
                BeginTime = TimeSpan.FromSeconds(delay),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            storyboard.Children.Add(animation);
        }

        private void PlayBattleEffect(
            FrameworkElement visual,
            Storyboard storyboard,
            double seconds,
            double delay = 0,
            Canvas? host = null,
            bool isKill = false,
            bool isStart = false,
            Action? completed = null)
        {
            host ??= EffectsCanvas;
            // 上限与生命周期集中管理，快速AI回合也不会积累不可见元素或动画时钟。
            while (battleEffects.Count >= 32)
            {
                RemoveBattleEffect(battleEffects.FirstOrDefault(effect => !effect.IsKill && !effect.IsStart) ?? battleEffects[0]);
            }

            visual.IsHitTestVisible = false;
            visual.Opacity = 0;
            Animate(storyboard, visual, OpacityProperty, 0, 1, 0.1);
            Animate(storyboard, visual, OpacityProperty, 1, 0, 0.25, seconds - 0.25);
            storyboard.BeginTime = TimeSpan.FromSeconds(delay);
            BattleEffect effect = new(visual, storyboard, host, isKill, isStart);
            storyboard.Completed += (_, _) =>
            {
                if (!battleEffects.Contains(effect)) return;
                RemoveBattleEffect(effect);
                completed?.Invoke();
            };
            host.Children.Add(visual);
            battleEffects.Add(effect);
            UpdateKillSkipButton();
            storyboard.Begin(host, HandoffBehavior.SnapshotAndReplace, true);
        }

        private Point SeatCenter(int? seat)
        {
            if (seat == 0)
            {
                return new Point(1340, 766);
            }

            Player? player = game.Players.FirstOrDefault(item => item.Seat == seat);
            if (player == null)
            {
                return new Point(588, 355);
            }

            PlayerView view = new(player, game, null);
            return new Point(view.X + 86, view.Y + 160);
        }

        private Point CardOrigin(GameCue cue)
        {
            if (cue.Seat == 0 && cue.PhysicalCard != null)
            {
                CardView? view = HandCards.FirstOrDefault(card => card.Id == cue.PhysicalCard.Id);
                if (view != null && HandItems.ItemContainerGenerator.ContainerFromItem(view)
                    is FrameworkElement container && container.ActualWidth > 0)
                {
                    Point point = container.TranslatePoint(
                        new Point(container.ActualWidth / 2, 85), SceneRoot);
                    if (point.X >= 175 && point.X <= 1235)
                    {
                        return point;
                    }
                }

                return new Point(675, 770);
            }

            return SeatCenter(cue.Seat);
        }

        private void RenderBattleCue(GameCue cue)
        {
            if (cue.Kind == GameCueKind.MatchStarted)
            {
                ClearBattleEffects();
                OfficialFrameLibrary.Warm("start"); OfficialFrameLibrary.Warm("kill1");
                if (visualEffectsEnabled && !paused && HasGame && Lobby.Visibility != Visibility.Visible &&
                    RulesOverlay.Visibility != Visibility.Visible && AudioOverlay.Visibility != Visibility.Visible &&
                    SkinsOverlay.Visibility != Visibility.Visible) RenderBattleStart();
                return;
            }

            if (cue.Kind == GameCueKind.MatchEnded)
            {
                // Elimination is emitted immediately before victory, not after a UI delay.
                foreach (BattleEffect effect in battleEffects.Where(effect => !effect.IsKill).ToArray())
                    RemoveBattleEffect(effect);
                return;
            }

            if (!visualEffectsEnabled || paused || !HasGame ||
                Lobby.Visibility == Visibility.Visible || RulesOverlay.Visibility == Visibility.Visible ||
                AudioOverlay.Visibility == Visibility.Visible || ResultOverlay.Visibility == Visibility.Visible ||
                SkinsOverlay.Visibility == Visibility.Visible)
            {
                return;
            }

            Point center = SeatCenter(cue.Seat);
            switch (cue.Kind)
            {
                case GameCueKind.CardPlayed:
                case GameCueKind.CardResponded:
                    FlyCard(cue);
                    RenderCardImpact(cue, center);
                    break;
                case GameCueKind.SkillActivated:
                    ShowSkillBanner(cue);
                    Burst(center, "#EFD28E", 0.85);
                    break;
                case GameCueKind.Damaged:
                    Burst(center, "#FF8872", 0.85, 0.2);
                    Slash(center + new Vector(-65, 58), center + new Vector(65, -58), "#FFD0A3");
                    FloatingText(center, $"−{Math.Max(1, cue.Amount)}", "#FFB09B", 43, 0.2);
                    break;
                case GameCueKind.Healed:
                    Burst(center, "#8EE6B4", 1.05);
                    FloatingText(center, $"+{Math.Max(1, cue.Amount)}", "#B5F3C8", 39);
                    break;
                case GameCueKind.ArmorJudgment:
                    Burst(center, "#9BD9ED", 1.1);
                    FloatingText(center, "八卦", "#D1F2F5", 25);
                    break;
                case GameCueKind.Eliminated:
                    RenderElimination(cue);
                    break;
                case GameCueKind.NearDeath:
                    FloatingText(center, "濒死", "#FFB7A4", 30);
                    break;
            }
        }

        private void FlyCard(GameCue cue)
        {
            if (cue.Card is not CardKind kind)
            {
                return;
            }

            Card card = cue.PhysicalCard ?? new Card(0, kind, "♠", 1);
            Card displayed = card with { Kind = kind, Nature = cue.Nature };
            Grid face = new() { Width = 135, Height = 190 };
            face.Children.Add(new Image { Source = OfficialAssets.CardFace(displayed) });
            face.Children.Add(new Image
            {
                Source = OfficialAssets.Rank(displayed),
                Width = 23,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 3, 0, 0)
            });
            face.Children.Add(new Image
            {
                Source = OfficialAssets.Suit(displayed),
                Width = 23,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 29, 0, 0)
            });
            RotateTransform rotation = new(-11);
            Border visual = new()
            {
                Width = 104,
                Height = 146,
                Child = new Viewbox { Child = face },
                BorderBrush = Art.Color("#F3D59C"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = rotation,
                Effect = new DropShadowEffect
                {
                    Color = EffectColor("#E1B85F"),
                    BlurRadius = 22,
                    ShadowDepth = 0,
                    Opacity = 0.6
                }
            };
            Point from = CardOrigin(cue);
            Point to = new(588 + (cue.Kind == GameCueKind.CardResponded ? 115 : 0), 426);
            Storyboard storyboard = new();
            Animate(storyboard, visual, Canvas.LeftProperty, from.X - 52, to.X - 52, 0.38);
            DoubleAnimationUsingKeyFrames arc = new();
            arc.KeyFrames.Add(new LinearDoubleKeyFrame(from.Y - 73, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            arc.KeyFrames.Add(new EasingDoubleKeyFrame(
                (from.Y + to.Y) / 2 - 120, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.18)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            arc.KeyFrames.Add(new EasingDoubleKeyFrame(
                to.Y - 73, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.38)),
                new CubicEase { EasingMode = EasingMode.EaseInOut }));
            Storyboard.SetTarget(arc, visual);
            Storyboard.SetTargetProperty(arc, new PropertyPath(Canvas.TopProperty));
            storyboard.Children.Add(arc);
            Animate(storyboard, rotation, RotateTransform.AngleProperty, -11, 0, 0.38);
            PlayBattleEffect(visual, storyboard, 0.82);
        }

        private void RenderCardImpact(GameCue cue, Point source)
        {
            if (cue.Card == CardKind.Dodge)
            {
                Burst(source, "#A0E7F1", 1.3);
                FloatingText(source, "闪", "#D9FAFF", 38);
                return;
            }

            if (cue.Card == CardKind.DrawTwo)
            {
                FloatingText(source, "摸牌 +2", "#F6DBA1", 25);
            }

            if (cue.TargetSeat != null)
            {
                Point target = SeatCenter(cue.TargetSeat);
                Slash(source, target, cue.Card == CardKind.Slash ? "#FFD596" : "#A9DFE1");
                Burst(target, cue.Card == CardKind.Slash ? "#F3BE83" : "#9FD6CF", 0.7, 0.24);
            }
            else if (cue.Card is CardKind.Barbarians or CardKind.Arrows or CardKind.Feast)
            {
                string color = cue.Card == CardKind.Feast ? "#9FE6BB" : "#F1CA8E";
                foreach (Player player in game.Players.Where(player => player.IsAlive &&
                    (player.Seat != cue.Seat || cue.Card == CardKind.Feast)))
                {
                    Burst(SeatCenter(player.Seat), color, 0.7, 0.18);
                }
            }
            else if (cue.Card is CardKind.Crossbow or CardKind.Blade or CardKind.EightTrigrams or CardKind.Horse)
            {
                Burst(source, "#EFD28E", 0.8);
            }
        }

        private void Slash(Point from, Point to, string color)
        {
            Vector direction = to - from;
            double length = direction.Length;
            if (length < 1)
            {
                return;
            }

            ScaleTransform reveal = new(0, 1);
            TransformGroup transform = new();
            transform.Children.Add(reveal);
            transform.Children.Add(new RotateTransform(Math.Atan2(direction.Y, direction.X) * 180 / Math.PI));
            Border blade = new()
            {
                Width = length,
                Height = 3,
                CornerRadius = new CornerRadius(2),
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Colors.Transparent, 0),
                        new GradientStop(EffectColor(color), 0.68),
                        new GradientStop(Colors.White, 1)
                    }, new Point(0, 0), new Point(1, 0)),
                RenderTransform = transform,
                Effect = new DropShadowEffect
                {
                    Color = EffectColor(color),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };
            Canvas.SetLeft(blade, from.X);
            Canvas.SetTop(blade, from.Y);
            Storyboard storyboard = new();
            Animate(storyboard, reveal, ScaleTransform.ScaleXProperty, 0, 1, 0.28);
            PlayBattleEffect(blade, storyboard, 0.62, 0.12);
        }

        private void Burst(Point center, string color, double strength, double delay = 0)
        {
            Canvas visual = new() { Width = 180, Height = 180 };
            Canvas.SetLeft(visual, center.X - 90);
            Canvas.SetTop(visual, center.Y - 90);
            Storyboard storyboard = new();
            Ellipse halo = new()
            {
                Width = 170,
                Height = 170,
                Fill = new RadialGradientBrush(EffectColor(color), Colors.Transparent),
                Opacity = 0.22,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            visual.Children.Add(halo);
            for (int index = 0; index < 2; index++)
            {
                ScaleTransform scale = new(0.4, 0.4);
                Ellipse ring = new()
                {
                    Width = 138,
                    Height = 138,
                    Stroke = Art.Color(color),
                    StrokeThickness = index == 0 ? 2 : 0.7,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = scale
                };
                Canvas.SetLeft(ring, 21);
                Canvas.SetTop(ring, 21);
                visual.Children.Add(ring);
                Animate(storyboard, scale, ScaleTransform.ScaleXProperty, 0.4, strength + index * 0.3, 0.68);
                Animate(storyboard, scale, ScaleTransform.ScaleYProperty, 0.4, strength + index * 0.3, 0.68);
            }

            for (int index = 0; index < 12; index++)
            {
                double angle = index * Math.PI / 6;
                Ellipse particle = new()
                {
                    Width = index % 3 == 0 ? 4 : 2,
                    Height = index % 3 == 0 ? 4 : 2,
                    Fill = Art.Color(color)
                };
                visual.Children.Add(particle);
                Animate(storyboard, particle, Canvas.LeftProperty, 88,
                    88 + Math.Cos(angle) * 84 * strength, 0.65);
                Animate(storyboard, particle, Canvas.TopProperty, 88,
                    88 + Math.Sin(angle) * 84 * strength, 0.65);
            }

            PlayBattleEffect(visual, storyboard, 0.85, delay);
        }

        private void FloatingText(Point center, string text, string color, double size, double delay = 0)
        {
            TextBlock visual = new()
            {
                Text = text,
                Width = 170,
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("KaiTi, SimSun"),
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Foreground = Art.Color(color),
                Effect = new DropShadowEffect
                {
                    Color = EffectColor("#071619"),
                    BlurRadius = 9,
                    ShadowDepth = 2,
                    Opacity = 1
                }
            };
            Canvas.SetLeft(visual, Math.Clamp(center.X - 85, 4, 1265));
            Storyboard storyboard = new();
            Animate(storyboard, visual, Canvas.TopProperty, center.Y - 25, center.Y - 95, 0.88);
            PlayBattleEffect(visual, storyboard, 1.05, delay);
        }

        private void ShowSkillBanner(GameCue cue)
        {
            if (cue.HeroId == null || string.IsNullOrWhiteSpace(cue.Skill))
            {
                return;
            }

            string key = $"{cue.HeroId}:{cue.Skill}";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (skillEffectTimes.TryGetValue(key, out DateTimeOffset previous) &&
                now - previous < TimeSpan.FromMilliseconds(650))
            {
                return;
            }

            skillEffectTimes[key] = now;
            foreach (BattleEffect effect in battleEffects.Where(item =>
                Equals(item.Visual.Tag, "skill-banner")).ToArray())
            {
                RemoveBattleEffect(effect);
            }

            Hero hero = Heroes.Find(cue.HeroId);
            Grid content = new();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.Children.Add(new Border
            {
                Background = Art.Portrait(hero),
                Width = 66,
                Height = 74,
                Margin = new Thickness(10, 0, 0, 0),
                BorderBrush = Art.Color("#AAAD965F"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            });
            StackPanel words = new() { VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(new TextBlock
            {
                Text = $"{hero.Kingdom} · {hero.Name}    /    技能发动",
                Foreground = Art.Color("#B3C4B8"),
                FontSize = 11,
                Margin = new Thickness(2, 0, 0, 5)
            });
            words.Children.Add(new TextBlock
            {
                Text = cue.Skill,
                Foreground = Art.Color("#FFE4A7"),
                FontFamily = new FontFamily("KaiTi, SimSun"),
                FontSize = 35,
                FontWeight = FontWeights.Bold
            });
            Grid.SetColumn(words, 1);
            content.Children.Add(words);
            TranslateTransform slide = new(-35, 0);
            Border banner = new()
            {
                Tag = "skill-banner",
                Width = 530,
                Height = 94,
                Padding = new Thickness(18, 8, 15, 8),
                Background = new LinearGradientBrush(
                    EffectColor("#F00D2228"), EffectColor("#E72B382F"), 0),
                BorderBrush = Art.Color("#AFD2B778"),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Child = content,
                RenderTransform = slide,
                Effect = new DropShadowEffect
                {
                    Color = EffectColor("#030E14"),
                    BlurRadius = 26,
                    ShadowDepth = 9,
                    Opacity = 0.75
                }
            };
            Canvas.SetLeft(banner, 323);
            Canvas.SetTop(banner, 300);
            Storyboard storyboard = new();
            Animate(storyboard, slide, TranslateTransform.XProperty, -35, 0, 0.28);
            PlayBattleEffect(banner, storyboard, 1.35);
        }
    }
}

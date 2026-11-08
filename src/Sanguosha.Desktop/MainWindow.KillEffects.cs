using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Sanguosha.Core;

namespace Sanguosha.Desktop;

public partial class MainWindow
{
    private readonly Queue<EliminationPresentation> pendingKillBanners = new();
    private readonly HashSet<int> pendingDeathPresentations = new();
    private bool killBannerPlaying;
    private bool HasKillEffects => killBannerPlaying || pendingKillBanners.Count > 0 || pendingDeathPresentations.Count > 0 || battleEffects.Any(effect => effect.IsKill);

    private void UpdateKillSkipButton()
    {
        if (KillSkipButton == null) return;
        KillSkipButton.Visibility = HasKillEffects || HasStartEffects ? Visibility.Visible : Visibility.Collapsed;
        KillSkipButton.Content = HasKillEffects ? "跳过击杀特效 · Esc" : "跳过开战特效 · Esc";
    }

    private void ResetKillSequence()
    {
        pendingKillBanners.Clear();
        pendingDeathPresentations.Clear();
        killBannerPlaying = false;
        UpdateKillSkipButton();
    }

    private void SkipKillEffects_Click(object sender, RoutedEventArgs e)
    {
        ResetKillSequence();
        foreach (BattleEffect effect in battleEffects.Where(effect => effect.IsKill || effect.IsStart).ToArray()) RemoveBattleEffect(effect);
        audio.StopStartEffect();
        audio.StopKillAnnouncements(); // Keep victim death voices; do not cancel a mandatory game choice.
        UpdateKillSkipButton();
    }

    private Rect SeatBounds(int seat)
    {
        SceneSeat bounds = BattleSceneLayout.Bounds(game.Players.Count, seat);
        return new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private void RenderElimination(GameCue cue)
    {
        if (cue.Seat is not int seat || seat < 0 || seat >= game.Players.Count) return;
        Player victim = game.Players[seat];
        pendingDeathPresentations.Add(seat);
        if (cue.Elimination is { Tier: > 0 } info) OfficialFrameLibrary.Warm($"kill{info.Tier}");
        Rect bounds = SeatBounds(seat);
        Grid transition = new() { Width = bounds.Width, Height = bounds.Height };
        // The real seat below is already gray. A brief color layer dissolves into that state.
        Border portrait = new()
        {
            Background = Art.Portrait(victim.Hero), Margin = new Thickness(25, 3, 20, seat == 0 ? 24 : 42)
        };
        transition.Children.Add(portrait);
        ScaleTransform slam = new(2.35, 2.35);
        Image seal = new()
        {
            Source = OfficialInterface.DeathStamp(victim, game.Mode), Width = 130, Height = 113,
            Margin = new Thickness(0, 0, 0, 12), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = slam,
            Effect = new DropShadowEffect { Color = EffectColor("#801E16"), BlurRadius = 17, ShadowDepth = 0 }
        };
        transition.Children.Add(seal);
        Canvas.SetLeft(transition, bounds.X); Canvas.SetTop(transition, bounds.Y);
        Storyboard death = new();
        Animate(death, portrait, OpacityProperty, 1, 0, 0.55);
        Animate(death, slam, ScaleTransform.ScaleXProperty, 2.35, 1, 0.32, 0.12);
        Animate(death, slam, ScaleTransform.ScaleYProperty, 2.35, 1, 0.32, 0.12);
        PlayBattleEffect(transition, death, 1.55, host: KillEffectsCanvas, isKill: true);

        if (cue.Elimination?.KillerSeat is int killer && killer >= 0 && killer < game.Players.Count)
        {
            Rect killerBounds = SeatBounds(killer);
            Border halo = new()
            {
                Width = killerBounds.Width, Height = killerBounds.Height, CornerRadius = new CornerRadius(8),
                BorderBrush = Art.Color("#FFE3A1"), BorderThickness = new Thickness(3),
                Effect = new DropShadowEffect { Color = EffectColor("#F6BF4E"), BlurRadius = 26, ShadowDepth = 0 }
            };
            Canvas.SetLeft(halo, killerBounds.X); Canvas.SetTop(halo, killerBounds.Y);
            PlayBattleEffect(halo, new Storyboard(), 1.35, host: KillEffectsCanvas, isKill: true);
        }
        UpdateKillSkipButton();
    }

    private void OnEliminationReady(EliminationPresentation presentation)
    {
        int seat = presentation.Cue.Seat ?? -1;
        if (!pendingDeathPresentations.Remove(seat)) return; // old game / already skipped
        if (visualEffectsEnabled && !paused && HasGame && Lobby.Visibility != Visibility.Visible &&
            RulesOverlay.Visibility != Visibility.Visible && AudioOverlay.Visibility != Visibility.Visible &&
            SkinsOverlay.Visibility != Visibility.Visible && pendingKillBanners.Count < 8)
        {
            pendingKillBanners.Enqueue(presentation);
            PlayNextKillBanner();
        }
        UpdateKillSkipButton();
    }

    private void PlayNextKillBanner()
    {
        if (killBannerPlaying || !pendingKillBanners.TryDequeue(out EliminationPresentation? presentation)) return;
        killBannerPlaying = true;
        GameCue cue = presentation.Cue;
        double duration = presentation.Seconds;
        EliminationInfo info = cue.Elimination ?? new(null, cue.Seat ?? 0, 0, 0);
        Player victim = game.Players[info.VictimSeat];
        Grid banner = new() { Width = 640, Height = 320, RenderTransformOrigin = new Point(0.5, 0.5) };
        ScaleTransform scale = new(0.82, 0.82); banner.RenderTransform = scale;
        banner.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = Art.Color("#A18A42"),
            Background = new LinearGradientBrush(new GradientStopCollection
            {
                new(EffectColor("#00180D0D"), 0), new(EffectColor("#D9180D0D"), 0.2),
                new(EffectColor("#E8321513"), 0.5), new(EffectColor("#D9180D0D"), 0.8), new(EffectColor("#00180D0D"), 1)
            }, new Point(0, 0), new Point(1, 0))
        });
        banner.Children.Add(new TextBlock
        {
            Text = info.KillerSeat.HasValue ? $"{info.TierLabel}   ·   本局第 {info.MatchKills} 次击杀" : "武 将 阵 亡",
            Foreground = Art.Color("#E5BF76"), FontFamily = new FontFamily("KaiTi, SimSun"), FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 17, 0, 0)
        });
        if (info.Tier > 0)
            banner.Children.Add(new Image
            {
                Source = OfficialAssets.LoadBitmap($"KillEffects/Titles/{info.Tier}.png"), Stretch = Stretch.Uniform,
                Margin = new Thickness(68, 150, 68, 61)
            });
        else
            banner.Children.Add(new TextBlock
            {
                Text = info.Title, FontSize = 54, FontFamily = new FontFamily("KaiTi, SimSun"), Foreground = Art.Color("#D8C5AA"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
        if (info.Tier > 0)
        {
            OfficialFrameVisual sheen = new() { Margin = new Thickness(60, 0, 60, 120), IsHitTestVisible = false, Opacity = 0.8 };
            sheen.Load($"kill{info.Tier}");
            banner.Children.Add(sheen);
            // The resource names preserve original frame indices, including intentionally sampled gaps.
            DoubleAnimationUsingKeyFrames frames = OfficialFrameVisual.Clock(duration);
            Storyboard.SetTarget(frames, sheen);
            Storyboard.SetTargetProperty(frames, new PropertyPath(OfficialFrameVisual.PlaybackSecondsProperty));
            // Added below once the common storyboard has been created.
            banner.Tag = frames;
        }
        string result = info.KillerSeat is int killer ? $"{game.Players[killer].Hero.Name}  击败  {victim.Hero.Name}" : $"{victim.Hero.Name}  阵亡（无击杀来源）";
        banner.Children.Add(new TextBlock
        {
            Text = result, FontSize = 18, Foreground = Art.Color("#EFDFC0"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 24)
        });
        // A small fixed spark set: no frame loops, timers, unmanaged shaders, or unbounded particles.
        Canvas sparks = new();
        banner.Children.Add(sparks);
        Storyboard animation = new();
        if (banner.Tag is DoubleAnimationUsingKeyFrames framesAnimation) animation.Children.Add(framesAnimation);
        banner.Tag = null;
        Brush sparkBrush = Art.Color("#FFD992"); sparkBrush.Freeze();
        for (int index = 0; index < 14; index++)
        {
            double x = 22 + (index * 47 % 596), y = 62 + (index * 31 % 146);
            Ellipse spark = new() { Width = 3, Height = 3, Fill = sparkBrush };
            TranslateTransform drift = new(); spark.RenderTransform = drift;
            Canvas.SetLeft(spark, x); Canvas.SetTop(spark, y); sparks.Children.Add(spark);
            Animate(animation, drift, TranslateTransform.YProperty, 15, -38, 1.5, index * 0.02);
            Animate(animation, spark, OpacityProperty, 0.9, 0, 1.7, index * 0.02);
        }
        Animate(animation, scale, ScaleTransform.ScaleXProperty, 0.82, 1, 0.28);
        Animate(animation, scale, ScaleTransform.ScaleYProperty, 0.82, 1, 0.28);
        Canvas.SetLeft(banner, 400); Canvas.SetTop(banner, 225);
        PlayBattleEffect(banner, animation, duration, host: KillEffectsCanvas, isKill: true, completed: () =>
        {
            killBannerPlaying = false;
            PlayNextKillBanner();
            UpdateKillSkipButton();
        });
    }
}

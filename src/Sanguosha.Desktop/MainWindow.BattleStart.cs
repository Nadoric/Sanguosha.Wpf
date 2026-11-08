using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Sanguosha.Core;

namespace Sanguosha.Desktop;

public partial class MainWindow
{
    private bool HasStartEffects => battleEffects.Any(effect => effect.IsStart);

    private void RenderBattleStart()
    {
        // MatchStarted is raised after selection and before BeginTurn is queued.
        // Loading a save never raises it. Do not delay the engine or capture gameplay input.
        Grid start = new() { Width = 700, Height = 340, RenderTransformOrigin = new Point(0.5, 0.5) };
        Image glow = new()
        {
            Source = OfficialAssets.LoadBitmap("GameStart/Sprites/glow.png"), Width = 510, Height = 310,
            Stretch = Stretch.Fill, Opacity = 0.55
        };
        start.Children.Add(glow);
        Image left = new()
        {
            Source = OfficialAssets.LoadBitmap("GameStart/Sprites/blade-left.png"), Width = 230, Height = 265,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(120, 0, 0, 40)
        };
        Image right = new()
        {
            Source = OfficialAssets.LoadBitmap("GameStart/Sprites/blade-right.png"), Width = 230, Height = 265,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 120, 40)
        };
        TranslateTransform leftMotion = new(-130, 0), rightMotion = new(130, 0);
        left.RenderTransform = leftMotion; right.RenderTransform = rightMotion;
        start.Children.Add(left); start.Children.Add(right);
        ScaleTransform titleScale = new(1.7, 1.7);
        Image title = new()
        {
            Source = OfficialAssets.LoadBitmap("GameStart/Sprites/title.png"), Width = 245, Height = 245, Opacity = 0,
            Stretch = Stretch.Uniform, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = titleScale
        };
        start.Children.Add(title);
        start.Children.Add(new TextBlock
        {
            Text = $"{game.ModeName}  ·  {game.Players.Count} 人入局", FontFamily = new FontFamily("KaiTi, SimSun"), FontSize = 20,
            Foreground = Art.Color("#FFE6AA"), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 12)
        });
        OfficialFrameVisual sheen = new() { Width = 490, Height = 245, PlaybackSeconds = -0.45, Margin = new Thickness(0, 0, 0, 15), IsHitTestVisible = false };
        sheen.Load("start"); start.Children.Add(sheen);
        Storyboard animation = new();
        DoubleAnimationUsingKeyFrames scan = OfficialFrameVisual.Clock(2, 0.45);
        Storyboard.SetTarget(scan, sheen);
        Storyboard.SetTargetProperty(scan, new PropertyPath(OfficialFrameVisual.PlaybackSecondsProperty));
        animation.Children.Add(scan);
        Animate(animation, leftMotion, TranslateTransform.XProperty, -130, 0, 0.28);
        Animate(animation, rightMotion, TranslateTransform.XProperty, 130, 0, 0.28);
        Animate(animation, title, OpacityProperty, 0, 1, 0.10, 0.18);
        Animate(animation, titleScale, ScaleTransform.ScaleXProperty, 1.7, 1, 0.28, 0.18);
        Animate(animation, titleScale, ScaleTransform.ScaleYProperty, 1.7, 1, 0.28, 0.18);
        Canvas.SetLeft(start, 370); Canvas.SetTop(start, 220);
        PlayBattleEffect(start, animation, 2.0, isStart: true);
    }
}

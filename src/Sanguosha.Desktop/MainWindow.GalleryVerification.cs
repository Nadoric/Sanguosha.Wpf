using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop;
public partial class MainWindow
{
    // Windows-only actual generator/visual-tree checks. A generator entry alone is NOT proof
    // that its recycled container is still attached (the old verification missed exactly this).
    private void VerifyGalleryReachability()
    {
        FrameworkElement root = (FrameworkElement)Content;
        double originalWidth = HeroList.Width;
        GameMode originalMode = selectedMode;
        Size currentSize = new(1440, 900);
        void Layout()
        {
            root.Measure(currentSize);
            root.Arrange(new Rect(new Point(), currentSize));
            root.UpdateLayout();
            Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            root.UpdateLayout();
        }
        static T? Descendant<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typed && (predicate == null || predicate(typed))) return typed;
                T? nested = Descendant(child, predicate);
                if (nested != null) return nested;
            }
            return null;
        }
        void CheckPage(VirtualizingHeroPanel panel, HashSet<string> seen)
        {
            HeroGalleryMetrics page = HeroGalleryLayout.Measure(HeroChoices.Count, panel.ViewportWidth,
                panel.ViewportHeight, panel.VerticalOffset);
            if (Math.Abs(panel.ExtentHeight - page.ExtentHeight) > 0.1 || panel.ViewportHeight < HeroGalleryLayout.CellHeight)
                throw new InvalidOperationException("Gallery viewport / full-row extent is incorrect.");
            int visualCount = VisualTreeHelper.GetChildrenCount(panel);
            if (visualCount != Math.Max(0, page.LastIndex - page.FirstIndex + 1))
                throw new InvalidOperationException("Generated items are missing from the gallery visual tree.");
            for (int index = page.FirstIndex; index <= page.LastIndex; index++)
            {
                if (HeroList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container ||
                    !ReferenceEquals(VisualTreeHelper.GetParent(container), panel) ||
                    container.Content is not HeroView item || item.Hero.Id != HeroChoices[index].Hero.Id)
                    throw new InvalidOperationException($"Detached/stale recycled hero container: {index}.");
                Rect bounds = container.TransformToAncestor(panel).TransformBounds(new Rect(container.RenderSize));
                if (bounds.Bottom > 0 && bounds.Top < panel.ViewportHeight) seen.Add(item.Hero.Id);
            }
        }
        void LastRow(VirtualizingHeroPanel panel)
        {
            int index = HeroChoices.Count - 1;
            if (HeroList.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem last ||
                !ReferenceEquals(VisualTreeHelper.GetParent(last), panel))
                throw new InvalidOperationException("Final hero has no attached container.");
            Rect bounds = last.TransformToAncestor(panel).TransformBounds(new Rect(last.RenderSize));
            if (bounds.Top < -0.5 || bounds.Bottom > panel.ViewportHeight + 0.5)
                throw new InvalidOperationException("Final hero row is clipped outside the viewport.");
        }
        try
        {
            if ((string)LobbyExitButton.Content != "退出游戏" || LobbyExitButton.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Lobby exit button is missing.");
            foreach (GameMode mode in Enum.GetValues<GameMode>())
            {
                selectedMode = mode;
                SetHeroFilter("全部");
                if (HeroChoices.Count != 90 || HeroList.Items.Count != 90)
                    throw new InvalidOperationException($"Mode {mode} unexpectedly filters the roster.");
            }
            foreach (var scenario in new[]
            {
                (Client: new Size(1080, 700), Width: double.NaN),
                (Client: new Size(1460, 940), Width: 720d),
                (Client: new Size(1920, 1080), Width: 1068d)
            })
            {
                currentSize = scenario.Client;
                HeroList.Width = scenario.Width;
                foreach ((string faction, int count) in new[] { ("全部", 90), ("魏", 24), ("蜀", 24), ("吴", 23), ("群", 19) })
                {
                    SetHeroFilter(faction);
                    HeroList.SelectedIndex = 0;
                    HeroList.ScrollIntoView(HeroList.SelectedItem);
                    Layout();
                    if (HeroList.Items.Count != count || HeroChoices.Count != count ||
                        !HeroChoices.Select(item => item.Hero.Id).SequenceEqual(HeroGalleryLayout.Filter(faction).Select(hero => hero.Id)))
                        throw new InvalidOperationException($"Roster/source/count mismatch: {faction}.");
                    VirtualizingHeroPanel panel = FindGalleryPanel(HeroList)
                        ?? throw new InvalidOperationException("Virtualizing gallery panel missing.");
                    ScrollViewer scroll = panel.ScrollOwner
                        ?? throw new InvalidOperationException("Gallery is not connected to a native ScrollViewer.");
                    scroll.ScrollToTop(); Layout();
                    HashSet<string> seen = new();
                    CheckPage(panel, seen);
                    for (int step = 0; panel.VerticalOffset < panel.ExtentHeight - panel.ViewportHeight - 0.1; step++)
                    {
                        if (step > 250) throw new InvalidOperationException("Mouse wheel never reached final row.");
                        double before = panel.VerticalOffset;
                        UIElement source = (UIElement)VisualTreeHelper.GetChild(panel, 0);
                        source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
                            { RoutedEvent = Mouse.MouseWheelEvent });
                        Layout();
                        if (panel.VerticalOffset <= before) throw new InvalidOperationException("Mouse wheel did not scroll the hero list.");
                        CheckPage(panel, seen);
                    }
                    LastRow(panel);
                    if (seen.Count != count) throw new InvalidOperationException($"Wheel did not expose all {count} {faction} generals.");
                    ScrollBar bar = Descendant<ScrollBar>(scroll, item => item.Orientation == Orientation.Vertical)
                        ?? throw new InvalidOperationException("Vertical gallery scrollbar missing.");
                    foreach (double value in new[] { 0d, bar.Maximum / 2, bar.Maximum, 0d })
                    {
                        bar.RaiseEvent(new ScrollEventArgs(ScrollEventType.ThumbTrack, value) { RoutedEvent = ScrollBar.ScrollEvent });
                        Layout();
                        if (Math.Abs(panel.VerticalOffset - Math.Clamp(value, 0, Math.Max(0, panel.ExtentHeight - panel.ViewportHeight))) > 1)
                            throw new InvalidOperationException("Scrollbar thumb range does not reach all rows.");
                        CheckPage(panel, seen);
                    }
                    foreach (Key key in new[] { Key.Home, Key.Right, Key.Down, Key.PageDown, Key.End })
                    {
                        NavigateHeroGallery(key); Layout(); CheckPage(panel, seen);
                    }
                    if (HeroList.SelectedIndex != count - 1 || selectedHero != HeroChoices[^1].Hero)
                        throw new InvalidOperationException("Keyboard End did not select the final general.");
                    LastRow(panel);
                    NavigateHeroGallery(Key.Home); Layout();
                }
            }
        }
        finally
        {
            HeroList.Width = originalWidth;
            selectedMode = originalMode;
            currentSize = new Size(1440, 900);
            SetHeroFilter("全部");
            Layout();
        }
        PortraitCache.DrainForVerification();
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Sanguosha.Desktop;

// Fixed-size gallery cells; pixel scrolling plus one overscan row in each direction.
// Containers are recycled. The full roster is not measured/rendered on every scroll/filter.
public sealed class VirtualizingHeroPanel : VirtualizingPanel, IScrollInfo
{
    public const double CellWidth = HeroGalleryLayout.CellWidth;
    public const double CellHeight = HeroGalleryLayout.CellHeight;
    private Size extent, viewport;
    private double offset;
    private ScrollViewer? scrollOwner;
    private bool scrollInfoDirty;
    private HeroGalleryMetrics metrics;
    private int? bringIndex;
    public int Columns => Math.Max(1, (int)(viewport.Width / CellWidth));
    public int VisibleRows => Math.Max(1, (int)(viewport.Height / CellHeight));

    protected override Size MeasureOverride(Size availableSize)
    {
        ItemsControl? owner = ItemsControl.GetItemsOwner(this);
        if (owner == null) return new Size();
        Size previousViewport = viewport, previousExtent = extent;
        double previousOffset = offset;
        metrics = HeroGalleryLayout.Measure(owner.Items.Count, availableSize.Width, availableSize.Height, offset);
        if (bringIndex is int requested && requested >= 0 && requested < owner.Items.Count)
            metrics = HeroGalleryLayout.Measure(owner.Items.Count, availableSize.Width, availableSize.Height,
                HeroGalleryLayout.OffsetForIndex(requested, owner.Items.Count, metrics));
        else if (previousExtent.Height > previousViewport.Height &&
            Math.Abs(previousOffset - (previousExtent.Height - previousViewport.Height)) < 0.1 &&
            (previousViewport.Width != metrics.Width || previousViewport.Height != metrics.Height))
            metrics = HeroGalleryLayout.Measure(owner.Items.Count, availableSize.Width, availableSize.Height, metrics.MaximumOffset);
        viewport = new Size(metrics.Width, metrics.Height);
        extent = new Size(metrics.Width, metrics.ExtentHeight);
        offset = metrics.Offset;
        if (scrollInfoDirty || viewport != previousViewport || extent != previousExtent || previousOffset != offset)
        {
            ScrollOwner?.InvalidateScrollInfo();
            scrollInfoDirty = false;
        }
        int first = metrics.FirstIndex, last = metrics.LastIndex;
        // WPF通过InternalChildren延迟初始化生成器，必须先取得子项集合。
        UIElementCollection children = InternalChildren;
        // 切换筛选源时旧面板可能参与最后一轮布局，此时生成器已被WPF解除关联。
        if (ItemContainerGenerator is not IItemContainerGenerator generator)
        {
            return viewport;
        }
        // Remove outside the realized range before generating to keep generator positions consistent.
        for (int child = children.Count - 1; child >= 0; child--)
        {
            GeneratorPosition position = new(child, 0);
            int item = generator.IndexFromGeneratorPosition(position);
            if (item < first || item > last)
            {
                if (generator is IRecyclingItemContainerGenerator recycling) recycling.Recycle(position, 1);
                else generator.Remove(position, 1);
                RemoveInternalChildRange(child, 1);
            }
        }
        if (last >= first)
        {
            GeneratorPosition start = generator.GeneratorPositionFromIndex(first);
            int child = start.Offset == 0 ? start.Index : start.Index + 1;
            using (generator.StartAt(start, GeneratorDirection.Forward, true))
                for (int index = first; index <= last; index++, child++)
                {
                    UIElement container = (UIElement)generator.GenerateNext(out bool isNew);
                    int visualIndex = children.IndexOf(container);
                    HeroContainerPlacement placement = HeroGalleryLayout.Placement(isNew, visualIndex, child);
                    if (placement.Attach)
                    {
                        if (visualIndex >= 0) RemoveInternalChildRange(visualIndex, 1);
                        if (child >= children.Count) AddInternalChild(container);
                        else InsertInternalChild(child, container);
                    }
                    // Recycled containers need their new Content / template / IsSelected prepared,
                    // even though WPF's isNew flag is false. Never infer tree membership from that flag.
                    if (placement.Prepare) generator.PrepareItemContainer(container);
                    container.Measure(new Size(CellWidth, CellHeight));
                }
        }
        return viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElementCollection children = InternalChildren;
        if (ItemContainerGenerator is not IItemContainerGenerator generator)
        {
            return finalSize;
        }
        for (int child = 0; child < children.Count; child++)
        {
            int index = generator.IndexFromGeneratorPosition(new GeneratorPosition(child, 0));
            children[child].Arrange(new Rect((index % Columns) * CellWidth,
                (index / Columns) * CellHeight - offset, CellWidth, CellHeight));
        }
        bringIndex = null;
        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        // Keep the visual-child ordinal in sync with the generator's realized-container ordinal.
        if (args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove or
            System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
        {
            if (args.ItemUICount > 0) RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
        }
        else if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            if (args.ItemUICount > 0) RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
        }
        else if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            RemoveInternalChildRange(0, InternalChildren.Count);
            offset = 0;
            bringIndex = null;
        }
        base.OnItemsChanged(sender, args);
        InvalidateMeasure();
    }

    protected override void BringIndexIntoView(int index)
    {
        ItemsControl? owner = ItemsControl.GetItemsOwner(this);
        if (owner == null) return;
        // Use current measure metrics; the native ScrollViewer remains the sole scroll owner.
        SetVerticalOffset(HeroGalleryLayout.OffsetForIndex(index, owner.Items.Count, metrics with { Offset = offset }));
        bringIndex = index; // If another measure pass changes viewport/columns, resolve against that fresh geometry too.
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        ItemsControl? owner = ItemsControl.GetItemsOwner(this);
        DependencyObject? container = owner == null ? null : ItemsControl.ContainerFromElement(owner, visual);
        int index = container == null ? -1 : owner!.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0) return Rect.Empty;
        BringIndexIntoView(index);
        Rect bounds = new((index % Columns) * CellWidth, (index / Columns) * CellHeight - offset,
            CellWidth, CellHeight);
        bounds.Intersect(new Rect(new Point(), viewport));
        return bounds;
    }

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth => extent.Width;
    public double ExtentHeight => extent.Height;
    public double ViewportWidth => viewport.Width;
    public double ViewportHeight => viewport.Height;
    public double HorizontalOffset => 0;
    public double VerticalOffset => offset;
    public ScrollViewer? ScrollOwner
    {
        get => scrollOwner;
        set
        {
            if (ReferenceEquals(scrollOwner, value)) return;
            scrollOwner = value;
            scrollInfoDirty = true;
            scrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }
    }
    public void SetHorizontalOffset(double value) { }
    public void SetVerticalOffset(double value)
    {
        if (double.IsNaN(value)) return;
        bringIndex = null; // A wheel/track command supersedes an earlier deferred keyboard reveal.
        double next = HeroGalleryLayout.ClampOffset(value, extent.Height, viewport.Height);
        if (Math.Abs(next - offset) < 0.01) return;
        offset = next;
        InvalidateMeasure();
        ScrollOwner?.InvalidateScrollInfo();
    }
    public void LineUp() => SetVerticalOffset(offset - 48);
    public void LineDown() => SetVerticalOffset(offset + 48);
    public void PageUp() => SetVerticalOffset(offset - viewport.Height);
    public void PageDown() => SetVerticalOffset(offset + viewport.Height);
    public void MouseWheelUp() => SetVerticalOffset(offset - 144);
    public void MouseWheelDown() => SetVerticalOffset(offset + 144);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
}

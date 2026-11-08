using Sanguosha.Core;

namespace Sanguosha.Desktop;

// Pure layout / lifecycle rules shared with the Linux verification executable.
internal readonly record struct HeroGalleryMetrics(int Columns, int Rows, double Width, double Height,
    double ExtentHeight, double Offset, int FirstIndex, int LastIndex)
{
    internal double MaximumOffset => Math.Max(0, ExtentHeight - Height);
}
internal readonly record struct HeroContainerPlacement(bool Attach, bool Prepare);
internal static class HeroGalleryLayout
{
    internal const double CellWidth = 176;
    internal const double CellHeight = 204;
    internal static IReadOnlyList<Hero> Filter(string kingdom) =>
        Heroes.All.Where(hero => kingdom == "全部" || hero.Kingdom == kingdom).ToArray();

    internal static HeroContainerPlacement Placement(bool newlyCreated, int visualIndex, int desiredIndex)
    {
        // WPF GenerateNext returns false for BOTH existing AND recycled containers!
        // A recycled container has been detached, but must be attached / prepared again.
        bool attach = visualIndex != desiredIndex;
        return new(attach, newlyCreated || attach);
    }

    internal static HeroGalleryMetrics Measure(int count, double width, double height, double offset)
    {
        count = Math.Max(0, count);
        width = double.IsFinite(width) ? Math.Max(0, width) : CellWidth;
        int columns = Math.Max(1, (int)(width / CellWidth));
        int rows = (int)Math.Ceiling((double)count / columns);
        double extent = rows * CellHeight;
        // A physical-scrolling parent may measure with infinity. Report the FULL content,
        // not a fabricated three-row viewport that hides the remainder from its ScrollViewer.
        height = double.IsFinite(height) ? Math.Max(0, height) : extent;
        offset = ClampOffset(offset, extent, height);
        int first = Math.Min(count, Math.Max(0, ((int)(offset / CellHeight) - 1) * columns));
        int last = Math.Min(count - 1, ((int)Math.Ceiling((offset + height) / CellHeight) + 1) * columns - 1);
        return new(columns, rows, width, height, extent, offset, first, last);
    }

    internal static double ClampOffset(double requested, double extent, double viewport) =>
        double.IsNaN(requested) ? 0 : Math.Clamp(requested, 0, Math.Max(0, extent - viewport));

    internal static double OffsetForIndex(int index, int count, HeroGalleryMetrics metrics)
    {
        if (index < 0 || index >= count || metrics.Columns < 1) return metrics.Offset;
        double top = (index / metrics.Columns) * CellHeight;
        double requested = metrics.Offset;
        if (top < requested || metrics.Height < CellHeight) requested = top;
        else if (top + CellHeight > requested + metrics.Height) requested = top + CellHeight - metrics.Height;
        return ClampOffset(requested, metrics.ExtentHeight, metrics.Height);
    }
}

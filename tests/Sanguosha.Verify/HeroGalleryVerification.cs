using Sanguosha.Core;
using Sanguosha.Desktop;

namespace Sanguosha.Verify;
internal static partial class Program
{
    // Models WPF's documented/source behavior: a dequeued recycled container returns isNew=false.
    private sealed class GalleryContainer9 { internal int Content = -1; }
    private sealed class GalleryGenerator9
    {
        private readonly Dictionary<int, GalleryContainer9> realized = new();
        private readonly Queue<GalleryContainer9> recycle = new();
        private readonly List<GalleryContainer9> visuals = new();
        internal readonly HashSet<int> Seen = new();
        internal int Reused;
        internal bool Visit(HeroGalleryMetrics metrics, bool emulateOldBug = false)
        {
            foreach (int index in realized.Keys.Where(index => index < metrics.FirstIndex || index > metrics.LastIndex).ToArray())
            {
                GalleryContainer9 container = realized[index]; realized.Remove(index);
                visuals.Remove(container); recycle.Enqueue(container);
            }
            int slot = 0;
            for (int index = metrics.FirstIndex; index <= metrics.LastIndex; index++, slot++)
            {
                bool isNew = false;
                if (!realized.TryGetValue(index, out GalleryContainer9? container))
                {
                    if (recycle.TryDequeue(out container)) Reused++; // NOT newly created, but no longer a visual child.
                    else { container = new(); isNew = true; }
                    realized.Add(index, container);
                }
                int visualIndex = visuals.IndexOf(container);
                HeroContainerPlacement plan = emulateOldBug ? new(isNew, isNew) :
                    HeroGalleryLayout.Placement(isNew, visualIndex, slot);
                if (plan.Attach)
                {
                    if (visualIndex >= 0) visuals.RemoveAt(visualIndex);
                    visuals.Insert(Math.Min(slot, visuals.Count), container);
                }
                if (plan.Prepare) container.Content = index;
            }
            if (visuals.Count != Math.Max(0, metrics.LastIndex - metrics.FirstIndex + 1)) return false;
            for (int index = metrics.FirstIndex; index <= metrics.LastIndex; index++)
            {
                GalleryContainer9 container = visuals[index - metrics.FirstIndex];
                if (!ReferenceEquals(container, realized[index]) || container.Content != index) return false;
                Seen.Add(index);
            }
            return true;
        }
    }

    private static void VerifyHeroGalleryLayout()
    {
        Dictionary<string, int> expected = new() { ["全部"] = 90, ["魏"] = 24, ["蜀"] = 24, ["吴"] = 23, ["群"] = 19 };
        Check(HeroGalleryLayout.Filter("全部").Select(hero => hero.Id).SequenceEqual(Heroes.All.Select(hero => hero.Id)),
            "Gallery All uses every registered general, preserving standard/refreshed variants and ordering");
        Check(HeroGalleryLayout.OffsetForIndex(0, 90, default) == 0, "BringIntoView before first measure does not divide by zero");
        HeroContainerPlacement reuse = HeroGalleryLayout.Placement(false, -1, 0);
        Check(reuse.Attach && reuse.Prepare, "Recycled WPF container with isNew=false must re-enter visual tree AND be prepared");
        Check(!HeroGalleryLayout.Placement(false, 0, 0).Attach && !HeroGalleryLayout.Placement(false, 0, 0).Prepare,
            "Already attached unchanged container is not needlessly recreated/prepared");
        GalleryGenerator9 old = new();
        Check(old.Visit(HeroGalleryLayout.Measure(90, 1232, 408, 0), true), "Old panel could render its first page");
        Check(!old.Visit(HeroGalleryLayout.Measure(90, 1232, 408, double.PositiveInfinity), true),
            "Reproduce patch8 lifecycle defect: recycled false flag leaves later rows absent from visual tree");
        int reused = 0, layouts = 0;
        foreach ((string faction, int count) in expected)
        {
            IReadOnlyList<Hero> roster = HeroGalleryLayout.Filter(faction);
            Check(roster.Count == count && roster.Select(hero => hero.Id).Distinct().Count() == count,
                $"Actual gallery source count and unique identities: {faction}={count}");
            Check(faction == "全部" || roster.All(hero => hero.Kingdom == faction), "Faction source has no other faction");
            // DIP widths include column thresholds/fractional widths after 100/125/150/200% scaling.
            foreach (double width in new[] { 352d, 528d, 704d, 1056d, 1231.75, 1232, 1232.25 })
            foreach (double height in new[] { 204d, 305.5, 408d, 612d })
            {
                GalleryGenerator9 model = new();
                HeroGalleryMetrics start = HeroGalleryLayout.Measure(count, width, height, 0);
                Check(start.Rows == (int)Math.Ceiling((double)count / start.Columns) &&
                    start.ExtentHeight == start.Rows * HeroGalleryLayout.CellHeight, "Scroll extent includes the final partial row");
                for (double offset = 0; offset < start.MaximumOffset; offset += 144)
                {
                    HeroGalleryMetrics page = HeroGalleryLayout.Measure(count, width, height, offset);
                    Check(model.Visit(page), "Every wheel page has attached/prepared containers with correct identity");
                    layouts++;
                }
                HeroGalleryMetrics end = HeroGalleryLayout.Measure(count, width, height, double.PositiveInfinity);
                Check(model.Visit(end), "Scrollbar bottom jump attaches recycled containers");
                Check(model.Seen.Count == count && Enumerable.Range(0, count).All(model.Seen.Contains),
                    $"All {count} {faction} generals reachable, not just the initial realized page");
                Check(model.Visit(start) && model.Visit(HeroGalleryLayout.Measure(count, width, height, start.MaximumOffset / 2)) && model.Visit(end),
                    "Back-scroll and thumb jumps keep recycled visual/content mapping consistent");
                for (int index = 0; index < count; index++)
                {
                    double requested = HeroGalleryLayout.OffsetForIndex(index, count, end);
                    HeroGalleryMetrics page = HeroGalleryLayout.Measure(count, width, height, requested);
                    double top = (index / page.Columns) * HeroGalleryLayout.CellHeight;
                    Check(top >= page.Offset - 0.001 && top + HeroGalleryLayout.CellHeight <= page.Offset + page.Height + 0.001 &&
                        index >= page.FirstIndex && index <= page.LastIndex, "Keyboard ScrollIntoView fully exposes the chosen row, including final row");
                }
                reused += model.Reused;
            }
        }
        HeroGalleryMetrics physical = HeroGalleryLayout.Measure(90, 1232, double.PositiveInfinity, 0);
        Check(physical.Height == physical.ExtentHeight && physical.FirstIndex == 0 && physical.LastIndex == 89,
            "Infinite-height physical-scroll fallback reports ALL content, not a fabricated three-row extent");
        Check(HeroGalleryLayout.Measure(0, 0, 204, 0).LastIndex == -1, "Empty roster has no generated items");
        Check(reused > 0, "Regression suite exercised actual simulated recycled false-flag returns");
        Console.WriteLine($"Gallery regression: 90/24/24/23/19 entries; {layouts} wheel layouts; {reused} recycled containers checked, all rows reachable.");
    }
}

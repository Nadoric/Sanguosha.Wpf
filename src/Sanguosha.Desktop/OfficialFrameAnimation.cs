using System.Globalization;
using System.IO;
using System.Resources;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace Sanguosha.Desktop;

internal sealed record EffectFrame(int X, int Y, int Width, int Height, double Time);
internal sealed record EffectSequence(string Key, string Asset, double Duration, EffectFrame[] Frames);
internal sealed record DecodedEffect(EffectSequence Sequence, BitmapSource[] Frames);

internal static class OfficialFrameLibrary
{
    private static readonly ResourceManager Resources = CreateResources();
    private static readonly SemaphoreSlim Decoders = new(2);
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DecodedEffect Value, long Age)> Cache = new();
    private static readonly Dictionary<string, Task<DecodedEffect>> Loading = new();
    private static long age;
    private static readonly Lazy<Dictionary<string, EffectSequence>> Catalog = new(() =>
    {
        using Stream s = Resources.GetStream("assets/official/effectframes/animations.json", CultureInfo.InvariantCulture)!;
        return JsonSerializer.Deserialize<EffectSequence[]>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.ToDictionary(x => x.Key);
    });
    private static ResourceManager CreateResources()
    {
        var assembly = typeof(OfficialFrameLibrary).Assembly;
        string name = assembly.GetManifestResourceNames().Single(x => x.EndsWith(".g.resources", StringComparison.Ordinal));
        return new ResourceManager(name[..^".resources".Length], assembly);
    }
    internal static Task<DecodedEffect> Get(string key)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached)) { Cache[key] = (cached.Value, ++age); return Task.FromResult(cached.Value); }
            if (Loading.TryGetValue(key, out var task)) return task;
            Task<DecodedEffect> load = Task.Run(async () =>
            {
                await Decoders.WaitAsync().ConfigureAwait(false);
                try
                {
                    EffectSequence sequence = Catalog.Value[key];
                    using Stream s = Resources.GetStream(("assets/official/" + sequence.Asset).ToLowerInvariant(), CultureInfo.InvariantCulture)!;
                    BitmapImage bitmap = new(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = s; bitmap.EndInit(); bitmap.Freeze();
                    BitmapSource[] frames = sequence.Frames.Select(f =>
                    {
                        CroppedBitmap crop = new(bitmap, new Int32Rect(f.X, f.Y, f.Width, f.Height)); crop.Freeze(); return (BitmapSource)crop;
                    }).ToArray();
                    DecodedEffect decoded = new(sequence, frames);
                    lock (Gate)
                    {
                        Cache[key] = (decoded, ++age);
                        while (Cache.Count > 4) Cache.Remove(Cache.MinBy(x => x.Value.Age).Key);
                    }
                    return decoded;
                }
                finally { Decoders.Release(); }
            });
            Loading[key] = load;
            _ = load.ContinueWith(_ => { lock (Gate) Loading.Remove(key); }, TaskScheduler.Default);
            return load;
        }
    }
    internal static void Warm(string key) { _ = Get(key).ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted); }
}

// One linear WPF clock per sequence; OnRender only draws a cached, frozen shared-atlas crop.
internal sealed class OfficialFrameVisual : FrameworkElement
{
    public static readonly DependencyProperty PlaybackSecondsProperty = DependencyProperty.Register(nameof(PlaybackSeconds), typeof(double),
        typeof(OfficialFrameVisual), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double PlaybackSeconds { get => (double)GetValue(PlaybackSecondsProperty); set => SetValue(PlaybackSecondsProperty, value); }
    private DecodedEffect? decoded;
    internal async void Load(string key)
    {
        try { decoded = await OfficialFrameLibrary.Get(key); InvalidateVisual(); }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException or JsonException or ArgumentException or MissingManifestResourceException or KeyNotFoundException)
        { System.Diagnostics.Trace.TraceWarning($"Official frame fallback: {key}: {e.Message}"); }
    }
    internal static DoubleAnimationUsingKeyFrames Clock(double seconds, double delay = 0)
    {
        DoubleAnimationUsingKeyFrames clock = new() { Duration = TimeSpan.FromSeconds(seconds) };
        for (int i = 0; i <= Math.Ceiling(seconds * 24); i++)
        {
            double time = Math.Min(i / 24d, seconds);
            clock.KeyFrames.Add(new DiscreteDoubleKeyFrame(time - delay, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(time))));
        }
        return clock;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (decoded == null || PlaybackSeconds < 0) return;
        double end = Math.Min(decoded.Sequence.Duration, decoded.Sequence.Frames[^1].Time + 2d/24);
        if (PlaybackSeconds > end) return;
        int index = 0;
        while (index + 1 < decoded.Frames.Length && decoded.Sequence.Frames[index + 1].Time <= PlaybackSeconds) index++;
        double width = Math.Min(ActualWidth, ActualHeight * 2);
        double height = width / 2;
        dc.PushOpacity(Math.Clamp((end-PlaybackSeconds)/0.16, 0, 1));
        dc.DrawImage(decoded.Frames[index], new Rect((ActualWidth-width)/2, (ActualHeight-height)/2, width, height));
        dc.Pop();
    }
}

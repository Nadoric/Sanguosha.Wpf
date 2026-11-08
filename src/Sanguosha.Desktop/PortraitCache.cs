using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop;

// All brushes belong to the UI dispatcher; only frozen decoded bitmaps cross threads.
// At most two WIC decoders run at once. No full-resolution portrait decode on the UI thread.
internal static class PortraitCache
{
    private static readonly SemaphoreSlim Decoders = new(2);
    private static readonly ResourceManager Resources = CreateResourceManager();
    private static ResourceManager CreateResourceManager()
    {
        System.Reflection.Assembly assembly = typeof(PortraitCache).Assembly;
        string name = assembly.GetManifestResourceNames().Single(value => value.EndsWith(".g.resources", StringComparison.Ordinal));
        return new ResourceManager(name[..^".resources".Length], assembly);
    }
    private static readonly Dictionary<(string Asset, int Width, bool Grayscale), ImageBrush> Brushes = new();
    private static readonly List<Task> Pending = new();
    private static readonly ConcurrentQueue<string> Failures = new();
    internal static IReadOnlyCollection<string> Errors => Failures.ToArray();

    internal static ImageBrush Get(HeroSkin skin, bool grayscale = false)
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;
        dispatcher.VerifyAccess();
        const int width = 384; // 2x a gallery tile, also adequate for the board / skin preview.
        var key = (skin.Asset, width, grayscale);
        if (Brushes.TryGetValue(key, out ImageBrush? cached)) return cached;
        ImageBrush brush = new() { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top };
        Brushes.Add(key, brush);
        string fallback = $"Portraits/{Heroes.Find(skin.HeroId).BaseHeroId ?? skin.HeroId}.png";
        Pending.Add(FillAsync(brush, skin.Asset, fallback, width, grayscale, dispatcher));
        return brush;
    }

    private static async Task FillAsync(ImageBrush brush, string asset, string fallback, int width, bool grayscale, Dispatcher dispatcher)
    {
        BitmapSource? bitmap = await Task.Run(async () =>
        {
            await Decoders.WaitAsync().ConfigureAwait(false);
            try
            {
                try { return Decode(asset, width); }
                catch (Exception error) when (IsImageError(error))
                {
                    Report(asset, error);
                    if (asset != fallback)
                    {
                        try { return Decode(fallback, width); }
                        catch (Exception fallbackError) when (IsImageError(fallbackError)) { Report(fallback, fallbackError); }
                    }
                    return null;
                }
            }
            finally { Decoders.Release(); }
        }).ConfigureAwait(false);
        if (grayscale && bitmap != null)
        {
            BitmapSource decoded = bitmap;
            bitmap = await Task.Run(() =>
            {
                FormatConvertedBitmap gray = new(decoded, PixelFormats.Gray8, null, 0);
                gray.Freeze();
                return gray;
            }).ConfigureAwait(false);
        }
        if (!dispatcher.HasShutdownStarted)
            await dispatcher.InvokeAsync(() =>
            {
                brush.ImageSource = bitmap;
                brush.Freeze(); // Change notification repaints every bound card, without rebuilding its container.
            }, DispatcherPriority.Background);
    }

    private static BitmapSource Decode(string asset, int width)
    {
        // ResourceManager.GetStream is thread-safe. Do not use Application.GetResourceStream
        // (a WPF package API without a thread-safety guarantee) on decoder workers.
        using Stream stream = Resources.GetStream($"assets/official/{asset}".ToLowerInvariant(), CultureInfo.InvariantCulture)
            ?? throw new FileNotFoundException($"Embedded portrait missing: {asset}");
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static bool IsImageError(Exception error) => error is IOException or NotSupportedException or
        InvalidOperationException or ArgumentException or FormatException or
        MissingManifestResourceException or System.Runtime.InteropServices.COMException;
    private static void Report(string asset, Exception error)
    {
        string message = $"{asset}: {error.Message}";
        Failures.Enqueue(message);
        Trace.TraceError(message); // One attempt per cached asset, never a repeated binding exception loop.
    }

    // Windows-only verification uses a dispatcher frame, never a blocking Wait on the UI thread.
    internal static void DrainForVerification()
    {
        Application.Current.Dispatcher.VerifyAccess();
        Task pending = Task.WhenAll(Pending.ToArray());
        if (!pending.IsCompleted)
        {
            DispatcherFrame frame = new();
            DispatcherTimer timeout = new(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(60) };
            bool timedOut = false;
            timeout.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
            timeout.Start();
            _ = pending.ContinueWith(_ => Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background, new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            timeout.Stop();
            if (timedOut) throw new TimeoutException("Portrait decoder did not finish within 60 seconds.");
        }
        pending.GetAwaiter().GetResult();
        if (!Failures.IsEmpty) throw new InvalidOperationException(string.Join(Environment.NewLine, Failures));
    }
}

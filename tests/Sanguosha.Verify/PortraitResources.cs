using System.Collections;
using System.Reflection;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Xml.Linq;
using Sanguosha.Core;
using Sanguosha.Desktop;

namespace Sanguosha.Verify;
internal static partial class Program
{
    private static void VerifyPortraitResources()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Sanguosha.slnx"))) root = root.Parent;
        Check(root != null, "Locate source root for official portrait regression checks");
        string desktop = Path.Combine(root!.FullName, "src", "Sanguosha.Desktop");
        string assets = Path.Combine(desktop, "Assets", "Official");
        string portraitsJson = File.ReadAllText(Path.Combine(assets, "sources.json"));
        string skinsJson = File.ReadAllText(Path.Combine(assets, "Skins", "sources.json"));
        IReadOnlyList<PortraitEntry> catalog = PortraitCatalog.Read(portraitsJson, skinsJson);
        Check(catalog.Count == Heroes.All.Count * 3, "All 90 selectable heroes resolve three official appearances");
        foreach (Hero hero in Heroes.All)
        {
            PortraitEntry[] choices = catalog.Where(skin => skin.HeroId == hero.Id).ToArray();
            Check(choices.Length == 3 && choices.Count(skin => skin.Id == "classic") == 1,
                $"Portrait catalog resolves {hero.Id}");
            Check(choices.Single(skin => skin.Id == "classic").Asset == $"Portraits/{hero.BaseHeroId ?? hero.Id}.png",
                $"Refreshed general maps to base portrait: {hero.Id}");
        }
        Span<byte> signature = stackalloc byte[8];
        foreach (PortraitEntry skin in catalog)
        {
            string path = Path.Combine(assets, skin.Asset.Replace('/', Path.DirectorySeparatorChar));
            Check(File.Exists(path), $"Portrait source exists: {skin.HeroId}/{skin.Id}");
            using FileStream stream = File.OpenRead(path);
            Check(stream.Read(signature) == 8 && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                $"Official portrait PNG signature: {skin.Asset}");
        }
        IReadOnlyList<PortraitEntry> fallback = PortraitCatalog.Read(null, null);
        Check(fallback.Count == Heroes.All.Count && Heroes.All.All(hero => fallback.Any(skin => skin.HeroId == hero.Id)),
            "Metadata fallback preserves all selectable heroes, including refreshed aliases");
        Check(PortraitCatalog.Read("{\"files\":[{\"id\":\"liubei\",\"appearance\":\"测试形象\"}]}", null)
            .Single(skin => skin.HeroId == "liubei").Name == "测试形象", "Manifest uses id rather than nonexistent file property");
        XDocument project = XDocument.Load(Path.Combine(desktop, "Sanguosha.Desktop.csproj"));
        HashSet<string> includes = project.Descendants("Resource").Select(element => (string?)element.Attribute("Include") ?? "").ToHashSet();
        Check(includes.Contains("Assets/Official/sources.json") && includes.Contains("Assets/Official/Skins/sources.json"),
            "Both manifests are WPF embedded resources (not source-only None items)");
        Check(includes.Contains("Assets/Official/**/*.png"), "Portrait PNGs are embedded, not dependent on working directory");

        // Read the actual build output on Linux without loading any WPF types or Windows runtime.
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string assemblyPath = Path.Combine(desktop, "bin", configuration, "net10.0-windows", "山河弈.dll");
        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine("Portrait source/catalog checks passed; build the solution to inspect embedded build-output resources.");
            return;
        }
        Assembly assembly = Assembly.LoadFile(assemblyPath);
        string resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".g.resources", StringComparison.Ordinal));
        using Stream resources = assembly.GetManifestResourceStream(resourceName)!;
        using ResourceReader reader = new(resources);
        HashSet<string> keys = new(StringComparer.Ordinal);
        IDictionaryEnumerator enumerator = reader.GetEnumerator();
        while (enumerator.MoveNext()) keys.Add((string)enumerator.Key);
        foreach (string manifest in new[] { "assets/official/sources.json", "assets/official/skins/sources.json" })
        {
            Check(keys.Contains(manifest), $"Build output embeds manifest: {manifest}");
            reader.GetResourceData(manifest, out string type, out byte[] data);
            Check(type == "ResourceTypeCode.Stream" && data.Length > 4, $"Manifest stream readable: {manifest}");
            using JsonDocument document = JsonDocument.Parse(data.AsMemory(4)); // Resource stream length prefix.
            Check(document.RootElement.GetProperty("files").GetArrayLength() > 0, $"Embedded manifest JSON valid: {manifest}");
        }
        foreach (string asset in catalog.Select(skin => skin.Asset).Distinct())
        {
            string key = $"assets/official/{asset}".ToLowerInvariant();
            Check(keys.Contains(key), $"Build output embeds portrait: {asset}");
            reader.GetResourceData(key, out string type, out byte[] data);
            Check(type == "ResourceTypeCode.Stream" && data.Length > 12 && data[4] == 137 && data[5] == 80,
                $"Build output portrait is readable PNG stream: {asset}");
        }
        ResourceManager pack = new(resourceName[..^".resources".Length], assembly);
        int workerReads = 0;
        Parallel.ForEach(catalog.Select(skin => skin.Asset).Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = 2 }, asset =>
            {
                using Stream stream = pack.GetStream($"assets/official/{asset}".ToLowerInvariant(), CultureInfo.InvariantCulture)
                    ?? throw new InvalidDataException($"Background portrait stream missing: {asset}");
                if (stream.ReadByte() != 137 || stream.ReadByte() != 80)
                    throw new InvalidDataException($"Background portrait stream invalid: {asset}");
                Interlocked.Increment(ref workerReads);
            });
        byte[] expected = File.ReadAllBytes(Path.Combine(assets, "Portraits", "liubei.png"));
        Parallel.For(0, 20, new ParallelOptions { MaxDegreeOfParallelism = 2 }, _ =>
        {
            using Stream stream = pack.GetStream("assets/official/portraits/liubei.png", CultureInfo.InvariantCulture)!;
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            if (!copy.ToArray().AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("Concurrent shared-portrait streams have incorrect content / positions.");
        });
        Check(true, "Concurrent reads of the same embedded portrait have independent stream positions");
        Check(workerReads == catalog.Select(skin => skin.Asset).Distinct().Count(),
            "All embedded portrait streams can be read through ResourceManager on two decoder workers");
        Console.WriteLine($"Portrait regression: {Heroes.All.Count} heroes, {catalog.Count} appearances, " +
            $"{catalog.Select(skin => skin.Asset).Distinct().Count()} PNGs verified in {configuration} build output.");
    }
}

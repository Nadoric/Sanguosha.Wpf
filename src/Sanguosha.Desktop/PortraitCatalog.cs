using System.IO;
using System.Text.Json;
using Sanguosha.Core;

namespace Sanguosha.Desktop;

// Portable manifest reader, also compiled into Verify: no WPF/runtime dependency.
internal sealed record PortraitEntry(string Id, string HeroId, string Name, string Asset);
internal static class PortraitCatalog
{
    internal static IReadOnlyList<PortraitEntry> Read(string? portraitsJson, string? skinsJson)
    {
        Dictionary<string, string> titles = new(StringComparer.Ordinal);
        if (portraitsJson != null)
        {
            using JsonDocument document = JsonDocument.Parse(portraitsJson);
            foreach (JsonElement entry in document.RootElement.GetProperty("files").EnumerateArray())
            {
                // sources.json uses "id" and "path", NOT "file".
                if (entry.TryGetProperty("id", out JsonElement id) &&
                    entry.TryGetProperty("appearance", out JsonElement title))
                    titles[id.GetString()!] = title.GetString() ?? "经典形象";
            }
        }
        List<PortraitEntry> skins = Heroes.All.Where(hero => hero.BaseHeroId == null)
            .Select(hero => new PortraitEntry("classic", hero.Id,
                titles.GetValueOrDefault(hero.Id, "经典形象"), $"Portraits/{hero.Id}.png")).ToList();
        if (skinsJson != null)
        {
            using JsonDocument document = JsonDocument.Parse(skinsJson);
            foreach (JsonElement entry in document.RootElement.GetProperty("files").EnumerateArray())
            {
                string hero = entry.GetProperty("hero").GetString()!;
                string asset = entry.GetProperty("asset").GetString()!;
                string id = entry.GetProperty("id").GetString()!;
                if (!Heroes.All.Any(item => item.Id == hero && item.BaseHeroId == null) ||
                    !asset.StartsWith("Skins/", StringComparison.Ordinal) || asset.Contains("..") ||
                    asset.Contains('\\') || !asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(id) || skins.Any(skin => skin.HeroId == hero && skin.Id == id))
                    throw new InvalidDataException($"Invalid/duplicate official skin: {hero}/{id}/{asset}");
                skins.Add(new PortraitEntry(id, hero, entry.GetProperty("name").GetString()!, asset));
            }
        }
        foreach (Hero hero in Heroes.All.Where(hero => hero.BaseHeroId != null))
            skins.AddRange(skins.Where(skin => skin.HeroId == hero.BaseHeroId)
                .Select(skin => skin with { HeroId = hero.Id }).ToArray());
        return skins.AsReadOnly();
    }
}

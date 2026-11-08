using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public sealed record HeroSkin(string Id, string HeroId, string Name, string Asset)
    {
        public ImageBrush Portrait => Art.SkinPortrait(this);
    }

    public sealed class SkinLibrary
    {
        private readonly Dictionary<string, string> selections = new();
        private readonly string settingsPath;
        private readonly bool persist;
        private readonly Dictionary<string, IReadOnlyList<HeroSkin>> byHero;

        public static SkinLibrary Shared { get; } = new(persist:
            !Environment.GetCommandLineArgs().Any(argument => argument.StartsWith("--verify-")));

        public SkinLibrary(string? settingsPath = null, bool persist = true)
        {
            this.persist = persist;
            this.settingsPath = Path.GetFullPath(settingsPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShanHeYi", "skins.json"));
            IReadOnlyList<PortraitEntry> entries;
            try
            {
                entries = PortraitCatalog.Read(ReadManifest("sources.json"), ReadManifest("Skins/sources.json"));
            }
            catch (Exception exception) when (exception is IOException or JsonException or
                InvalidOperationException or KeyNotFoundException or NotSupportedException)
            {
                // Metadata damage must never poison Shared's static initializer / all portrait bindings.
                System.Diagnostics.Trace.TraceError($"Official portrait metadata: {exception}");
                entries = PortraitCatalog.Read(null, null);
            }
            All = entries.Select(entry => new HeroSkin(entry.Id, entry.HeroId, entry.Name, entry.Asset)).ToArray();
            byHero = All.GroupBy(skin => skin.HeroId).ToDictionary(group => group.Key,
                group => (IReadOnlyList<HeroSkin>)group.ToArray());
            if (persist)
            {
                LoadPreferences();
            }
        }

        public IReadOnlyList<HeroSkin> All { get; }

        public IReadOnlyList<HeroSkin> ForHero(string heroId)
        {
            return byHero[heroId];
        }

        public HeroSkin Current(string heroId)
        {
            selections.TryGetValue(heroId, out string? id);
            return ForHero(heroId).FirstOrDefault(skin => skin.Id == (id ?? "classic")) ??
                ForHero(heroId).First(skin => skin.Id == "classic");
        }

        public bool Apply(string heroId, string skinId, out string message)
        {
            HeroSkin? skin = All.FirstOrDefault(item => item.HeroId == heroId && item.Id == skinId);
            if (skin == null)
            {
                message = "这款皮肤不属于当前武将。";
                return false;
            }

            selections[heroId] = skinId;
            message = $"已为{Heroes.Find(heroId).Name}更换「{skin.Name}」。";
            if (!persist)
            {
                return true;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                string temporary = settingsPath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(selections));
                File.Move(temporary, settingsPath, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                message += " 本次已生效，但无法保存本机设置。";
            }

            return true;
        }

        private static string? ReadManifest(string path)
        {
            using Stream? stream = Application.GetResourceStream(new Uri(
                $"pack://application:,,,/Assets/Official/{path}"))?.Stream;
            if (stream == null) throw new FileNotFoundException($"Missing embedded official manifest: {path}");
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        private void LoadPreferences()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return;
                }

                Dictionary<string, string>? saved = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(settingsPath));
                foreach (KeyValuePair<string, string> item in saved ?? new())
                {
                    if (All.Any(skin => skin.HeroId == item.Key && skin.Id == item.Value))
                    {
                        selections[item.Key] = item.Value;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // 损坏或过期的外观设置回退经典形象，不影响游戏存档与规则。
            }
        }
    }
}

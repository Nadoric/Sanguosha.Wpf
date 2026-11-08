using System.Collections;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text.Json;
using Sanguosha.Core;

namespace Sanguosha.Verify;

internal static partial class Program
{
    private static GameEngine KillFixture()
    {
        GameEngine game = new(42);
        List<GameCueKind> starts = new(); game.CueRaised += cue => starts.Add(cue.Kind);
        game.Start("liubei", Role.Lord, GameMode.Identity8);
        for (int step = 0; !game.CanSave && step < 1000; step++)
        {
            if (game.PendingDecision is { } decision) game.ChooseSkillDecision(decision.Options[0].Key);
            else if (game.Pending != null) game.Respond(null);
            else game.Advance();
        }
        Check(game.CanSave, "Kill fixture starts in the human play phase");
        Check(starts.Count(kind => kind == GameCueKind.MatchStarted) == 1 && starts.IndexOf(GameCueKind.MatchStarted) < starts.IndexOf(GameCueKind.TurnStarted),
            "New match has exactly one start effect cue after selection and before first turn");
        return game;
    }

    private static void VerifyKillEffects()
    {
        VerifyKillEffectResources();
        GameEngine game = KillFixture();
        List<GameCue> cues = new(); game.CueRaised += cues.Add;
        Player[] victims = game.Players.Where(player => player.Seat != 0)
            .OrderBy(player => player.Role == Role.Rebel ? 1 : 0).ToArray(); // keep a rebel until final kill
        Player first = victims[0]; first.Health = 0;
        Call(game, "Eliminate", game.Human, first);
        GameCue death = cues.Single(cue => cue.Kind == GameCueKind.Eliminated);
        Check(death.Seat == first.Seat && death.SourceSeat == 0 && death.HeroId == first.Hero.Id,
            "Elimination retains victim and killer attribution without parsing text logs");
        Check(death.Elimination is { MatchKills: 1, TurnKills: 1, Tier: 1, Title: "一骑当先" }, "First kill uses official Ten-Year tier");
        Call(game, "Eliminate", game.Human, first);
        Check(cues.Count(cue => cue.Kind == GameCueKind.Eliminated) == 1 && game.MatchKills(0) == 1, "Duplicate death cannot replay effect or award another kill");
        CheckAllCards(game);
        string saved = game.Save();
        GameEngine loaded = GameEngine.Load(saved);
        Check(loaded.MatchKills(0) == 1 && loaded.TurnKills(0) == 1, "Both counters persist across save/load without replaying death cues");
        CheckAllCards(loaded);
        SavedGame snapshot = JsonSerializer.Deserialize<SavedGame>(saved)!;
        GameEngine legacy = Restore(snapshot with { MatchKills = null, TurnKills = null });
        Check(legacy.MatchKills(0) == 0 && legacy.TurnKills(0) == 0, "Older saves remain readable with unknown historic kills starting at zero");
        bool rejected = false;
        try { Restore(snapshot with { MatchKills = new() { [0] = 9 } }); } catch (JsonException) { rejected = true; }
        Check(rejected, "Corrupt/impossible kill counters rejected");
        rejected = false;
        try { Restore(snapshot with { TurnKills = new() { [0] = 2 } }); } catch (JsonException) { rejected = true; }
        Check(rejected, "Turn kills cannot exceed personal match kills");

        // BeginTurn is shared by ordinary and extra turns. Turn count resets, match tier does not.
        Call(game, "BeginTurn");
        Check(game.MatchKills(0) == 1 && game.TurnKills(0) == 0, "A new turn resets only the per-turn counter");
        for (int i = 1; i < victims.Length; i++)
        {
            victims[i].Health = 0;
            Call(game, "Eliminate", game.Human, victims[i]);
            EliminationInfo info = cues.Last(cue => cue.Kind == GameCueKind.Eliminated).Elimination!;
            Check(info.MatchKills == i + 1 && info.TurnKills == i && info.Tier == i + 1, $"Cumulative official kill tier {i + 1} does not reset at turn boundary");
            CheckAllCards(game);
        }
        Check(game.Phase == GamePhase.GameOver && cues[^2].Kind == GameCueKind.Eliminated && cues[^1].Kind == GameCueKind.MatchEnded,
            "Final kill is emitted before the result event, exactly once");
        string[] titles = { "一骑当先", "世无双", "冠三军", "镇诸侯", "天下无敌", "统九州乾坤", "千古第一人" };
        for (int i = 1; i <= 7; i++) Check(new EliminationInfo(0, 1, i, i).Title == titles[i - 1], "Titles match official texture/voice sequence");
        Check(new EliminationInfo(null, 1, 0, 0).Tier == 0, "No-source death has a separate uncredited variant");
        Check(new EliminationInfo(0, 1, 25, 25).Tier == 7, "Tier saturates safely above seven for future larger modes");
        game.Start("liubei", Role.Lord, GameMode.Identity8);
        Check(game.Players.All(player => game.MatchKills(player.Seat) == 0 && game.TurnKills(player.Seat) == 0), "New match clears all old kill statistics");

        foreach (bool self in new[] { false, true })
        {
            game = KillFixture(); cues.Clear(); game.CueRaised += cues.Add;
            Player victim = game.Players.First(player => player.Role == Role.Loyalist); victim.Health = 0;
            Call(game, "Eliminate", self ? victim : null, victim);
            Check(cues.Single(cue => cue.Kind == GameCueKind.Eliminated).Elimination is { KillerSeat: null, MatchKills: 0, Tier: 0 },
                "Self-inflicted / sourceless death never credits a killer");
            Check(game.Players.All(player => game.MatchKills(player.Seat) == 0), "Uncredited death does not increment any seat");
        }
        game = KillFixture(); cues.Clear(); game.CueRaised += cues.Add;
        Player ai = game.Players.First(player => player.Role == Role.Rebel);
        Player target = game.Players.First(player => player.Role == Role.Loyalist); target.Health = 0;
        Call(game, "Eliminate", ai, target);
        Check(cues.Single(cue => cue.Kind == GameCueKind.Eliminated).Elimination?.KillerSeat == ai.Seat && game.MatchKills(ai.Seat) == 1,
            "AI kills use the identical event/counting path");
        // A source may already have died while its queued/chained damage is still resolving.
        ai.Health = 0; Call(game, "Eliminate", null, ai);
        target = game.Players.First(player => player.IsAlive && player != game.Human && player != ai); target.Health = 0;
        Call(game, "Eliminate", ai, target);
        Check(game.MatchKills(ai.Seat) == 2, "Already-dead attributed source retains credit for deferred damage");
        Console.WriteLine("Kill effects: official 7-tier assets, source/self/AI/deferred/final deaths, duplicate suppression, turn/reset/save compatibility verified.");
    }

    private static void VerifyKillEffectResources()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Sanguosha.slnx"))) root = root.Parent;
        Check(root != null, "Locate official kill effect sources");
        string desktop = Path.Combine(root!.FullName, "src/Sanguosha.Desktop");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(desktop, "Assets/Official/KillEffects/sources.json")));
        Check(manifest.RootElement.GetProperty("files").GetArrayLength() >= 21 && manifest.RootElement.GetProperty("derived").GetArrayLength() == 7,
            "Seven original texture pages, atlases, announcements and seven pixel-preserving crops registered");
        foreach (JsonElement item in manifest.RootElement.GetProperty("files").EnumerateArray().Concat(manifest.RootElement.GetProperty("derived").EnumerateArray()))
        {
            string path = Path.Combine(root.FullName, item.GetProperty("path").GetString()!);
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes.Length == item.GetProperty("bytes").GetInt64() && Convert.ToHexString(SHA256.HashData(bytes)).Equals(item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase),
                $"Official/derived kill resource hash valid: {path}");
            if (item.TryGetProperty("url", out JsonElement url)) Check(new Uri(url.GetString()!).Host == "web.sanguosha.com", "Kill resource provenance is the official host");
        }
        using JsonDocument opening = JsonDocument.Parse(File.ReadAllText(Path.Combine(desktop, "Assets/Official/GameStart/sources.json")));
        Check(opening.RootElement.GetProperty("files").GetArrayLength() == 3 && opening.RootElement.GetProperty("derived").GetArrayLength() == 4,
            "Opening effect includes original PNG/skeleton/SFX and four documented crops");
        foreach (JsonElement item in opening.RootElement.GetProperty("files").EnumerateArray().Concat(opening.RootElement.GetProperty("derived").EnumerateArray()))
        {
            byte[] data = File.ReadAllBytes(Path.Combine(root.FullName, item.GetProperty("path").GetString()!));
            Check(data.Length == item.GetProperty("bytes").GetInt64() && Convert.ToHexString(SHA256.HashData(data)).Equals(item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase),
                "Original / cropped opening resource passes SHA-256");
            if (item.TryGetProperty("url", out JsonElement url)) Check(new Uri(url.GetString()!).Host == "web.sanguosha.com", "Opening resource uses official host");
        }
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string output = Path.Combine(desktop, "bin", configuration, "net10.0-windows");
        string dll = Path.Combine(output, "山河弈.dll");
        if (!File.Exists(dll)) return;
        Assembly assembly = Assembly.LoadFile(dll);
        string resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".g.resources", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using ResourceReader reader = new(stream);
        HashSet<string> keys = new(); IDictionaryEnumerator enumerator = reader.GetEnumerator();
        while (enumerator.MoveNext()) keys.Add((string)enumerator.Key);
        using JsonDocument audio = JsonDocument.Parse(File.ReadAllText(Path.Combine(desktop, "Assets/Official/Audio/catalog.json")));
        foreach (string name in new[] { "title", "blade-left", "blade-right", "glow" })
            Check(keys.Contains($"assets/official/gamestart/sprites/{name}.png"), "Official opening crop embedded in WPF output");
        string startClip = audio.RootElement.GetProperty("effects").GetProperty("start").GetString()!;
        Check(startClip == "Effects/SF_kaizhan_eff_putong.mp3" && File.Exists(Path.Combine(output, "Assets/Official/Audio", startClip)),
            "The actual output contains the official opening sound, not the previous generic sample");
        for (int i = 1; i <= 7; i++)
        {
            Check(keys.Contains($"assets/official/killeffects/titles/{i}.png"), "Official title crop embedded in actual WPF output");
            string clip = audio.RootElement.GetProperty("effects").GetProperty($"kill:{i}").GetString()!;
            Check(File.Exists(Path.Combine(output, "Assets/Official/Audio", clip)), "Official announcement copied into actual build output");
        }
    }
}

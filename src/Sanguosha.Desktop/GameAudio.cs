using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public sealed class AudioPreferences
    {
        public bool Muted { get; set; }
        public bool EffectsEnabled { get; set; } = true;
        public bool VoicesEnabled { get; set; } = true;
        public double EffectsVolume { get; set; } = 0.55;
        public double VoicesVolume { get; set; } = 0.8;
    }

    public sealed class AudioCatalog
    {
        public Dictionary<string, string> Effects { get; set; } = new();
        public Dictionary<string, string[]> CardVoices { get; set; } = new();
        public Dictionary<string, string[]> MissingVoices { get; set; } = new();
        public Dictionary<string, string> MissingCardVoices { get; set; } = new();
        public Dictionary<string, Dictionary<string, string[]>> Voices { get; set; } = new();
    }

    public sealed record EliminationPresentation(GameCue Cue, double Seconds, bool HasSound);

    public sealed partial class GameAudio : IDisposable
    {
        private sealed record VoiceClip(string File, string Key, DateTimeOffset QueuedAt, bool IsDeath, bool IsAnnouncement = false, GameCue? Cue = null)
        {
            public bool Presented { get; set; }
        }

        private readonly string audioRoot = Path.Combine(
            AppContext.BaseDirectory, "Assets", "Official", "Audio");
        private readonly string settingsPath;
        private readonly bool silent;
        private readonly bool persist;
        private readonly bool muteOutputForVerification;
        private readonly AudioCatalog catalog;
        private readonly List<MediaPlayer> effectPlayers = new();
        private readonly List<VoiceClip> pending = new();
        private readonly Dictionary<string, DateTimeOffset> cooldowns = new();
        private readonly HashSet<string> eliminated = new();
        private readonly DispatcherTimer saveTimer;
        private readonly DispatcherTimer voiceWatchdog;
        private MediaPlayer? voicePlayer;
        private VoiceClip? currentVoice;
        private DateTimeOffset voiceDeadline;
        private int effectIndex;
        private bool settingsDirty;
        private bool disposed;
        private bool killEffectsEnabled = true;
        private string? pendingMatchEffect;
        private MediaPlayer? startEffectPlayer;

        public bool KillEffectsEnabled
        {
            get => killEffectsEnabled;
            set { killEffectsEnabled = value; if (!value) { StopKillAnnouncements(); StopStartEffect(); } }
        }

        public GameAudio(
            bool silent = false,
            string? settingsPath = null,
            bool muteOutputForVerification = false)
        {
            this.silent = silent;
            this.muteOutputForVerification = muteOutputForVerification;
            persist = !silent;
            this.settingsPath = settingsPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShanHeYi", "audio.json");
            Settings = ReadJson(this.settingsPath, new AudioPreferences());
            Settings.EffectsVolume = Clamp(Settings.EffectsVolume, 0.55);
            Settings.VoicesVolume = Clamp(Settings.VoicesVolume, 0.8);
            catalog = ReadJson(Path.Combine(audioRoot, "catalog.json"), new AudioCatalog());
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            saveTimer.Tick += (_, _) => SavePreferences();
            voiceWatchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            voiceWatchdog.Tick += (_, _) =>
            {
                if (DateTimeOffset.UtcNow > voiceDeadline)
                {
                    FinishVoice();
                }
            };
            for (int index = 0; index < 4; index++)
            {
                MediaPlayer player = new();
                player.MediaFailed += (_, _) => ReportFailure("音效暂时无法播放，请检查系统音频设备。");
                effectPlayers.Add(player);
            }
        }

        public event Action? StatusChanged;
        public event Action<EliminationPresentation>? EliminationReady;
        public AudioPreferences Settings { get; }
        public string LastError { get; private set; } = "";
        public string Status => LastError.Length > 0 ? LastError :
            Settings.Muted ? "全部声音已静音" : $"本地卡牌报音 · {Heroes.All.Count}名武将技能与阵亡语音";
        internal int PendingVoiceCount => pending.Count;
        internal string? ActiveVoice => currentVoice?.Key;
        internal int EffectPlayCount { get; private set; }
        internal int VoicePlayCount { get; private set; }
        public AudioCatalog Catalog => catalog;
        public string Root => audioRoot;

        private static T ReadJson<T>(string path, T fallback)
        {
            try
            {
                if (File.Exists(path))
                {
                    return JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? fallback;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // 缺失或损坏的偏好使用默认值；不阻止对局启动。
            }

            return fallback;
        }

        private static double Clamp(double value, double fallback)
        {
            return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : fallback;
        }

        public void ApplySettings()
        {
            Settings.EffectsVolume = Clamp(Settings.EffectsVolume, 0.55);
            Settings.VoicesVolume = Clamp(Settings.VoicesVolume, 0.8);
            foreach (MediaPlayer player in effectPlayers)
            {
                player.Volume = muteOutputForVerification ? 0 : Settings.EffectsVolume;
                if (Settings.Muted || !Settings.EffectsEnabled || Settings.EffectsVolume == 0)
                {
                    player.Close();
                }
            }

            pending.RemoveAll(clip => !CanPlayVoice(clip));
            if (currentVoice != null && (!CanPlayVoice(currentVoice) || currentVoice.IsAnnouncement && !CanSoundAnnouncement(currentVoice)))
            {
                VoiceClip clip = currentVoice;
                if (clip.IsAnnouncement && killEffectsEnabled) PublishPresentation(clip, false);
                if (ReferenceEquals(currentVoice, clip)) StopCurrentVoice();
            }
            if (voicePlayer != null && currentVoice != null)
                voicePlayer.Volume = muteOutputForVerification ? 0 : VoiceVolume(currentVoice);
            PlayNextVoice();
            PlayDeferredMatchEffect();

            ApplyCardAudioSettings();

            settingsDirty = true;
            saveTimer.Stop();
            if (persist)
            {
                saveTimer.Start();
            }

            StatusChanged?.Invoke();
        }

        private void SavePreferences()
        {
            saveTimer.Stop();
            if (!persist || !settingsDirty)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(Settings));
                settingsDirty = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ReportFailure("本次音量设置已生效，但无法保存到本机。");
            }
        }

        public void Handle(GameCue cue)
        {
            if (disposed)
            {
                return;
            }

            switch (cue.Kind)
            {
                case GameCueKind.MatchStarted:
                    StopAll();
                    eliminated.Clear();
                    cooldowns.Clear();
                    if (killEffectsEnabled) PlayEffect("start");
                    break;
                case GameCueKind.CardPlayed:
                case GameCueKind.CardResponded:
                    PlayEffect(EffectFor(cue.Card));
                    if (currentVoice?.IsDeath != true && !pending.Any(clip => clip.IsDeath)) AnnounceCard(cue);
                    break;
                case GameCueKind.SkillActivated:
                    if (cue.HeroId != null && cue.Skill != null && !eliminated.Contains(cue.HeroId))
                    {
                        Speak(cue.HeroId, cue.Skill);
                    }

                    break;
                case GameCueKind.Damaged:
                    PlayEffect("hurt");
                    break;
                case GameCueKind.Healed:
                    PlayEffect("peach");
                    break;
                case GameCueKind.NearDeath:
                    PlayEffect("dying");
                    break;
                case GameCueKind.Eliminated:
                    if (cue.HeroId != null && !eliminated.Add(cue.HeroId)) break;
                    StopCardVoices();
                    StopStartEffect();
                    PlayEffect("death");
                    if (cue.HeroId != null) Speak(cue.HeroId, "death", true);
                    QueueEliminationPresentation(cue);
                    break;
                case GameCueKind.MatchEnded:
                    pending.RemoveAll(clip => !clip.IsDeath);
                    if (currentVoice != null && !currentVoice.IsDeath) StopCurrentVoice();
                    pendingMatchEffect = cue.HumanWon ? "win" : "lose";
                    PlayNextVoice();
                    PlayDeferredMatchEffect();
                    break;
                case GameCueKind.ArmorJudgment:
                    PlayEffect("bagua");
                    break;
            }
        }

        private static string EffectFor(CardKind? card)
        {
            return card switch
            {
                CardKind.Slash => "slash",
                CardKind.Dodge => "dodge",
                CardKind.Peach => "peach",
                CardKind.Duel => "duel",
                CardKind.DrawTwo => "drawtwo",
                CardKind.Barbarians => "barbarians",
                CardKind.Feast => "feast",
                CardKind.Crossbow or CardKind.Blade => "weapon",
                CardKind.EightTrigrams => "armor",
                CardKind.Horse => "horse",
                _ => "card"
            };
        }

        public void PlayEffect(string key)
        {
            if (disposed || Settings.Muted || !Settings.EffectsEnabled || Settings.EffectsVolume == 0 ||
                !catalog.Effects.TryGetValue(key, out string? file))
            {
                return;
            }

            string throttleKey = $"effect:{key}";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (cooldowns.TryGetValue(throttleKey, out DateTimeOffset previous) &&
                now - previous < TimeSpan.FromMilliseconds(90))
            {
                return;
            }

            cooldowns[throttleKey] = now;
            string path = Path.Combine(audioRoot, file);
            if (!File.Exists(path))
            {
                ReportFailure("部分本地音效文件缺失，可从完整压缩包恢复。");
                return;
            }

            EffectPlayCount++;
            if (silent)
            {
                return;
            }

            MediaPlayer player = effectPlayers[effectIndex++ % effectPlayers.Count];
            if (ReferenceEquals(player, startEffectPlayer)) startEffectPlayer = null;
            if (key == "start") startEffectPlayer = player;
            try
            {
                player.Close();
                player.Volume = muteOutputForVerification ? 0 : Settings.EffectsVolume;
                player.Open(new Uri(path));
                player.Play();
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or NotSupportedException)
            {
                ReportFailure("音效暂时无法播放，请检查系统音频设备。");
            }
        }

        public void PreviewVoice(string heroId)
        {
            StopAll();
            if (catalog.Voices.TryGetValue(heroId, out Dictionary<string, string[]>? skills))
            {
                string? skill = skills.Keys.FirstOrDefault(name => name != "death");
                if (skill != null)
                {
                    cooldowns.Remove($"{heroId}:{skill}");
                    Speak(heroId, skill);
                }
            }
        }

        private void Speak(string heroId, string skill, bool death = false)
        {
            if (Settings.Muted || !Settings.VoicesEnabled || Settings.VoicesVolume == 0 ||
                !catalog.Voices.TryGetValue(heroId, out Dictionary<string, string[]>? skills) ||
                !skills.TryGetValue(skill, out string[]? files) || files.Length == 0)
            {
                return;
            }

            string key = $"{heroId}:{skill}";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (cooldowns.TryGetValue(key, out DateTimeOffset previous) &&
                now - previous < TimeSpan.FromSeconds(1.5))
            {
                return;
            }

            cooldowns[key] = now;
            VoiceClip clip = new(files[Random.Shared.Next(files.Length)], key, now, death);
            if (death)
            {
                QueuePriority(clip);
                return;
            }
            if (currentVoice?.IsDeath == true || pending.Any(item => item.IsDeath)) return;

            // 单一语音声道最多保留两个新近请求，防止快速AI回合堆积过期台词。
            pending.RemoveAll(item => item.Key == key || now - item.QueuedAt > TimeSpan.FromSeconds(4));
            if (pending.Count >= 2)
            {
                pending.RemoveAt(0);
            }

            pending.Add(clip);
            PlayNextVoice();
        }

        private bool CanPlayVoice(VoiceClip clip) => clip.IsAnnouncement ? killEffectsEnabled :
            !Settings.Muted && Settings.VoicesEnabled && Settings.VoicesVolume > 0;
        private bool CanSoundAnnouncement(VoiceClip clip) => clip.File.Length > 0 && !Settings.Muted &&
            Settings.EffectsEnabled && Settings.EffectsVolume > 0;

        private void PublishPresentation(VoiceClip clip, bool sound, double? seconds = null)
        {
            if (clip.Cue == null || clip.Presented || disposed) return;
            clip.Presented = true;
            double[] durations = { 2.45, 2.832, 2.712, 2.880, 3.504, 3.384, 4.416, 4.320 };
            int tier = clip.Cue.Elimination?.Tier ?? 0;
            EliminationReady?.Invoke(new(clip.Cue, Math.Clamp(seconds ?? durations[Math.Clamp(tier, 0, 7)], 1.5, 6), sound));
        }

        private double VoiceVolume(VoiceClip clip) => clip.IsAnnouncement ? Settings.EffectsVolume : Settings.VoicesVolume;

        private void QueuePriority(VoiceClip clip)
        {
            // Seven victims => at most fourteen death/announcement clips in an eight-seat game.
            // Reserve these FIFO entries: later deaths must not interrupt earlier victims.
            pending.RemoveAll(item => !item.IsDeath);
            if (currentVoice != null && !currentVoice.IsDeath) StopCurrentVoice();
            if (pending.Count < 16) pending.Add(clip);
            PlayNextVoice();
        }

        private void QueueEliminationPresentation(GameCue cue)
        {
            if (!killEffectsEnabled || cue.Seat == null || cue.Elimination == null) return;
            int tier = cue.Elimination.Tier;
            string key = $"kill:{tier}";
            string file = tier > 0 && catalog.Effects.TryGetValue(key, out string? found) ? found : "";
            // A silent marker still releases the matching visual after its victim's death voice.
            QueuePriority(new VoiceClip(file, key, DateTimeOffset.UtcNow, true, true, cue));
        }

        public void StopKillAnnouncements()
        {
            pending.RemoveAll(clip => clip.IsAnnouncement);
            if (currentVoice?.IsAnnouncement == true) StopCurrentVoice();
            PlayNextVoice();
            PlayDeferredMatchEffect();
        }

        private void PlayDeferredMatchEffect()
        {
            if (pendingMatchEffect == null || currentVoice?.IsDeath == true || pending.Any(clip => clip.IsDeath)) return;
            string effect = pendingMatchEffect;
            pendingMatchEffect = null;
            PlayEffect(effect);
        }

        private void PlayNextVoice()
        {
            if (currentVoice != null || pending.Count == 0 || disposed)
            {
                return;
            }

            VoiceClip clip = pending[0];
            pending.RemoveAt(0);
            if (!CanPlayVoice(clip) || !clip.IsDeath && DateTimeOffset.UtcNow - clip.QueuedAt > TimeSpan.FromSeconds(4))
            {
                PlayNextVoice();
                return;
            }

            if (clip.IsAnnouncement && !CanSoundAnnouncement(clip))
            {
                PublishPresentation(clip, false);
                PlayNextVoice();
                return;
            }

            string path = Path.Combine(audioRoot, clip.File);
            if (!File.Exists(path))
            {
                if (clip.IsAnnouncement) PublishPresentation(clip, false);
                ReportFailure("部分武将语音文件缺失，可从完整压缩包恢复。");
                PlayNextVoice();
                return;
            }

            currentVoice = clip;
            VoicePlayCount++;
            if (silent)
            {
                if (clip.IsAnnouncement) PublishPresentation(clip, true);
                return;
            }

            MediaPlayer player = new();
            voicePlayer = player;
            player.MediaOpened += (_, _) =>
            {
                if (!ReferenceEquals(player, voicePlayer)) return;
                if (clip.IsAnnouncement) PublishPresentation(clip, true, player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan.TotalSeconds : null);
                if (ReferenceEquals(player, voicePlayer)) player.Play();
            };
            player.MediaEnded += (_, _) =>
            {
                if (ReferenceEquals(player, voicePlayer))
                {
                    FinishVoice();
                }
            };
            player.MediaFailed += (_, _) =>
            {
                if (ReferenceEquals(player, voicePlayer))
                {
                    ReportFailure("武将语音暂时无法播放，请检查系统音频设备。");
                    FinishVoice();
                }
            };
            voiceDeadline = DateTimeOffset.UtcNow.AddSeconds(12);
            voiceWatchdog.Start();
            try
            {
                player.Volume = muteOutputForVerification ? 0 : VoiceVolume(clip);
                player.Open(new Uri(path));
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or NotSupportedException)
            {
                ReportFailure("武将语音暂时无法播放，请检查系统音频设备。");
                FinishVoice();
            }
        }

        private void ReportFailure(string message)
        {
            LastError = message;
            StatusChanged?.Invoke();
        }

        private void StopCurrentVoice()
        {
            voiceWatchdog.Stop();
            MediaPlayer? previous = voicePlayer;
            voicePlayer = null;
            currentVoice = null;
            previous?.Close();
        }

        private void FinishVoice()
        {
            if (currentVoice?.IsAnnouncement == true) PublishPresentation(currentVoice, false);
            StopCurrentVoice();
            PlayNextVoice();
            PlayDeferredMatchEffect();
        }

        internal void FinishVoiceForVerification()
        {
            if (!silent)
            {
                throw new InvalidOperationException("Only silent verification can advance a voice manually.");
            }

            FinishVoice();
        }

        private void StopVoices()
        {
            pending.Clear();
            StopCurrentVoice();
        }

        public void StopStartEffect()
        {
            startEffectPlayer?.Close();
            startEffectPlayer = null;
        }

        public void StopAll()
        {
            startEffectPlayer = null;
            pendingMatchEffect = null;
            StopVoices();
            StopCardVoices();
            foreach (MediaPlayer player in effectPlayers)
            {
                player.Close();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            StopAll();
            SavePreferences();
            disposed = true;
        }
    }
}

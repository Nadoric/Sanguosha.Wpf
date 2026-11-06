using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        private static void VerifyAudioControls()
        {
            using GameAudio test = new(silent: true);
            test.Settings.Muted = false;
            test.Settings.EffectsEnabled = true;
            test.Settings.VoicesEnabled = true;
            test.Settings.EffectsVolume = 0.5;
            test.Settings.VoicesVolume = 0.7;
            test.ApplySettings();
            foreach (Hero hero in Heroes.All)
            {
                if (!test.Catalog.Voices.TryGetValue(hero.Id, out Dictionary<string, string[]>? skills) ||
                    !skills.ContainsKey("death") || skills.Count < 2)
                {
                    throw new InvalidOperationException($"Missing skill/death voice: {hero.Name}");
                }
            }

            test.Handle(new GameCue(GameCueKind.SkillActivated, "guanyu", Skill: "武圣"));
            test.Handle(new GameCue(GameCueKind.SkillActivated, "guanyu", Skill: "武圣"));
            if (test.VoicePlayCount != 1 || test.PendingVoiceCount != 0)
            {
                throw new InvalidOperationException("Repeated skill voice was not coalesced.");
            }

            test.Handle(new GameCue(GameCueKind.SkillActivated, "caocao", Skill: "奸雄"));
            test.Handle(new GameCue(GameCueKind.SkillActivated, "zhangfei", Skill: "咆哮"));
            test.Handle(new GameCue(GameCueKind.SkillActivated, "zhaoyun", Skill: "龙胆"));
            if (test.PendingVoiceCount != 2)
            {
                throw new InvalidOperationException("Voice queue is not bounded to two pending clips.");
            }

            test.FinishVoiceForVerification();
            if (test.ActiveVoice != "zhangfei:咆哮")
            {
                throw new InvalidOperationException("Old voice requests were not dropped in order.");
            }

            test.Handle(new GameCue(GameCueKind.Eliminated, "huanggai"));
            if (test.ActiveVoice != "huanggai:death" || test.PendingVoiceCount != 0)
            {
                throw new InvalidOperationException("Death voice did not supersede pending skill voices.");
            }

            test.Settings.Muted = true;
            test.ApplySettings();
            int effects = test.EffectPlayCount;
            test.Handle(new GameCue(GameCueKind.CardPlayed, "liubei", CardKind.Slash));
            if (test.ActiveVoice != null || test.PendingVoiceCount != 0 || test.EffectPlayCount != effects)
            {
                throw new InvalidOperationException("Mute did not stop active and pending audio.");
            }

            test.Settings.Muted = false;
            test.ApplySettings();
            if (test.ActiveVoice != null)
            {
                throw new InvalidOperationException("Unmute replayed stale audio.");
            }
        }

        internal void RunAudioVerification(string directory)
        {
            Directory.CreateDirectory(directory);
            timer.Stop();
            VerifyAudioControls();
            string preferencesPath = Path.Combine(directory, "preferences.json");
            using (GameAudio preferences = new(settingsPath: preferencesPath))
            {
                preferences.Settings.Muted = true;
                preferences.Settings.EffectsVolume = 0.31;
                preferences.Settings.VoicesVolume = 0.74;
                preferences.ApplySettings();
            }

            using (GameAudio restored = new(silent: true, settingsPath: preferencesPath))
            {
                if (!restored.Settings.Muted || restored.Settings.EffectsVolume != 0.31 ||
                    restored.Settings.VoicesVolume != 0.74)
                {
                    throw new InvalidOperationException("Audio preferences did not survive reload.");
                }
            }

            VerifyNativeVoicePlayback(directory);

            List<string> files = audio.Catalog.Effects.Values
                .Concat(audio.Catalog.Voices.Values.SelectMany(skills => skills.Values.SelectMany(items => items)))
                .Distinct().Order().ToList();
            List<object> decoded = new();
            foreach (string file in files)
            {
                string path = Path.Combine(audio.Root, file);
                MediaPlayer player = new();
                DispatcherFrame frame = new();
                Exception? failure = null;
                double duration = 0;
                DispatcherTimer timeout = new() { Interval = TimeSpan.FromSeconds(6) };
                timeout.Tick += (_, _) =>
                {
                    failure = new TimeoutException($"Audio decoder timed out: {file}");
                    frame.Continue = false;
                };
                player.MediaOpened += (_, _) =>
                {
                    if (!player.HasAudio || !player.NaturalDuration.HasTimeSpan ||
                        player.NaturalDuration.TimeSpan.TotalSeconds <= 0)
                    {
                        failure = new InvalidOperationException($"No valid audio track: {file}");
                    }
                    else
                    {
                        duration = player.NaturalDuration.TimeSpan.TotalSeconds;
                    }

                    frame.Continue = false;
                };
                player.MediaFailed += (_, args) =>
                {
                    failure = new InvalidOperationException($"Cannot decode {file}", args.ErrorException);
                    frame.Continue = false;
                };
                try
                {
                    // 只以零音量打开并读取媒体轨道，不向用户扬声器播放测试台词。
                    player.Volume = 0;
                    timeout.Start();
                    player.Open(new Uri(path));
                    Dispatcher.PushFrame(frame);
                    if (failure != null)
                    {
                        throw failure;
                    }

                    decoded.Add(new { file, seconds = duration });
                }
                finally
                {
                    timeout.Stop();
                    player.Close();
                }
            }

            if (files.Count < 100)
            {
                throw new InvalidOperationException("Audio catalog is incomplete.");
            }

            File.WriteAllText(Path.Combine(directory, "decoded.json"),
                JsonSerializer.Serialize(decoded, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(directory, "result.txt"),
                $"PASS: {files.Count} local MP3 files opened with audio tracks and positive durations; " +
                "25 hero skill/death mappings; bounded queue, deduplication, death priority, mute and preference reload verified. " +
                "Native MediaPlayer voice playback/MediaEnded sequencing and immediate mute also passed with output volume zero. " +
                "Verification was silent, not a listening test.");
        }

        private static void VerifyNativeVoicePlayback(string directory)
        {
            using GameAudio playback = new(
                settingsPath: Path.Combine(directory, "playback-preferences.json"),
                muteOutputForVerification: true);
            playback.Settings.Muted = false;
            playback.Settings.VoicesEnabled = true;
            playback.Settings.VoicesVolume = 0.7;
            playback.ApplySettings();
            playback.Handle(new GameCue(GameCueKind.SkillActivated, "guanyu", Skill: "武圣"));
            playback.Handle(new GameCue(GameCueKind.SkillActivated, "caocao", Skill: "奸雄"));
            DispatcherFrame frame = new();
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(25) };
            bool completed = false;
            poll.Tick += (_, _) =>
            {
                completed = playback.VoicePlayCount == 2 && playback.ActiveVoice == null &&
                    playback.PendingVoiceCount == 0;
                if (completed || playback.LastError.Length > 0 || DateTimeOffset.UtcNow > deadline)
                {
                    frame.Continue = false;
                }
            };
            try
            {
                poll.Start();
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                poll.Stop();
            }

            if (!completed || playback.LastError.Length > 0)
            {
                throw new InvalidOperationException($"Native voice sequencing failed: {playback.LastError}");
            }

            playback.PreviewVoice("zhangfei");
            playback.Settings.Muted = true;
            playback.ApplySettings();
            if (playback.ActiveVoice != null || playback.PendingVoiceCount != 0)
            {
                throw new InvalidOperationException("Native voice was not cancelled immediately by mute.");
            }
        }
    }
}

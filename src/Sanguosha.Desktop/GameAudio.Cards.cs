using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public sealed partial class GameAudio
    {
        private readonly Queue<string> cardVoices = new();
        private MediaPlayer? cardPlayer;
        private DispatcherTimer? cardTimeout;
        internal string? ActiveCardVoice { get; private set; }
        internal int CardVoicePlayCount { get; private set; }
        internal int PendingCardVoiceCount => cardVoices.Count;

        private void AnnounceCard(GameCue cue)
        {
            if (Settings.Muted || !Settings.EffectsEnabled || Settings.EffectsVolume == 0 ||
                cue.Card == null || !catalog.CardVoices.TryGetValue(cue.Card.ToString()!, out string[]? files) ||
                files.Length == 0)
            {
                return;
            }

            bool female = Heroes.All.FirstOrDefault(hero => hero.Id == cue.HeroId)?.IsFemale == true;
            string file = files[female && files.Length > 1 ? 1 : 0];
            // 报音单独排队，避免杀与紧随其后的闪互相覆盖；最多保留四个待播请求。
            if (cardVoices.Count >= 4)
            {
                cardVoices.Dequeue();
            }

            cardVoices.Enqueue(file);
            PlayNextCardVoice();
        }

        private void PlayNextCardVoice()
        {
            if (ActiveCardVoice != null || cardVoices.Count == 0 || disposed)
            {
                return;
            }

            string file = cardVoices.Dequeue();
            string path = Path.GetFullPath(Path.Combine(audioRoot, file));
            if (!File.Exists(path))
            {
                ReportFailure("卡牌报音文件缺失，请恢复完整资源包。");
                PlayNextCardVoice();
                return;
            }

            ActiveCardVoice = file;
            CardVoicePlayCount++;
            if (silent)
            {
                return;
            }

            MediaPlayer player = new();
            cardPlayer = player;
            cardTimeout ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            cardTimeout.Stop();
            cardTimeout.Tick -= CardVoiceTimeout;
            cardTimeout.Tick += CardVoiceTimeout;
            player.MediaEnded += (_, _) =>
            {
                if (ReferenceEquals(cardPlayer, player))
                {
                    FinishCardVoice();
                }
            };
            player.MediaFailed += (_, _) =>
            {
                if (ReferenceEquals(cardPlayer, player))
                {
                    ReportFailure("卡牌报音无法播放，请检查系统音频设备。");
                    FinishCardVoice();
                }
            };
            try
            {
                player.Volume = muteOutputForVerification ? 0 : Settings.EffectsVolume;
                player.Open(new Uri(path));
                player.Play();
                cardTimeout.Start();
            }
            catch (Exception exception) when (exception is InvalidOperationException or
                IOException or NotSupportedException)
            {
                ReportFailure("卡牌报音无法播放，请检查系统音频设备。");
                FinishCardVoice();
            }
        }

        private void CardVoiceTimeout(object? sender, EventArgs e)
        {
            FinishCardVoice();
        }

        internal void FinishCardVoice()
        {
            cardTimeout?.Stop();
            MediaPlayer? previous = cardPlayer;
            cardPlayer = null;
            ActiveCardVoice = null;
            previous?.Close();
            PlayNextCardVoice();
        }

        private void StopCardVoices()
        {
            cardVoices.Clear();
            FinishCardVoice();
        }

        private void ApplyCardAudioSettings()
        {
            if (Settings.Muted || !Settings.EffectsEnabled || Settings.EffectsVolume == 0)
            {
                StopCardVoices();
            }
            else if (cardPlayer != null)
            {
                cardPlayer.Volume = muteOutputForVerification ? 0 : Settings.EffectsVolume;
            }
        }
    }
}

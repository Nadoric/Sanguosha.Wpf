using System.Windows;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow
    {
        public string AudioHeaderLabel => audio.Settings.Muted ? "声音：静音" : "声音";
        public string AudioStatus => audio.Status;
        public string EffectsVolumeLabel => $"{audio.Settings.EffectsVolume:P0}";
        public string VoicesVolumeLabel => $"{audio.Settings.VoicesVolume:P0}";
        public string PreviewHeroLabel => $"试听武将 · {PreviewHero.Name}";
        private Hero PreviewHero => HasGame && Lobby.Visibility != Visibility.Visible
            ? game.Human.Hero
            : selectedHero;

        public bool AudioMuted
        {
            get => audio.Settings.Muted;
            set
            {
                audio.Settings.Muted = value;
                audio.ApplySettings();
            }
        }

        public bool EffectsEnabled
        {
            get => audio.Settings.EffectsEnabled;
            set
            {
                audio.Settings.EffectsEnabled = value;
                audio.ApplySettings();
            }
        }

        public bool VoicesEnabled
        {
            get => audio.Settings.VoicesEnabled;
            set
            {
                audio.Settings.VoicesEnabled = value;
                audio.ApplySettings();
            }
        }

        public double EffectsVolume
        {
            get => audio.Settings.EffectsVolume;
            set
            {
                audio.Settings.EffectsVolume = value;
                audio.ApplySettings();
            }
        }

        public double VoicesVolume
        {
            get => audio.Settings.VoicesVolume;
            set
            {
                audio.Settings.VoicesVolume = value;
                audio.ApplySettings();
            }
        }

        private void BindGame(GameEngine next)
        {
            game.Changed -= Refresh;
            game.CueRaised -= audio.Handle;
            audio.StopAll();
            game = next;
            game.Changed += Refresh;
            game.CueRaised += audio.Handle;
        }

        private void Audio_Click(object sender, RoutedEventArgs e)
        {
            audio.StopAll();
            AudioOverlay.Visibility = Visibility.Visible;
            RaiseAll();
        }

        private void CloseAudio_Click(object sender, RoutedEventArgs e)
        {
            audio.StopAll();
            AudioOverlay.Visibility = Visibility.Collapsed;
        }

        private void PreviewEffect_Click(object sender, RoutedEventArgs e)
        {
            audio.PlayEffect("slash");
        }

        private void PreviewVoice_Click(object sender, RoutedEventArgs e)
        {
            audio.PreviewVoice(PreviewHero.Id);
        }
    }
}

// TEST-ONLY platform stubs. Silent production queue logic is executed; no native audio claims.
#pragma warning disable CS0067
namespace System.Windows.Media { public sealed class MediaPlayer { public event EventHandler? MediaOpened, MediaEnded; public event EventHandler? MediaFailed; public double Volume {get;set;} public FakeDuration NaturalDuration=>new(); public void Open(Uri u){} public void Play(){} public void Close(){} } public sealed class FakeDuration { public bool HasTimeSpan=>true; public TimeSpan TimeSpan=>TimeSpan.FromSeconds(3); } }
namespace System.Windows.Threading { public sealed class DispatcherTimer { public TimeSpan Interval {get;set;} public event EventHandler? Tick; public void Start(){} public void Stop(){} } }
namespace Sanguosha.Desktop { public sealed partial class GameAudio { private void ApplyCardAudioSettings(){} private void StopCardVoices(){} private void AnnounceCard(Sanguosha.Core.GameCue cue){} } }

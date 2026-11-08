using Sanguosha.Core;
using Sanguosha.Desktop;
int checks=0;
void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
GameCue Cue(string hero="liubei",int seat=1,int kills=1)=>new(GameCueKind.Eliminated,hero,Seat:seat,SourceSeat:0,Elimination:new(0,seat,kills,kills));
GameAudio New(){var a=new GameAudio(silent:true);a.Settings.Muted=false;a.Settings.VoicesEnabled=true;a.Settings.EffectsEnabled=true;a.Settings.VoicesVolume=.7;a.Settings.EffectsVolume=.5;a.ApplySettings();return a;}
using(var a=New()){
 List<EliminationPresentation> signals=new();a.EliminationReady+=signals.Add;
 string[] names={"liubei","guanyu","zhangfei","caocao","simayi","zhouyu","sunquan"};
 for(int i=0;i<7;i++)a.Handle(Cue(names[i],i+1,i+1));
 Check(a.PendingVoiceCount==13&&signals.Count==0,"Death voice must precede banner");
 a.Handle(new(GameCueKind.MatchEnded,HumanWon:true));
 for(int i=0;i<7;i++){Check(a.ActiveVoice==names[i]+":death","Victim order");a.FinishVoiceForVerification();Check(a.ActiveVoice==$"kill:{i+1}"&&signals.Count==i+1&&signals[i].Cue.Seat==i+1&&signals[i].HasSound,"Matching visual/audio release");a.FinishVoiceForVerification();}
 Check(a.ActiveVoice==null&&a.PendingVoiceCount==0,"Queue drained");
}
foreach(string mode in new[]{"muted","effectsOff","voiceOff","skip","toggleOff","missing","muteQueued","stop"}){
 using var a=New();List<EliminationPresentation> signals=new();a.EliminationReady+=signals.Add;
 if(mode=="muted")a.Settings.Muted=true;
 if(mode=="effectsOff")a.Settings.EffectsEnabled=false;
 if(mode=="voiceOff")a.Settings.VoicesEnabled=false;
 if(mode=="toggleOff")a.KillEffectsEnabled=false;
 if(mode=="missing")a.Catalog.Effects["kill:1"]="Effects/not-present.mp3";
 a.ApplySettings();a.Handle(Cue());
 if(mode=="skip")a.StopKillAnnouncements();
 if(mode=="stop")a.StopAll();
 if(mode=="muteQueued"){a.Settings.Muted=true;a.ApplySettings();}
 a.FinishVoiceForVerification();
 if(mode is "skip" or "toggleOff" or "stop")Check(signals.Count==0,"Cancelled queue must never resurrect presentation");
 else Check(signals.Count==1&&signals[0].HasSound==(mode=="voiceOff"),"Silent/missing/mute fallback must release one matching visual");
 a.FinishVoiceForVerification();Check(signals.Count<=1,"No duplicate visual on audio completion");
}
Console.WriteLine($"PASS {checks} production GameAudio silent-queue checks (platform stubs; not native media/WPF verification)");

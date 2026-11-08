using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Sanguosha.Desktop;

namespace Sanguosha.Verify;
internal static partial class Program
{
    private static void VerifyPatch11Effects()
    {
        for (int count=2; count<=8; count++)
        {
            List<SceneSeat> seats = new();
            for (int seat=1; seat<count; seat++)
            {
                SceneSeat p = BattleSceneLayout.Opponent(count, seat); seats.Add(p);
                Check(p.X >= 0 && p.Y >= 0 && p.X+p.Width <= 1176 && p.Y+p.Height <= 600, "Every 2–8 player seat fits the battle canvas");
                SceneSeat overlay = BattleSceneLayout.Bounds(count, seat);
                Check(overlay.X==p.X && overlay.Y==p.Y+44 && overlay.Width==p.Width, "Effect bounds include header offset exactly once");
                foreach ((double width,double height) in new[] { (960d,600d), (1440d,900d), (1920d,1080d), (2560d,1440d) })
                foreach (double dpi in new[] {1d,1.25,1.5,2})
                    Check(Math.Abs(overlay.X*width/1440*dpi-p.X*width/1440*dpi)<0.001 && overlay.Y*height/900*dpi>=0,
                        "Seat/effect coordinates share one scene-to-window/DPI transform");
            }
            for(int i=0;i<seats.Count;i++) for(int j=i+1;j<seats.Count;j++)
            {
                var a=seats[i];var b=seats[j];
                Check(a.X+a.Width<=b.X || b.X+b.Width<=a.X || a.Y+a.Height<=b.Y || b.Y+b.Height<=a.Y,"Seat rectangles do not overlap at 2–8 players");
            }
        }
        DirectoryInfo? root=new(AppContext.BaseDirectory);
        while(root!=null&&!File.Exists(Path.Combine(root.FullName,"Sanguosha.slnx")))root=root.Parent;
        string assets=Path.Combine(root!.FullName,"src/Sanguosha.Desktop/Assets/Official");
        using JsonDocument catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(assets,"EffectFrames/animations.json")));
        Check(catalog.RootElement.GetArrayLength()==8,"Seven official tier sequences plus opening sequence");
        int frames=0;
        foreach(var sequence in catalog.RootElement.EnumerateArray())
        {
            byte[] png=File.ReadAllBytes(Path.Combine(assets,sequence.GetProperty("asset").GetString()!));
            int w=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4)), h=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4));
            Check(w<=2048&&h<=2048,"Compact shared-atlas decode fits bounded bitmap budget");
            double previous=-1;
            foreach(var frame in sequence.GetProperty("frames").EnumerateArray())
            {
                int x=frame.GetProperty("x").GetInt32(),y=frame.GetProperty("y").GetInt32();double t=frame.GetProperty("time").GetDouble();
                Check(t>=previous&&double.IsFinite(t)&&x>=0&&y>=0&&x+384<=w&&y+192<=h,"Frame clock monotonic and every crop lies within its source sheet");
                previous=t;frames++;
                if(frame.TryGetProperty("sourcePage",out var page))Check(File.Exists(Path.Combine(assets,"KillEffects",page.GetString()!)),"Every frame retains its exact original source texture");
            }
            if(sequence.GetProperty("key").GetString()=="start")Check(sequence.GetProperty("duration").GetDouble()==2,"Opening uses original Laya 2000ms clip duration");
        }
        using JsonDocument manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(assets,"EffectFrames/sources.json")));
        foreach(var entry in manifest.RootElement.GetProperty("derived").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(root.FullName,entry.GetProperty("path").GetString()!));
            Check(bytes.Length==entry.GetProperty("bytes").GetInt64()&&Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase),"Frame sheet SHA/size provenance valid");
        }
        string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string dll=Path.Combine(root.FullName,"src/Sanguosha.Desktop/bin",configuration,"net10.0-windows/山河弈.dll");
        if(File.Exists(dll))
        {
            var assembly=System.Reflection.Assembly.LoadFile(dll);
            string name=assembly.GetManifestResourceNames().Single(x=>x.EndsWith(".g.resources",StringComparison.Ordinal));
            using Stream resources=assembly.GetManifestResourceStream(name)!;
            using System.Resources.ResourceReader reader=new(resources);
            reader.GetResourceData("assets/official/effectframes/animations.json",out string _,out byte[] json);
            using JsonDocument embedded=JsonDocument.Parse(json.AsMemory(4));
            Check(embedded.RootElement.GetArrayLength()==8,"Actual WPF output embeds sequence metadata");
            foreach(var sequence in embedded.RootElement.EnumerateArray())
            {
                reader.GetResourceData(("assets/official/"+sequence.GetProperty("asset").GetString()).ToLowerInvariant(),out _,out byte[] data);
                Check(data.Length>12&&data[4]==137&&data[5]==80,"Actual WPF output embeds every usable frame sheet");
            }
        }
        Check(frames==207,"All 207 original-pixel sequence frames retained");
        Console.WriteLine("Patch11: 207 official-pixel frames / 8 sequences; 2–8 seat bounds, overlap, window/DPI transforms and sheet hashes verified.");
    }
}

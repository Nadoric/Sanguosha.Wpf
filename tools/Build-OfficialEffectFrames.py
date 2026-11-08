#!/usr/bin/env python3
"""Rebuild compact original-pixel effect sheets; requires Pillow, no network."""
from pathlib import Path
import json, struct, hashlib
from PIL import Image
root=Path(__file__).resolve().parent.parent
base=root/"src/Sanguosha.Desktop/Assets/Official"
out=base/"EffectFrames"; out.mkdir(exist_ok=True)
prefixes={"shoupo":"zi/shoupo_tx1_", "lianpo":"2/tx/lianpo_tx1_", "sanpo":"3/tx/sanpo_", "sipo":"4/tx/zi1_", "wupo":"5/tx/wupo_lg_", "liupo":"6/huoyan/liupo_lg_", "qipo":"7/shandian/7po_lg_"}
def atlas(name, prefix):
    lines=(base/"KillEffects"/(name+".atlas")).read_text().splitlines(); page=""; frames=[]
    for i,line in enumerate(lines):
        if line.endswith(".png"): page=line
        if line.startswith(prefix):
            props={}
            for value in lines[i+1:]:
                if ":" not in value: break
                k,v=value.split(":",1); props[k]=v
            frames.append(dict(source=line, sourcePage=page, index=int(line[len(prefix):]), props=props))
    return sorted(frames,key=lambda f:f["index"])
packets=[]; derived=[]
def save(key, frames, tiles, duration):
    sheet=Image.new("RGBA",(384*5,192*((len(tiles)+4)//5)))
    for i,tile in enumerate(tiles):
        tile.thumbnail((384,192)); sheet.alpha_composite(tile,(i%5*384+(384-tile.width)//2,i//5*192+(192-tile.height)//2))
        frames[i].update(x=i%5*384,y=i//5*192,width=384,height=192)
    path=out/(key+".png"); sheet.save(path,optimize=True); data=path.read_bytes()
    derived.append(dict(path=path.relative_to(root).as_posix(),bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),operation="original pixels; undo atlas rotation and trim; fit 384x192; pack sheet"))
    packets.append(dict(key=key,asset="EffectFrames/"+key+".png",duration=duration,frames=frames))
for tier,(name,prefix) in enumerate(prefixes.items(),1):
    tiles=[]; records=[]; textures={}; frames=atlas(name,prefix)
    for frame in frames:
        p=frame["props"]; x,y,w,h=map(int,p["bounds"].split(",")); rot=p.get("rotate")=="90"
        page=frame["sourcePage"]
        if page not in textures: textures[page]=Image.open(base/"KillEffects"/page).convert("RGBA")
        image=textures[page].crop((x,y,x+(h if rot else w),y+(w if rot else h)))
        if rot:image=image.transpose(Image.Transpose.ROTATE_270)
        ox,oy,ow,oh=map(int,p.get("offsets",f"0,0,{w},{h}").split(","))
        canvas=Image.new("RGBA",(ow,oh)); canvas.alpha_composite(image,(ox,oh-oy-image.height)); tiles.append(canvas)
        records.append(dict(time=frame["index"]/24,source=frame["source"],sourcePage=page))
    save("kill"+str(tier),records,tiles,(frames[-1]["index"]+2)/24)
# Laya 1.6 extension table: original texture names + eight floats per trim rectangle.
data=(base/"GameStart/SF_kaizhan_eff_putong.sk").read_bytes(); pos=0
for _ in range(3): length=struct.unpack_from("<H",data,pos)[0]; pos+=length+2
header=pos
_,_,pos=struct.unpack_from("<BII",data,pos)
playtime=struct.unpack_from("<f",data,header+12)[0]/1000
count=struct.unpack_from("<I",data,pos)[0];pos+=4
length=struct.unpack_from("<H",data,pos)[0];pos+=2
names=data[pos:pos+length].decode().split("\n");pos+=length; regions={}
for i in range(count): regions[names[i*2+1]]=struct.unpack_from("<8f",data,pos);pos+=32
texture=Image.open(base/"GameStart/SF_kaizhan_eff_putong.png").convert("RGBA");tiles=[];records=[]
for i in range(20):
    name=f"dasima/kaizhan/kaizhan_sg_{i:02}";x,y,w,h,ox,oy,fw,fh=map(int,regions[name]);image=texture.crop((x,y,x+w,y+h))
    if fw<fh:image=image.transpose(Image.Transpose.ROTATE_270)
    canvas=Image.new("RGBA",(345,345));canvas.alpha_composite(image,(ox,oy));tiles.append(canvas);records.append(dict(time=i/24,source=name,sourceSkeleton="GameStart/SF_kaizhan_eff_putong.sk"))
save("start",records,tiles,playtime)
(out/"animations.json").write_text(json.dumps(packets,ensure_ascii=False,indent=2)+"\n")
(out/"sources.json").write_text(json.dumps(dict(notes="Original atlas/SK fragments; 24fps reconstructed sampling, not a full skeletal runtime.",derived=derived),ensure_ascii=False,indent=2)+"\n")
print(f"PASS: {sum(len(x['frames']) for x in packets)} frames / {len(packets)} sequences, official opening duration {playtime}s")

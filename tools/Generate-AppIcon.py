"""Original ShanHeYi seal: lacquer, twin jian swords and a mountain mark.
Requires Python 3 and Pillow. No downloaded artwork, fonts or network needed.
Run from any directory; writes the app ICO and 512px brand PNG into the project.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

ROOT = Path(__file__).resolve().parents[1]
S = 1024

def gradient(top, bottom):
    im = Image.new('RGBA', (S, S))
    draw = ImageDraw.Draw(im)
    for y in range(S):
        t = y / (S - 1)
        c = tuple(round(a * (1-t) + b*t) for a,b in zip(top,bottom))
        draw.line((0,y,S,y), fill=(*c,255))
    return im

def render(simple=False):
    im = Image.new('RGBA', (S,S))
    shadow = Image.new('RGBA', (S,S)); d=ImageDraw.Draw(shadow)
    d.rounded_rectangle((65,74,959,980), radius=202, fill=(0,0,0,160))
    im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(18)))
    mask=Image.new('L',(S,S)); m=ImageDraw.Draw(mask)
    m.rounded_rectangle((48,40,976,968),radius=192,fill=255)
    plate=gradient((133,40,42),(39,13,23)); im.paste(plate,(0,0),mask)
    d=ImageDraw.Draw(im)
    # Gilt lip, inner lacquer bevel and warm, engraved octagonal frame.
    d.rounded_rectangle((51,43,973,965),radius=190,outline='#D1A759',width=10)
    d.rounded_rectangle((66,58,958,950),radius=178,outline='#532C28',width=5)
    octagon=[(316,112),(708,112),(900,304),(900,704),(708,896),(316,896),(124,704),(124,304)]
    d.line(octagon+[octagon[0]],fill='#C69D55',width=9,joint='curve')
    inner=[(329,138),(695,138),(874,317),(874,691),(695,870),(329,870),(150,691),(150,317)]
    d.line(inner+[inner[0]],fill='#8D5836',width=3,joint='curve')
    if not simple:
        # Opposing ceremonial blades, each with a bright cutting edge.
        for flip in (False,True):
            blade=Image.new('RGBA',(S,S)); b=ImageDraw.Draw(blade)
            b.polygon([(248,238),(315,260),(701,661),(661,701),(260,315)],fill='#D7BD86')
            b.polygon([(248,238),(701,661),(681,682)],fill='#FFF0C5')
            b.line([(274,272),(681,677)], fill='#98764B', width=6)
            b.line([(632,740),(742,630)],fill='#5B301F',width=40)
            b.line([(632,732),(734,630)],fill='#E5B965',width=21)
            b.line([(695,695),(783,783)], fill='#2D2021',width=45)
            b.line([(698,698),(780,780)], fill='#BE8545',width=26)
            for t in range(713,779,17): b.line([(t-9,t+9),(t+9,t-9)],fill='#F2CD83',width=5)
            b.ellipse((762,762,806,806),fill='#E6BE75',outline='#6C3A28',width=5)
            if flip: blade=blade.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            im.alpha_composite(blade)
        d=ImageDraw.Draw(im)
        d.polygon([(512,267),(753,505),(512,749),(271,505)],fill='#2D101C',outline='#E4B966',width=9)
        d.polygon([(512,291),(727,505),(512,722),(297,505)],fill='#6F202C',outline='#915033',width=3)
    # Original geometric mountain seal; bold enough to survive 16px.
    emblem=Image.new('L',(S,S)); e=ImageDraw.Draw(emblem)
    if simple:
        pts=[(294,416),(294,642),(730,642),(730,416)]
        e.line(pts,fill=255,width=68,joint='curve');e.line((512,306,512,642),fill=255,width=72)
    else:
        e.line([(380,454),(380,579),(644,579),(644,454)], fill=255,width=43,joint='curve')
        e.line((512,388,512,579),fill=255,width=46)
        e.line([(401,625),(449,610),(512,628),(575,610),(623,625)],fill=255,width=12,joint='curve')
    gold=gradient((255,237,183),(180,123,54));im.paste(gold,(0,0),emblem)
    d=ImageDraw.Draw(im)
    if not simple:
        for x,y in [(512,174),(512,834),(201,505),(823,505)]:
            d.polygon([(x,y-13),(x+13,y),(x,y+13),(x-13,y)],fill='#DDB66D')
    return im

preview=render().resize((512,512),Image.Resampling.LANCZOS)
brand=ROOT/'src/Sanguosha.Desktop/Assets/Branding';brand.mkdir(parents=True,exist_ok=True)
preview.save(brand/'shanheyi-mark.png')
# An ICO with explicitly drawn small-size masters, not illegible shrunk details.
masters={n:render(n<=32).resize((n,n),Image.Resampling.LANCZOS) for n in (16,24,32,48,64,128,256)}
masters[256].save(ROOT/'src/Sanguosha.Desktop/Assets/Official/Icon/app.ico',format='ICO',
                  sizes=[(n,n) for n in masters],append_images=list(masters.values()))
print('Generated original icon (16/24/32/48/64/128/256) and 512px brand preview.')

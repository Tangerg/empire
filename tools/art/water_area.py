"""Approved reusable water occupancy and bounded material sampling geometry."""
from PIL import Image, ImageDraw

def water_area_mask(size,mask8):
    if not 0<=mask8<=255 or size not in (32,64):raise ValueError('expected8bit mask and32/64 tile')
    scale=4;s=size*scale;inset=round(s*.10)
    n,ne,e,se,south,sw,w,nw=[bool(mask8&(1<<i)) for i in range(8)]
    out=Image.new('L',(s,s));d=ImageDraw.Draw(out)
    # Full shared edges; inset only at exposed shores. No cross-cell painting.
    d.rectangle((0 if w else inset,0 if n else inset,s-1 if e else s-inset-1,s-1 if south else s-inset-1),fill=255)
    # Preserve a concave land notch only where two cardinal water neighbors
    # meet a genuinely absent diagonal. This never hollows a2xN interior.
    for a,b,diagonal,cx,cy in [(n,w,nw,0,0),(n,e,ne,s,0),(south,e,se,s,s),(south,w,sw,0,s)]:
        if a and b and not diagonal:d.ellipse((cx-inset,cy-inset,cx+inset,cy+inset),fill=0)
    return out.resize((size,size),Image.Resampling.LANCZOS)

def water_period(source, period):
    tile=source.convert('RGB').resize((period,period),Image.Resampling.LANCZOS)
    # Same bounded one-pixel opposite-edge matching at the larger UV period.
    corner=tuple(round(sum(tile.getpixel((x,y))[c] for x,y in [(0,0),(period-1,0),(0,period-1),(period-1,period-1)])/4) for c in range(3))
    for i in range(1,period-1):
        horizontal=tuple(round((tile.getpixel((i,0))[c]+tile.getpixel((i,period-1))[c])/2) for c in range(3))
        vertical=tuple(round((tile.getpixel((0,i))[c]+tile.getpixel((period-1,i))[c])/2) for c in range(3))
        tile.putpixel((i,0),horizontal);tile.putpixel((i,period-1),horizontal)
        tile.putpixel((0,i),vertical);tile.putpixel((period-1,i),vertical)
    for xy in [(0,0),(period-1,0),(0,period-1),(period-1,period-1)]:tile.putpixel(xy,corner)
    return tile

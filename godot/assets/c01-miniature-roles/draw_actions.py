"""C01 first-pass poses, authored joint/weapon pixel positions, never sprite warps."""
import sys
sys.dont_write_bytecode = True
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw
from draw_roles import PAL, role_image

ROOT=Path(__file__).parent
ROLES=['flag-bearer','swordsman','shield','spear','archer','cleric']


def pose(role, action, phase, rear):
    if action=='attack' and phase==0 and role not in ['archer','cleric']:
        # This first key is the already-authored idle, not a transformed frame.
        return role_image(role,rear)
    im=Image.new('RGBA',(64,64));d=ImageDraw.Draw(im)
    def p(points,c):d.polygon(points,fill=PAL[c])
    def r(box,c):d.rectangle(box,fill=PAL[c])
    def l(points,c,w=1):d.line(points,fill=PAL[c],width=w)
    def limb(points,c='cloth'):
        l(points,'ink',5);l(points,c,3);x,y=points[-1];r((x-1,y-1,x+1,y+1),'skin')
    def sword(hand,tip):
        l([hand,tip],'ink',4);l([hand,tip],'steel',2);l([(hand[0],hand[1]-1),(tip[0],tip[1]-1)],'steel_light')
        r((hand[0]-2,hand[1]-2,hand[0]+2,hand[1]),'gold')
    def head(cx,cy):
        p([(cx-6,cy-5),(cx-2,cy-8),(cx+3,cy-7),(cx+7,cy-2),(cx+6,cy+5),(cx+1,cy+8),(cx-5,cy+5)],'ink')
        if rear:
            p([(cx-4,cy-4),(cx-1,cy-6),(cx+3,cy-5),(cx+5,cy-1),(cx+4,cy+4),(cx-2,cy+5),(cx-4,cy+2)],'hair' if role=='flag-bearer' else 'cloth_dark' if role in ['archer','cleric'] else 'steel_dark')
            l([(cx-3,cy-4),(cx,cy-6),(cx+3,cy-4)],'wood' if role=='flag-bearer' else 'steel',2)
            l([(cx-4,cy+5),(cx+1,cy+7),(cx+4,cy+5)],'gray',2)
        else:
            p([(cx-2,cy-3),(cx+3,cy-3),(cx+5,cy),(cx+7,cy+2),(cx+4,cy+3),(cx+3,cy+6),(cx-2,cy+5)],'skin')
            r((cx+2,cy,cx+3,cy+1),'ink');r((cx+1,cy+3,cx+3,cy+4),'skin_light')
            cover='hair' if role=='flag-bearer' else 'cloth_dark' if role in ['archer','cleric'] else 'steel'
            p([(cx-6,cy-2),(cx-4,cy-6),(cx,cy-8),(cx+4,cy-6),(cx+6,cy-2),(cx+1,cy-3),(cx-3,cy+1),(cx-4,cy+4)],cover)
            l([(cx-3,cy-5),(cx,cy-6),(cx+3,cy-4)],'wood' if role=='flag-bearer' else 'cloth' if role in ['archer','cleric'] else 'steel_light')
    if action=='death':
        # Four separately constructed changing poses: stagger, kneel, collapse, prone.
        if phase==0:return pose(role,'hit',1,rear)
        if phase==1:
            p([(25,41),(35,41),(40,48),(44,54),(42,56),(32,55),(30,49),(24,54),(18,55),(18,51)],'ink')
            l([(26,43),(23,50),(20,53)],'leather',4);l([(34,43),(36,51),(41,54)],'steel_dark',4)
            p([(23,30),(33,29),(38,37),(34,44),(25,44),(21,39)],'ink');p([(25,32),(32,31),(35,37),(32,41),(25,41)],'gray_dark' if role=='cleric' else 'cloth')
            head(27,25);limb([(24,34),(19,41),(16,48)]);limb([(34,34),(39,41),(37,47)])
        elif phase==2:
            p([(23,42),(36,41),(44,46),(48,53),(46,56),(39,53),(34,48),(24,50),(17,49)],'ink')
            p([(24,43),(34,43),(40,47),(36,48),(26,48),(20,47)],'cloth');l([(39,46),(43,51),(46,53)],'leather',4)
            head(19,36);limb([(23,43),(18,48),(12,51)]);limb([(30,43),(32,49),(36,51)])
        else:
            p([(19,46),(31,44),(38,47),(45,46),(50,49),(49,53),(39,54),(34,52),(23,54),(18,52)],'ink')
            p([(21,47),(30,46),(35,48),(33,51),(23,52),(20,50)],'gray_dark' if role=='cleric' else 'cloth')
            l([(37,49),(46,48),(49,50)],'leather',4);l([(35,51),(42,52),(47,52)],'steel_dark',3)
            head(14,45);limb([(22,48),(18,53),(11,54)]);limb([(28,48),(30,53),(34,54)])
        if role in ['archer','spear','cleric','flag-bearer']:
            l([(8,57),(51,57)],'wood');
            if role=='archer':l([(40,56),(46,53),(54,55),(57,58)],'wood_light')
            elif role=='flag-bearer':p([(39,57),(51,57),(49,62),(40,60)],'gray')
            elif role=='cleric':r((48,54,54,59),'ink');r((49,55,53,58),'gold')
            else:p([(51,55),(59,57),(52,59)],'steel_light')
        else:
            sword((44,57),(58,58))
            if role=='swordsman':
                d.ellipse((36,51,46,57),fill=PAL['ink']);d.ellipse((38,52,44,55),fill=PAL['wood'])
            elif role=='shield':
                p([(35,51),(46,52),(49,56),(43,59),(34,56)],'ink');p([(36,52),(45,53),(46,56),(41,57),(36,55)],'gray')
        return im
    dx=(-2, -3, 0)[phase] if action=='hit' else 0
    dy=(0, 2, 0)[phase] if action=='hit' else (1 if phase==1 else 0)
    # Feet are fixed; arms/torso/head use explicit new poses.
    p([(24,39),(31,39),(30,48),(27,55),(20,55),(20,52),(24,46)],'ink');p([(32,39),(39,40),(39,48),(43,54),(43,56),(34,56),(32,47)],'ink')
    r((24,42,28,48),'shadow');r((34,42,37,49),'steel_dark');r((22,50,27,54),'leather');r((35,51,41,55),'leather');r((20,55,28,56),'sole');r((34,56,44,56),'sole')
    p([(25+dx,24+dy),(35+dx,24+dy),(40+dx,29+dy),(38,37),(41,43),(34,43),(30,40),(24,43),(22,38)],'ink')
    garment='leather' if role=='swordsman' else 'gray_dark' if role=='cleric' else 'cloth'
    p([(27+dx,26+dy),(34+dx,26+dy),(37+dx,29+dy),(35,37),(38,40),(33,40),(30,37),(25,40),(25,34)],garment)
    l([(27+dx,27+dy),(33+dx,27+dy)],'gray' if role=='cleric' else 'steel',2)
    if rear:l([(27+dx,29+dy),(35,35)],'leather',2)
    r((24,36,38,38),'leather');r((30,36,32,38),'gold')
    if role=='cleric':
        p([(25,36),(36,36),(42,52),(38,55),(31,52),(23,55),(19,52)],'ink');p([(27,38),(34,38),(38,51),(34,52),(30,49),(24,52),(22,50)],'gray_dark');l([(28,39),(26,49)],'gray')
    head(31+dx,17+dy)
    shoulder=(38+dx,29+dy);other=(25+dx,29+dy)
    if action=='hit':
        limb([shoulder,(40+dx,32+dy),(34+dx,31+dy)],garment);limb([other,(22+dx,34+dy),(27+dx,34+dy)],garment)
        # Equipment remains held, not replaced by a red flash.
        if role=='archer':l([(42,23),(47,29),(48,36),(44,44)],'wood_light',2);l([(42,23),(40,34),(44,44)],'gray_light')
        elif role in ['cleric','spear','flag-bearer']:
            l([(16,13),(20,54)],'wood_light',2)
            if role=='cleric':r((13,15,19,24),'ink');r((14,17,18,22),'gold')
            elif role=='flag-bearer':p([(16,13),(6,15),(7,23),(17,20)],'gray')
            else:p([(13,13),(15,5),(18,13)],'steel_light')
        else:
            sword((29+dx,35+dy),(44,41))
            sx=19 if rear else 43
            if role=='swordsman':
                d.ellipse((sx-5,29,sx+5,41),fill=PAL['ink']);d.ellipse((sx-3,31,sx+3,39),fill=PAL['wood'])
            elif role=='shield':
                p([(sx-4,28),(sx+3,27),(sx+6,30),(sx+5,42),(sx,47),(sx-4,42)],'ink');p([(sx-2,30),(sx+2,29),(sx+4,31),(sx+3,41),(sx,44),(sx-2,40)],'gray_dark' if rear else 'gray')
        return im
    def equipment_points(points):
        return [(64-x,y) for x,y in points] if rear else points
    def equipment_poly(points,color):p(equipment_points(points),color)
    def equipment_line(points,color,width=1):l(equipment_points(points),color,width)
    def equipment_rect(box,color):
        x0,y0,x1,y1=box
        r((64-x1,y0,64-x0,y1) if rear else box,color)
    def equipment_arm(points,color='cloth'):limb(equipment_points(points),color)
    if role=='archer':
        equipment_poly([(22,23),(17,25),(20,42),(25,40)],'wood');equipment_line([(20,27),(17,17)],'wood_light');equipment_rect((16,17,18,20),'linen')
        hold=(45,32);pull=[(27,36),(35,32),(29,29),(26,27),(30,32),(27,38)][phase]
        equipment_arm([shoulder,(42,30),hold]);equipment_arm([other,(27,32),pull])
        equipment_line([(49,19),(52,25),(53,35),(50,45)],'ink',3);equipment_line([(49,19),(51,26),(52,35),(50,45)],'wood_light')
        if phase==2:equipment_line([(49,20),pull,(50,44)],'gray_light')
        else:equipment_line([(49,20),(47,32),(50,44)],'gray_light')
        if phase in [0,1,2]:equipment_line([(pull[0],pull[1]),(57,32)],'wood_light');equipment_poly([(56,30),(60,32),(56,33)],'steel_light')
        # On phase3 arrow is absent, string relaxed, release hand behind cheek.
    elif role=='cleric':
        staffx=[45,44,43,43,44,45][phase];top=[18,15,10,10,15,18][phase]
        equipment_line([(staffx,top),(staffx,55)],'ink',3);equipment_line([(staffx,top),(staffx,54)],'wood_light');equipment_rect((staffx-3,top,staffx+3,top+10),'ink');equipment_rect((staffx-2,top+2,staffx+2,top+8),'gold');equipment_rect((staffx,top+3,staffx+1,top+6),'linen')
        equipment_arm([shoulder,(41,30),(staffx,31)],garment)
        hand=[(25,35),(25,29),(23,21),(18,26),(24,31),(27,36)][phase];equipment_arm([other,(22,29),hand],garment)
        # Release is an opened palm/extended arm, not an added glow overlay.
        if phase==3:equipment_rect((15,24,19,27),'skin_light');equipment_line([(15,23),(15,28)],'skin')
    else:
        hand=[(40,38),(41,23),(48,31),(40,39)][phase]
        tip=[(50,47),(48,10),(60,29),(49,48)][phase]
        if role in ['swordsman','shield']:
            hand=[(25,39),(24,23),(42,31),(27,39)][phase]
            tip=[(38,47),(28,10),(58,29),(40,48)][phase]
            shoulder=other
        if rear:
            hand=(64-hand[0],hand[1]);tip=(64-tip[0],tip[1]);shoulder=(64-shoulder[0],shoulder[1])
        limb([shoulder,((shoulder[0]+hand[0])//2,hand[1]+3),hand],garment)
        if role in ['spear','shield']:
            l([(hand[0]-10,hand[1]+5),tip],'ink',3);l([(hand[0]-10,hand[1]+5),tip],'wood_light');p([(tip[0]-2,tip[1]-2),(tip[0]+2,tip[1]-2),(tip[0],tip[1]+2)],'steel_light')
        else:sword(hand,tip)
        if role=='flag-bearer':
            sx=48 if rear else 12;l([(sx,5),(sx,55)],'wood_light',2);p([(sx,7),(sx-9,9),(sx-9,18),(sx,16)],'gray');limb([(25,29),(19,35),(sx,36)],garment)
        else:
            sx=21 if rear else 43
            limb([(26,29) if rear else (37,29),(sx,32),(sx,36)],garment)
            if role=='swordsman':d.ellipse((sx-5,30,sx+6,43),fill=PAL['ink']);d.ellipse((sx-3,32,sx+4,41),fill=PAL['wood']);r((sx,35,sx+1,37),'steel_light')
            elif role=='shield':p([(sx-4,29),(sx+3,28),(sx+7,32),(sx+5,44),(sx,49),(sx-4,44)],'ink');p([(sx-2,31),(sx+2,30),(sx+5,33),(sx+3,43),(sx,46),(sx-2,42)],'gray_dark' if rear else 'gray')
    return im


def main():
    metadata={'schema':1,'scope':'C01 first integrated action test only','frameSize':[64,64],'footAnchor':[32,56],
              'eventOwner':'existing BoardView receipt; this file supplies visual samples only',
              'unavailable':['walk','celebrate','cavalry-actions','captain-specific-actions'],'roles':{}}
    for role in ROLES:
        attack_name='cast' if role=='cleric' else 'attack'
        if role=='archer':durations=[100,100,120,80,100,100];release=3;contract='archer.loose'
        elif role=='cleric':durations=[90,100,110,100,100,100];release=3;contract='gravekeeper.channel'
        elif role=='swordsman':durations=[80,80,65,135];release=2;contract='torren.thrust'
        else:durations=[35,40,35,40];release=2;contract='static.attack'
        clips={attack_name:(len(durations),durations,release),'hit':(3,[70,90,100],None),'death':(4,[120,160,200,200],None)}
        width=sum(c[0] for c in clips.values())*64;atlas=Image.new('RGBA',(width,128));offset=0;clip_meta={}
        for action,(count,times,event_frame) in clips.items():
            rects=[]
            for rear in [False,True]:
                row=[]
                for phase in range(count):
                    f=pose(role,'attack' if action=='cast' else action,phase,rear);assert set(f.getchannel('A').tobytes())=={0,255}
                    atlas.alpha_composite(f,(offset+phase*64,int(rear)*64));row.append([offset+phase*64,int(rear)*64,64,64])
                rects.append(row)
            entry={'frontFrames':rects[0],'backFrames':rects[1],'durationsMs':times,'loop':False,'holdFinal':action=='death'}
            if event_frame is not None:entry.update({'releaseFrame':event_frame,'referenceReleaseMs':sum(times[:event_frame]),'existingClipContract':contract})
            clip_meta[action]=entry;offset+=count*64
        path=ROOT/(role+'-actions.png');atlas.save(path);metadata['roles'][role]={'texture':'res://assets/c01-miniature-roles/'+path.name,'sourceActions':({'attack':'cast','cast':'cast'} if role=='cleric' else {'attack':'attack'}),'clips':clip_meta,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    (ROOT/'action-sheets.json').write_text(json.dumps(metadata,indent=2)+'\n')
    print('Authored six independent infantry action sheets, front/back; metadata only, no event owner changes.')

if __name__=='__main__':main()

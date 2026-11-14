"""Original C01-only pixel construction. No resizing or imported illustration pixels."""
import sys
sys.dont_write_bytecode = True
from pathlib import Path
import argparse
import tempfile
import hashlib
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).parent
PAL = {
    'ink':'#252b31','shadow':'#34383d','leather':'#674c3a','wood':'#98704a',
    'wood_light':'#b99161','skin':'#c2916c','skin_light':'#edc49a','skin_dark':'#996850',
    'steel':'#7b8c92','steel_light':'#b9c6c5','steel_dark':'#4e606a',
    'cloth':'#526875','cloth_light':'#7d9197','cloth_dark':'#3a4d5b',
    'gray':'#999b8f','gray_light':'#c6c7b4','gray_dark':'#696f6b','sole':'#332e2e',
    'rust':'#9b6652','linen':'#d9ceaa','gold':'#b79a60','hair':'#5e4234',
}


def initial_front(role):
    image = Image.new('RGBA', (64, 64))
    d = ImageDraw.Draw(image)
    def rect(box, color): d.rectangle(box, fill=PAL[color])
    def poly(points, color): d.polygon(points, fill=PAL[color])
    def line(points, color, width=1): d.line(points, fill=PAL[color], width=width)
    # All roles share a 56px contact plane, not a painted floor/shadow.
    if role == 'flag-bearer':
        rect((11,5,13,55),'ink');rect((12,7,12,53),'wood_light')
        poly([(12,6),(3,7),(2,17),(6,16),(9,19),(12,17)],'ink')
        poly([(11,8),(4,9),(4,14),(7,14),(10,16),(11,15)],'gray')
        line([(5,9),(10,9)],'gray_light');rect((6,10,7,13),'gray_dark')
        poly([(11,4),(12,1),(14,4)],'steel_light')
        poly([(23,16),(19,20),(18,27),(22,25),(25,19)],'hair')
    elif role == 'archer':
        poly([(21,24),(16,25),(19,43),(24,41)],'ink')
        poly([(20,28),(18,28),(21,40),(23,39)],'wood')
        line([(19,29),(16,20)],'wood_light');line([(21,28),(19,19)],'wood_light')
        poly([(15,19),(15,23),(18,23),(17,19)],'linen')
        poly([(18,18),(18,22),(21,22),(20,18)],'steel_light')
    # Legs remain separated with enough negative space for 1x reading.
    poly([(24,39),(33,39),(32,47),(28,53),(28,55),(20,55),(20,52),(24,46)],'ink')
    poly([(33,39),(40,40),(40,48),(43,52),(44,56),(34,56),(33,49),(31,46)],'ink')
    poly([(25,41),(30,41),(29,47),(25,51),(22,51),(25,46)],'shadow')
    poly([(34,41),(38,42),(37,48),(40,52),(35,52),(34,47)],'steel_dark' if role=='flag-bearer' else 'shadow')
    rect((22,50,27,54),'leather');rect((35,51,41,55),'leather')
    rect((23,50,27,51),'wood');rect((35,51,39,52),'wood')
    rect((20,55,28,56),'sole');rect((34,56,44,56),'sole')
    # Cloth/armor silhouette with a restrained three-value ramp.
    poly([(25,23),(35,22),(41,27),(40,37),(42,42),(34,43),(31,39),(28,43),(21,42),(24,34)],'ink')
    poly([(26,25),(35,24),(38,28),(37,37),(39,40),(34,41),(31,37),(27,40),(24,40),(27,33)],'cloth')
    poly([(27,25),(31,25),(30,34),(27,38),(25,38)],'cloth_light')
    poly([(34,26),(37,28),(36,37),(38,40),(34,40),(32,36)],'cloth_dark')
    rect((24,35,39,37),'leather');rect((30,35,33,38),'gold');rect((31,36,32,37),'ink')
    if role == 'shield':
        rect((25,27,36,29),'steel');line([(25,27),(35,27)],'steel_light')
        rect((26,30,36,31),'steel_dark');rect((23,39,28,41),'rust');rect((34,40,40,42),'rust')
    if role == 'flag-bearer':
        poly([(25,25),(30,24),(33,27),(31,31),(26,31),(23,28)],'steel_dark')
        poly([(25,25),(29,25),(31,27),(29,29),(25,29)],'steel');line([(25,25),(29,25)],'steel_light')
    # Head: original compact character identities, light face to the right.
    poly([(26,13),(34,12),(38,17),(37,23),(33,26),(27,24),(24,20)],'ink')
    poly([(29,15),(35,15),(36,18),(39,20),(36,21),(35,24),(31,24),(28,21)],'skin')
    poly([(32,16),(35,17),(35,19),(37,20),(34,21),(34,23),(31,22)],'skin_light')
    rect((34,18,35,19),'ink');rect((28,20,29,22),'skin_dark')
    if role == 'archer':
        poly([(24,13),(28,10),(34,10),(38,15),(37,18),(32,15),(29,17),(28,22),(31,26),(26,25),(22,20)],'ink')
        poly([(25,14),(29,11),(33,11),(36,14),(31,14),(27,18),(27,22),(25,21),(24,18)],'cloth')
        line([(26,14),(29,12),(33,12)],'cloth_light');line([(25,20),(28,24),(32,25)],'cloth_dark')
    elif role == 'shield':
        poly([(24,16),(25,12),(29,10),(34,10),(38,14),(39,18),(24,19),(22,18)],'ink')
        poly([(26,15),(27,12),(30,11),(33,12),(36,15),(37,16),(25,17)],'steel')
        line([(28,12),(30,11),(33,12)],'steel_light');rect((24,17,38,18),'steel_dark');rect((29,11,30,16),'steel_light')
    else:
        poly([(24,16),(25,12),(29,10),(34,11),(36,14),(33,16),(31,14),(29,17),(27,16),(26,22),(24,20)],'hair')
        line([(27,13),(30,12),(33,13)],'wood');rect((28,24,33,25),'gray')
    # Equipment drawn last with clear negative-space boundaries.
    if role == 'flag-bearer':
        poly([(24,28),(20,29),(17,34),(12,34),(11,38),(19,38),(24,34)],'ink')
        line([(23,30),(20,33),(17,36)],'steel',3);rect((11,34,14,37),'skin_light')
        poly([(38,29),(41,31),(41,36),(38,38),(36,34)],'ink');rect((38,32,40,35),'steel');rect((38,36,41,39),'skin')
        line([(40,39),(49,48)],'ink',4);line([(42,40),(50,48)],'steel',2);line([(42,40),(49,47)],'steel_light');line([(37,40),(42,36)],'gold')
    elif role == 'shield':
        # Pale tall shield with its dark contour separated from the short spear.
        poly([(37,27),(44,26),(49,29),(48,43),(44,49),(38,45),(36,32)],'ink')
        poly([(39,29),(44,28),(47,30),(46,42),(43,46),(40,43),(38,32)],'gray')
        line([(39,30),(43,29),(46,30)],'gray_light');line([(44,31),(43,43)],'gold',2)
        poly([(25,30),(21,32),(20,38),(24,40),(27,37)],'ink');rect((22,32,25,36),'steel_dark');rect((22,37,25,40),'skin')
        line([(14,45),(34,33)],'ink',3);line([(14,45),(34,33)],'wood');poly([(33,32),(38,29),(36,35)],'steel_light')
    else:
        poly([(37,27),(42,29),(44,34),(42,37),(38,34)],'ink');line([(38,29),(42,32),(43,35)],'cloth',3)
        rect((42,34,45,37),'skin_light')
        line([(48,20),(51,24),(53,31),(53,37),(50,45)],'ink',3)
        line([(48,20),(51,25),(52,31),(52,37),(50,45)],'wood_light')
        line([(48,21),(46,33),(50,44)],'gray_light')
        poly([(25,29),(22,31),(22,36),(25,40),(28,38),(26,34)],'ink');line([(25,31),(24,35)],'cloth',3);rect((25,36,28,39),'skin')
        line([(27,38),(45,36)],'wood_light');poly([(46,35),(48,36),(45,37)],'steel_light')
    return image


ROLES = ['flag-bearer','swordsman','archer','spear','cleric','cavalry','cavalry-captain','shield','shield-captain']
UNIT_ROLES = {'c01.laiya':'flag-bearer','c01.swordsman':'swordsman','c01.archer':'archer',
              'c01.banner-guard':'spear','c01.gravekeeper':'cleric','c01.knight':'cavalry',
              'c01.roderick':'cavalry-captain','c01.legion-shield':'shield','c01.cain':'shield-captain'}


def constructed_role(role, rear=False):
    im=Image.new('RGBA',(64,64)); d=ImageDraw.Draw(im)
    def r(box,c):d.rectangle(box,fill=PAL[c])
    def p(points,c):d.polygon(points,fill=PAL[c])
    def l(points,c,w=1):d.line(points,fill=PAL[c],width=w)
    mounted=role.startswith('cavalry')
    # Horse occupies the same contact plane, with a broader silhouette than infantry.
    if mounted:
        if rear:
            p([(14,34),(11,23),(16,20),(20,25),(23,32),(43,32),(49,37),(47,46),(42,48),(24,46),(19,41)],'ink')
            p([(14,25),(17,23),(19,28),(21,35),(43,34),(46,38),(43,44),(25,43),(20,38)],'leather')
            p([(13,22),(14,17),(17,21),(19,20),(18,27),(10,30),(8,27)],'ink')
            p([(13,23),(17,22),(16,26),(10,28),(10,26)],'wood')
            l([(46,35),(51,42),(51,49)],'hair',3)
            legs=[(20,42,18,55),(27,43,25,54),(40,43,42,55),(46,42,46,54)]
        else:
            p([(15,34),(35,32),(40,25),(44,21),(49,24),(47,36),(45,43),(36,47),(19,46),(13,41)],'ink')
            p([(17,35),(36,34),(41,28),(45,24),(47,25),(44,37),(41,42),(34,44),(20,43),(16,40)],'leather')
            p([(43,23),(44,18),(47,22),(49,20),(49,24),(55,28),(54,32),(47,31)],'ink')
            p([(46,24),(49,25),(53,29),(51,30),(47,28)],'wood')
            l([(15,35),(10,42),(10,49)],'hair',3)
            legs=[(17,42,16,55),(24,43,25,54),(39,43,38,55),(45,41,47,54)]
        for x,y,xx,yy in legs:
            l([(x,y),(xx,yy)],'ink',3);l([(x,y+2),(xx,yy-2)],'wood');r((xx-2,55,xx+2,56),'sole')
        p([(24,32),(37,32),(40,42),(24,42)],'cloth_dark');l([(26,33),(35,33),(37,40)],'rust',2)
        # Rider is authored smaller and higher, not a standing sprite pasted on a horse.
        p([(26,20),(35,20),(39,28),(36,34),(32,34),(30,30),(26,34),(22,31)],'ink')
        p([(27,22),(34,21),(36,27),(33,30),(27,29),(24,30)],'steel_dark')
        l([(27,22),(33,22),(35,25)],'steel',2);r((27,30,35,31),'leather')
        l([(25,31),(24,39),(28,41)],'ink',4);l([(26,32),(26,38)],'steel',2)
        if rear:
            p([(24,10),(29,7),(34,8),(37,13),(36,19),(30,22),(24,18)],'ink')
            p([(25,11),(29,9),(33,10),(35,14),(34,18),(28,19),(25,16)],'steel_dark')
            l([(27,11),(30,10),(33,12)],'steel');r((24,17,35,19),'hair')
        else:
            p([(26,10),(31,7),(35,10),(37,15),(35,21),(29,21),(26,17)],'ink')
            p([(30,14),(35,14),(36,17),(34,19),(30,19)],'skin');r((34,15,35,16),'ink')
            p([(26,12),(29,8),(33,9),(36,12),(37,14),(25,14)],'steel');l([(29,10),(33,10)],'steel_light')
        # Lance is horizontal-oblique with a clearly separate steel point.
        if rear:
            l([(7,22),(44,32)],'ink',3);l([(9,23),(44,32)],'wood_light');p([(3,20),(10,20),(9,25)],'steel_light');r((31,27,34,30),'skin')
        else:
            l([(20,32),(57,22)],'ink',3);l([(20,32),(56,23)],'wood_light');p([(56,20),(61,20),(57,25)],'steel_light');r((35,26,38,29),'skin')
        if role=='cavalry-captain':
            p([(29,8),(28,4),(33,2),(37,5),(34,8)],'ink');l([(30,5),(33,4),(35,5)],'rust',2)
            p([(25,22),(22,26),(23,36),(29,32)],'rust');l([(25,23),(24,30)],'gold')
        return im
    # Independent back/body construction, never a reflected front texture.
    for points in [[(24,39),(31,39),(30,48),(27,55),(20,55),(20,52),(24,46)],[(32,39),(39,40),(39,48),(43,54),(43,56),(34,56),(32,47)]]:p(points,'ink')
    r((24,42,28,48),'shadow');r((34,42,37,49),'steel_dark');r((22,50,27,54),'leather');r((35,51,41,55),'leather')
    r((20,55,28,56),'sole');r((34,56,44,56),'sole')
    cloth='gray_dark' if role=='cleric' else 'leather' if role=='swordsman' else 'cloth'
    p([(25,23),(35,23),(41,28),(39,36),(42,43),(34,44),(31,40),(27,44),(21,42),(24,34)],'ink')
    p([(26,25),(34,25),(38,29),(36,36),(39,41),(34,41),(31,37),(26,41),(24,39),(27,32)],cloth)
    l([(27,26),(30,25),(34,26)],'gray' if role=='cleric' else 'wood' if role=='swordsman' else 'cloth_light',2)
    if rear:l([(26,27),(34,34)],'leather',2)
    else:r((29,28,35,32),'steel_dark' if role in ['shield','shield-captain','spear'] else cloth)
    r((24,36,39,38),'leather');r((30,36,32,38),'gold')
    if role=='cleric':
        p([(24,34),(37,34),(43,53),(38,55),(31,52),(24,55),(19,52)],'ink')
        p([(26,36),(35,36),(39,51),(35,52),(30,49),(24,52),(22,50)],'gray_dark')
        l([(27,38),(25,49)],'gray');l([(33,38),(35,48)],'cloth_dark',2)
    # Rear view shows nape/cowl/helmet back, not a mirrored visible face.
    if rear:
        p([(25,12),(30,10),(36,13),(38,18),(36,23),(31,26),(25,24),(23,19)],'ink')
        p([(25,15),(28,12),(33,12),(36,15),(36,20),(32,23),(26,22)],'hair' if role=='flag-bearer' else 'cloth_dark' if role in ['archer','cleric'] else 'steel_dark')
        l([(27,14),(30,12),(34,14)],'wood' if role=='flag-bearer' else 'cloth' if role in ['archer','cleric'] else 'steel',2)
        l([(25,22),(29,24),(34,23)],'gray',2)
    else:
        p([(25,13),(31,10),(36,13),(38,18),(37,23),(33,26),(27,24),(24,19)],'ink')
        p([(29,14),(34,14),(36,18),(38,20),(35,22),(34,24),(29,22)],'skin');r((34,17,35,18),'ink');r((32,19,35,21),'skin_light')
        p([(25,13),(28,10),(33,10),(36,13),(37,16),(24,17)],'cloth_dark' if role=='cleric' else 'steel')
        l([(28,12),(31,11),(34,13)],'cloth' if role=='cleric' else 'steel_light')
        l([(26,23),(30,25),(35,24)],'gray',2)
    # Short sleeves and hands remain attached to torso.
    l([(25,28),(21,33),(23,38)],'ink',5);l([(25,29),(23,33)],cloth,3);r((22,36,25,39),'skin')
    l([(37,28),(41,32),(40,37)],'ink',5);l([(37,29),(39,33)],cloth,3);r((39,35,42,38),'skin')
    if role=='flag-bearer':
        r((47,4,49,55),'ink');r((48,6,48,54),'wood_light');p([(48,5),(59,7),(59,17),(55,15),(50,18),(48,16)],'ink');p([(50,7),(57,8),(57,13),(54,12),(50,15)],'gray');l([(50,8),(56,9)],'gray_light');p([(47,4),(48,1),(50,4)],'steel_light');r((45,35,49,38),'skin')
        l([(23,39),(12,47)],'ink',4);l([(22,39),(12,47)],'steel_light',2)
    elif role in ['shield','shield-captain','swordsman']:
        if role=='swordsman':
            d.ellipse((37,28,49,42),fill=PAL['ink']);d.ellipse((39,30,47,40),fill=PAL['wood']);r((42,33,44,36),'steel_light')
            l([(24,39),(12,47)] if rear else [(25,39),(38,47)],'ink',4);l([(24,39),(12,47)] if rear else [(25,39),(38,47)],'steel_light',2)
        else:
            p([(17,28),(23,27),(29,30),(28,44),(23,49),(18,44)],'ink');p([(19,30),(23,29),(27,31),(26,43),(23,46),(20,42)],'gray_dark' if rear else 'gray')
            l([(21,33),(25,39)],'wood' if rear else 'gold',2)
            l([(37,38),(50,45)],'ink',3);l([(37,38),(50,45)],'wood');p([(49,43),(55,47),(50,48)],'steel_light')
        if role=='shield-captain':r((28,8,34,10),'rust');r((30,5,33,8),'rust');r((27,27,34,28),'gold')
    elif role=='spear':
        if rear:l([(12,15),(18,55)],'ink',3);l([(12,15),(18,55)],'wood_light');p([(10,15),(11,5),(15,13)],'steel_light');r((16,35,19,38),'skin')
        else:l([(48,14),(43,55)],'ink',3);l([(48,14),(43,55)],'wood_light');p([(46,14),(50,5),(51,15)],'steel_light');r((43,35,46,38),'skin')
    elif role=='cleric':
        sx=15 if rear else 47;l([(sx,20),(sx,55)],'ink',3);l([(sx,20),(sx,54)],'wood');l([(sx-4,20),(sx-4,15),(sx+2,15),(sx+2,20)],'steel_dark');r((sx-3,20,sx+3,30),'ink');r((sx-2,22,sx+2,28),'gold');r((sx,23,sx+1,26),'linen');r((sx-3,21,sx+3,22),'steel');l([(sx-3,29),(sx+3,29)],'steel');r((sx-1,36,sx+2,38),'skin')
    elif role=='archer':
        p([(29,24),(35,23),(40,42),(33,44)],'ink');p([(31,25),(34,25),(37,40),(34,41)],'wood');l([(32,27),(29,17)],'wood_light');l([(34,26),(32,16)],'wood_light');r((28,16,30,20),'linen');r((31,15,33,19),'linen')
        l([(16,21),(12,27),(11,36),(14,45)],'ink',3);l([(16,21),(13,28),(12,36),(14,45)],'wood_light');l([(16,22),(18,33),(14,44)],'gray_light');r((16,34,19,37),'skin')
    return im


def role_image(role, rear=False):
    if not rear and role=='shield-captain':
        im=initial_front('shield');d=ImageDraw.Draw(im)
        d.rectangle((28,8,34,10),fill=PAL['rust']);d.rectangle((30,5,33,8),fill=PAL['rust'])
        d.rectangle((27,27,34,28),fill=PAL['gold']);return im
    if not rear and role in ['flag-bearer','shield','archer']:return initial_front(role)
    return constructed_role(role,rear)


def diagnostic_output():
    parser = argparse.ArgumentParser(description='C01 role generation/verification; optional diagnostic destination.')
    parser.add_argument('--out', type=Path, default=Path(tempfile.gettempdir()) / 'empire-c01-role-previews')
    output = parser.parse_args().out.expanduser().resolve()
    if output.is_relative_to(ROOT.parents[2].resolve()):
        parser.error('--out must be outside the worktree; diagnostics are not runtime assets')
    output.mkdir(parents=True, exist_ok=True)
    return output


def main():
    QA = diagnostic_output()
    manifest={'schema':2,'scope':'C01 miniature local prototype only','frameSize':[64,64],'footAnchor':[32,56],
              'textureFilter':'nearest','alpha':'binary','state':'native-pixel original two-facing idle prototypes; no combat-animation approval',
              'facings':['front-right','back-left'],'unitRoles':UNIT_ROLES,'roles':{}}
    preview=Image.new('RGBA',(64*len(ROLES),128),(98,107,98,255))
    for i,role in enumerate(ROLES):
        pair=Image.new('RGBA',(128,64)); bounds=[]
        for facing in range(2):
            image=role_image(role,bool(facing));assert image.size==(64,64)
            assert set(image.getchannel('A').tobytes())=={0,255}
            assert all(image.getpixel(pt)[3]==0 for pt in [(0,y) for y in range(64)]+[(63,y) for y in range(64)]+[(x,0) for x in range(64)]+[(x,63) for x in range(64)])
            bounds.append(list(image.getbbox()));pair.alpha_composite(image,(64*facing,0));preview.alpha_composite(image,(64*i,64*facing))
        path=ROOT/(role+'-views.png');pair.save(path)
        manifest['roles'][role]={'viewsTexture':'res://assets/c01-miniature-roles/'+path.name,'idleRegion':[0,0,64,64],'frontRegion':[0,0,64,64],'backRegion':[64,0,64,64],'opaqueBounds':bounds,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    preview.save(QA/'roles-native.png');preview.resize((1152,256),Image.Resampling.NEAREST).save(QA/'roles-2x.png')
    print('Generated 9 C01 roles x 2 independent idle facings; alpha/edge checks passed.')

if __name__=='__main__':main()

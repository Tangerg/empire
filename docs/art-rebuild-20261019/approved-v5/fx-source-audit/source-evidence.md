
## packages/story-candidate-01/assets/final-fantasy-v1/tools/build_final_fantasy_v1.py

SHA-256: b533ecffd7628905c744d9ddcfa40c93f08e76800b2c4427ee7d1ca5d9d74bd7

98: 
99: 
100: def grid_cell(image: Image.Image, cols: int, rows: int, col: int, row: int, inset: int = 4) -> Image.Image:
101:     x0 = round(col * image.width / cols) + inset
102:     x1 = round((col + 1) * image.width / cols) - inset
103:     y0 = round(row * image.height / rows) + inset
104:     y1 = round((row + 1) * image.height / rows) - inset
105:     cell = image.crop((x0, y0, x1, y1))
106:     if cell.mode == "RGBA":
107:         cell = clear_white_edge_gutter(cell)
108:     return cell
109: 
110: 
111: def clear_white_edge_gutter(image: Image.Image) -> Image.Image:
112:     """Flood-remove near-white grid gutters without erasing isolated white art."""
113:     result = image.copy().convert("RGBA")
114:     px = result.load()
115:     width, height = result.size
116:     queue = []
117:     seen = set()

141:         subprocess.run([
142:             "python3", str(helper), "--input", str(source), "--out", str(out),
143:             "--auto-key", "border", "--soft-matte", "--transparent-threshold", "14",
144:             "--opaque-threshold", "90", "--despill", "--force",
145:         ], check=True, stdout=subprocess.DEVNULL)
146:     image = Image.open(out).convert("RGBA")
147:     # ImageGen sometimes leaves a saturated magenta fringe or a magenta island
148:     # behind white cell gutters. Remove it explicitly after the border-key pass.
149:     pixels = []
150:     for r, g, b, a in image.get_flattened_data():
151:         if r > 225 and b > 175 and g < 105 and a > 0:
152:             pixels.append((r, g, b, 0))
153:         else:
154:             pixels.append((r, g, b, a))
155:     image.putdata(pixels)
156:     return image
157: 
158: 
159: def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
160:     alpha = image.getchannel("A").point(lambda v: 255 if v >= 28 else 0)
161:     box = alpha.getbbox()
162:     if box is None:
163:         raise ValueError("empty alpha cell")
164:     return box
165: 
166: 
167: def fit_transparent(image: Image.Image, size: tuple[int, int], pad: int = 2, bottom: bool = False) -> Image.Image:
168:     crop = image.crop(alpha_bbox(image))
169:     scale = min((size[0] - pad * 2) / crop.width, (size[1] - pad * 2) / crop.height)
170:     target = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
171:     crop = crop.resize(target, RESAMPLE)
172:     canvas = Image.new("RGBA", size, (0, 0, 0, 0))
173:     x = (size[0] - crop.width) // 2
174:     y = size[1] - pad - crop.height if bottom else (size[1] - crop.height) // 2
175:     canvas.alpha_composite(crop, (x, y))
176:     return canvas
177: 
178: 
179: def fit_opaque(image: Image.Image, size: tuple[int, int]) -> Image.Image:
180:     return ImageOps.fit(image.convert("RGB"), size, method=RESAMPLE, centering=(0.5, 0.5))
181: 
182: 
183: def horizontal(frames: list[Image.Image]) -> Image.Image:
184:     result = Image.new("RGBA", (sum(i.width for i in frames), max(i.height for i in frames)), (0, 0, 0, 0))

352: def build_fx(topics: dict[str, list[dict]], assets: list[dict]) -> None:
353:     rows: list[list[Image.Image]] = []
354:     for i in range(1, 7):
355:         source = MASTERS / "fx" / f"fx-atlas-{i:02d}.png"
356:         im = chroma(source)
357:         for r in range(4): rows.append([grid_cell(im, 4, 4, c, r, 5) for c in range(4)])
358:     assert len(rows) == 24
359:     for topic, row in zip(topics["fx"], rows):
360:         frames = [fit_transparent(c, (32, 32), 1, False) for c in row]
361:         assets.append(write_asset(horizontal(frames), "fx", topic, {
362:             "frameWidth": 32, "frameHeight": 32, "frames": 4,
363:             "fps": 12, "loop": topic["id"] in {"C01-FX-09", "C01-FX-10", "C01-FX-11", "C01-FX-12"},
364:             "blendMode": "normal", "anchor": [16, 24],
365:         }))


## packages/story-candidate-01/assets/final-fantasy-v1/tools/build_tactical_runtime_hd.py

SHA-256: 0da696023ff7e02e5ec2878144e68353f38d62807fcd18ac99821e36f40f304d

179:         frames = [base.fit_transparent(cell, (64, 64), 2, False) for cell in row]
180:         write_asset(base.horizontal(frames), "fx", topic, assets)
181: 
182: 
183: def build_environment(assets: list[dict]) -> None:
184:     source = Image.open(base.MASTERS / "terrain" / "terrain-atlas-capital-silverwood.png").convert("RGB")
185:     # The source board has a dark framing gutter; a generous inset prevents a
186:     # doubled horizontal seam after mirror-tiling.
187:     forest_floor = base.grid_cell(source, 4, 2, 0, 1, 24).convert("RGB")
188:     forest_floor = ImageEnhance.Color(forest_floor).enhance(0.78)


## packages/story-candidate-01/src/presentation/candidate-01-bindings.ts

SHA-256: e02f438cd0d07d99f1a3c798e2ecb1a359aa945a08d1344c592a5e92a2e5aad0

153: export const CANDIDATE_01_WEAPON_FX: Readonly<Partial<Record<WeaponId, string>>> = {
154:   'c01.border-blade': 'C01-FX-01',
155:   'c01.gray-oath': 'C01-FX-06',
156:   'c01.guard-spear': 'C01-FX-02',
157:   'c01.legion-spear': 'C01-FX-02',
158:   'c01.knight-lance': 'C01-FX-02',
159:   'c01.knight-charge': 'C01-FX-07',
160:   'c01.longbow': 'C01-FX-03',
161:   'c01.ranger-bow': 'C01-FX-03',
162:   'c01.lantern-staff': 'C01-FX-04',
163:   'c01.oath-dispel': 'C01-FX-06',
164:   'c01.engineer-hammer': 'C01-FX-04',
165:   'c01.satchel-charge': 'C01-FX-07',
166:   'c01.rune-hammer': 'C01-FX-04',
167:   'c01.forge-burst': 'C01-FX-07',
168:   'c01.wolf-spear': 'C01-FX-02',
169:   'c01.undead-blade': 'C01-FX-01',
170:   'c01.ghost-touch': 'C01-FX-08',
171:   'c01.inquisitor-flame': 'C01-FX-06',
172:   'c01.golem-slam': 'C01-FX-04',
173:   'c01.ballista': 'C01-FX-03',
174:   'c01.oath-bolt': 'C01-FX-06',
175:   'c01.dragon-breath': 'C01-FX-05',
176:   'c01.colossus-sweep': 'C01-FX-08',
177: };


## packages/story-candidate-01/src/presentation/candidate-01-runtime.ts

SHA-256: 2450dbd03bacf71ed8b758f09d11774e4f274a75e3d3ad26c6b39043e3f48aa0

227: export function candidate01WeaponFxTopic(weapon: WeaponId): string | null {
228:   return CANDIDATE_01_WEAPON_FX[weapon] ?? null;
229: }
230: 
231: /**
232:  * One effect: a strip, drawn about its own origin, already running.
233:  *
234:  * The board plays this the moment it appears and takes it away when it is done, so
235:  * `playing` is the whole of its animation policy — there is nobody to tell it to
236:  * start. It used to be a self-describing `<image>` whose clips the renderer found
237:  * by class name and read out of a JSON attribute, then played whichever came first.
238:  */
239: export function candidate01FxPicture(topic: string): BoardPicture {
240:   const record = candidate01Asset(topic);
241:   const frameWidth = record.frameWidth ?? 32;
242:   const frameHeight = record.frameHeight ?? 32;
243:   const frames = record.frames ?? 1;
244:   return {
245:     body: '',
246:     strip: {
247:       href: record.url,
248:       frameWidth,
249:       frameHeight,
250:       frameCount: frames,
251:       x: -frameWidth / 2,
252:       y: -frameHeight / 2,
253:       clips: [{
254:         id: 'effect',
255:         frames: Array.from({ length: frames }, (_, frame) => frame),
256:         fps: record.fps ?? 12,
257:         loop: record.loop ?? false,
258:       }],
259:       playing: 'effect',
260:     },
261:   };


## packages/story-candidate-01/src/presentation/index.ts

SHA-256: 8614f871aee8fbbf59e4317617effbd2e27c720c02bdb287202290420a2a720a

92:   sceneLayers: candidate01MapSceneryLayers,
93:   structure: candidate01StructurePicture,
94:   marker: candidate01MarkerMarkup,
95:   weaponFx: candidate01WeaponFxTopic,
96:   effect: candidate01FxPicture,
97:   healFx: 'C01-FX-17',
98: });


## packages/story-candidate-01/src/presentation/candidate-01-assets.ts

SHA-256: 821dacd8f867d227bc8a2839b1a85f5869be7b9cf1193c82796386d0676930fa

179: const runtimeFiles = import.meta.glob<string>(
180:   [
181:     '../../assets/final-fantasy-v1/runtime/**/*.png',
182:     '!../../assets/final-fantasy-v1/runtime/{combat-unit,mission-unit,terrain,interactive-structure,battle-prop,fx}/*.png',
183:   ],
184:   { eager: true, import: 'default', query: '?url' },
185: );
186: const runtimeHdFiles = import.meta.glob<string>(
187:   [
188:     '../../assets/final-fantasy-v1/runtime-hd/**/*.png',
189:     '../../assets/final-fantasy-v1/runtime-approved-v5/**/*@2x.png',
190:   ],
191:   { eager: true, import: 'default', query: '?url' },
192: );
193: const packRoot = '../../assets/final-fantasy-v1/';
194: const runtimeHdTopics = readCandidate01HdManifest(runtimeHdManifestJson);
195: 
196: function resolveRuntimeUrl(record: Candidate01AssetRecord): string {
197:   const hd = runtimeHdTopics.get(record.topicId);
198:   if (hd) {
199:     validateCandidate01HdAsset(record, hd);
200:     const hdUrl = runtimeHdFiles[`${packRoot}${hd.png}`];
201:     if (!hdUrl) throw new Error(`candidate-01 runtime HD asset is missing from the Vite graph: ${record.topicId}`);
202:     return hdUrl;
203:   }
204:   const url = runtimeFiles[`${packRoot}${record.png}`];
205:   if (!url) throw new Error(`candidate-01 asset is missing from the Vite graph: ${record.topicId} (${record.png})`);


## packages/game-ui/src/ui/board.ts

SHA-256: 7211c79358e4f6d1422fa8ef84df5b2b2706d89bb05563ff47b054c3b5901ad1

611:   async animateHit(at: Coord, damage: number, killed: boolean, weapon?: WeaponId): Promise<void> {
612:     const fx = weapon ? this.presentation.weaponFx(weapon) : null;
613:     // Drawn about its own origin and then placed, like everything else on the board.
614:     // These used to bake the cell's scene coordinates into the markup, which is the
615:     // one thing left that a renderer could not treat as a picture at a position: it
616:     // would have had to rasterise a field-sized image to hold a damage number.
617:     // The pack's picture, with the board's own parts drawn over it.
618:     const art = fx ? this.presentation.effect(fx) : null;
619:     const drawing = this.surface.effect({
620:       body: art?.body ?? '',
621:       strip: art?.strip,
622:       parts: [
623:         { role: 'burst', markup: '<circle r="12" fill="#ffffff" opacity="0.65"/>' },
624:         { role: 'number', markup: `<text y="-8" text-anchor="middle" class="fx-damage">-${damage}</text>` },
625:       ],
626:     });
627:     const { x: cx, y: cy } = this.centre(at);
628:     drawing.place(cx, cy);
629:     const burst = drawing.part('burst');
630:     const number = drawing.part('number');
631:     if (!await this.timeline.tween(420, (t) => {
632:       // 12 → 22 was written as a radius; as a swell it is the same picture and the
633:       // one property a backend without shapes can also honour.
634:       burst?.swell(1 + t * 0.83);
635:       burst?.opacity(0.8 * (1 - t));
636:       number?.nudge(0, -14 * t);
637:       number?.opacity(1 - t ** 2);
638:     })) return;
639:     drawing.remove();
640:     if (killed) await this.timeline.wait(40);
641:   }
642: 
643:   async animateDeath(unitId: number): Promise<void> {
644:     const drawing = this.surface.drawnUnit(unitId);
645:     if (!drawing) return;

653:   async animateHeal(at: Coord, amount: number): Promise<void> {
654:     const art = this.presentation.healFx ? this.presentation.effect(this.presentation.healFx) : null;
655:     const drawing = this.surface.effect({
656:       body: art?.body ?? '',
657:       strip: art?.strip,
658:       parts: [{ role: 'number', markup: `<text y="-6" text-anchor="middle" class="fx-heal">+${amount}</text>` }],
659:     });
660:     const { x: cx, y: cy } = this.centre(at);
661:     drawing.place(cx, cy);
662:     const number = drawing.part('number');
663:     if (!await this.timeline.tween(500, (t) => {
664:       number?.nudge(0, -16 * t);
665:       number?.opacity(1 - t ** 2);
666:     })) return;
667:     drawing.remove();
668:   }
669: 


## packages/game-ui/src/ui/event-presentation.ts

SHA-256: 2b906c70b5fdef28ea6368f3c236ddfde37ebd88e094f36a1671083cf2457f3b

150: /** Wind up, land, and report — the shape every direct strike shares. */
151: async function playStrike(
152:   stage: BattleStage,
153:   strike: { attacker?: number; at: Coord | null; damage: number; fatal: boolean; weapon?: string },
154: ): Promise<void> {
155:   const { at } = strike;
156:   const attacker = strike.attacker === undefined ? null : stage.unit(strike.attacker);
157:   if (attacker) {
158:     await stage.board.animateStrike(attacker, at ?? { x: attacker.x, y: attacker.y });
159:   }
160:   if (at) await stage.board.animateHit(at, strike.damage, strike.fatal, strike.weapon);
161: }

281:   .register(presenter({
282:     type: 'heal',
283:     animate: async (stage, event) => {
284:       const target = stage.unit(event.target);
285:       if (target) await stage.board.animateHeal({ x: target.x, y: target.y }, event.amount);
286:     },
287:     describe: ({ unitName }, event) => `${unitName(event.source)} 治疗了 ${event.amount} 点生命`,


## packages/game-ui/src/art/board-surface.ts

SHA-256: 333a0d0112bea724ae2a60c9e3e765f562b67238604f60f114a459721adac412

329:  */
330: export interface BoardStrip {
331:   /** The image the frames are cut from, laid out left to right. */
332:   readonly href: string;
333:   /** One frame's size, in scene units. */
334:   readonly frameWidth: number;
335:   readonly frameHeight: number;
336:   readonly frameCount: number;
337:   /** Where the frame window's top-left corner sits in the picture. */
338:   readonly x: number;
339:   readonly y: number;
340:   /**
341:    * A colour this sheet is multiplied by, or absent for the sheet as drawn.
342:    *
343:    * The campaign's units are one grey army: which side a figure belongs to was
344:    * readable only from the ring under its feet, so from any distance the board was
345:    * a crowd. Multiplying the sheet by a lightened team colour makes an army read as
346:    * blue or red the way a painted map does, without a second sheet per faction.
347:    *
348:    * On the strip rather than in the body markup, because both backends have to
349:    * honour it: the DOM one filters the image, the GPU one tints the sprite, and
350:    * multiply is the one operation that means the same thing in both.
351:    */
352:   readonly tint?: string | undefined;
353:   /**
354:    * Every clip that may be played on it, by name.
355:    *
356:    * A unit's strip is played by `BoardView` through the three names a battle has
357:    * for what a body is doing — `idle`, `walk` and `attack` — so a strip drawn for a
358:    * unit declares those. Playing a name that is not here is a defect and throws.
359:    */
360:   readonly clips: readonly FrameAnimationClip[];
361:   /**
362:    * A clip that is already playing when the picture is drawn.
363:    *
364:    * An explosion animates because it exists, not because anything told it to. The
365:    * SVG backend used to spell that as "register every strip you can find in this
366:    * subtree, then play whichever clip happens to come first in the JSON" — a guess,
367:    * and not one a second backend could be held to.
368:    */
369:   readonly playing?: string;
370: }
371: 
372: /** The window a strip shows one frame through, in the image's own coordinates. */

405:   `<svg class="board-strip" x="${strip.x}" y="${strip.y}"`
406:   + ` width="${strip.frameWidth}" height="${strip.frameHeight}"`
407:   + ` viewBox="${boardStripWindow(strip, frame)}" overflow="hidden">`
408:   + (strip.tint ? `<defs>${tintFilter(strip.tint)}</defs>` : '')
409:   + `<image href="${escapeAttr(strip.href)}" width="${strip.frameWidth * strip.frameCount}"`
410:   + ` height="${strip.frameHeight}" preserveAspectRatio="none"`
411:   + (strip.tint ? ` filter="url(#${tintId(strip.tint)})"` : '')
412:   + `/></svg>`;
413: 
414: /**


## packages/game-ui/src/art/pixi-board-surface.ts

SHA-256: 8d786726a1a66c24fb2f8f07d76cbc7f185d04b6aaf658370e58727cbd359519

269:    */
270:   private draw(strip: BoardStrip): void {
271:     const sprite = new Sprite(Texture.EMPTY);
272:     sprite.position.set(strip.x, strip.y);
273:     // The same multiply the DOM backend writes as a filter. Pixi's `tint` is a
274:     // per-sprite multiply, so the shared frame textures stay shared.
275:     if (strip.tint) sprite.tint = strip.tint;
276:     this.figure.addChild(sprite);
277:     this.selectionSource = sprite;
278:     this.syncSelectionCue();
279: 
280:     let frames: readonly Texture[] = [];
281:     let wanted = 0;
282:     const show = (frame: number): void => {
283:       sprite.texture = frames[frame];
284:       sprite.setSize(strip.frameWidth, strip.frameHeight);
285:       this.syncSelectionCue();


## packages/game-ui/src/art/picture-textures.ts

SHA-256: 2e95cc3c362893c4b4226875efe62488682be3becda7f53950954869cab1afeb

137:   /**
138:    * The frames of one strip, sharing the image's single texture.
139:    *
140:    * Cut in the image's own pixels rather than in scene units: a sheet is drawn into
141:    * a frame-sized box with `preserveAspectRatio="none"`, so nothing guarantees that
142:    * one source pixel is one scene unit, and the sprite is sized by whoever draws it.
143:    */
144:   private async slice(strip: BoardStrip): Promise<readonly Texture[]> {
145:     const image = new Image();
146:     image.src = strip.href;
147:     await image.decode();
148:     const source = Texture.from(image).source;
149:     // Pixel art, presented at whatever size the board is: no smoothing between frames.
150:     source.scaleMode = 'nearest';
151:     const width = image.naturalWidth / strip.frameCount;
152:     return Array.from({ length: strip.frameCount }, (_, frame) => new Texture({
153:       source,
154:       frame: new Rectangle(frame * width, 0, width, image.naturalHeight),
155:     }));


## packages/game-ui/src/art/frame-animation.ts

SHA-256: e3627a8a65da408bee3f3a4e5a788ce70ed3f000c3851caec2dbbc3c500000b9

214: 
215:   private readonly tick = (now: number): void => {
216:     this.requestHandle = null;
217:     for (const track of this.tracks.values()) {
218:       const clip = track.clip;
219:       if (!clip || clip.frames.length < 2) continue;
220:       const elapsedFrames = Math.max(0, Math.floor(((now - track.startedAt) * clip.fps) / 1000));
221:       if (!clip.loop && elapsedFrames >= clip.frames.length) {
222:         this.applyFrame(track, clip.frames[clip.frames.length - 1]);
223:         track.clip = null;
224:         continue;
225:       }
226:       const index = clip.loop ? elapsedFrames % clip.frames.length : elapsedFrames;
227:       this.applyFrame(track, clip.frames[index]);
228:     }
229:     if (this.hasRunningTimeline()) this.ensureLoop();


## tools/art/export_frame_overrides.py

SHA-256: a933452e588fd456bd0fee3d49ef9d43babdb4244961943bf57029f0824ec1e0

8: from PIL import Image
9: from export_overrides import ROOT, read_json, within, validate_alpha, records
10: 
11: CATALOG = Path('tools/art/frame-overrides.json')
12: ORDERS = {
13:     'combat-unit': ['idle', 'walk-a', 'attack', 'walk-b'],
14:     'mission-unit': ['idle', 'walk-a', 'carry-or-work', 'walk-b'],
15:     'interactive-structure': ['normal', 'damaged', 'captured'],
16: }
17: 
18: 
19: def plan(root, catalog):
20:     data = read_json(catalog)
21:     if set(data) != {'schemaVersion', 'assets'} or data['schemaVersion'] != 1 or not isinstance(data['assets'], dict):
22:         raise ValueError('expected schemaVersion1 and assets object')
23:     known = records(root)
24:     planned = []
25:     targets = set()
26:     for topic, override in data['assets'].items():
27:         if topic not in known:
28:             raise ValueError('unknown topic')
29:         pack, asset, hd = known[topic]
30:         category = asset['category']
31:         if category not in ORDERS:
32:             raise ValueError('unsupported framed category')
33:         order = ORDERS[category]
34:         fw, fh = asset['frameWidth'], asset['frameHeight']
35:         if (fw, fh) not in ((32, 48), (64, 48), (64, 64), (96, 64)):
36:             raise ValueError('unsupported frame dimensions')
37:         if category == 'interactive-structure' and (fw, fh) != (64, 64):

60:             if not path.is_relative_to((pack / 'masters').resolve()):
61:                 raise ValueError('source must be in own pack masters')
62:             raw = path.read_bytes()
63:             if hashlib.sha256(raw).hexdigest() != entry['sha256']:
64:                 raise ValueError('source SHA-256 mismatch')
65:             with Image.open(io.BytesIO(raw)) as opened:
66:                 if opened.format != 'PNG' or opened.mode != 'RGBA':
67:                     raise ValueError('frame must be PNG RGBA')
68:                 source = opened.copy()
69:             alpha = validate_alpha(source, name)
70:             if source_size is None:
71:                 source_size = source.size
72:             if source.size != source_size:
73:                 raise ValueError('all poses must share authored canvas dimensions')
74:             pixel_digest = hashlib.sha256(source.tobytes()).hexdigest()
75:             if pixel_digest in hashes:
76:                 raise ValueError('duplicate decoded source poses; meaningful distinct poses required')
77:             hashes.add(pixel_digest)
78:             sources.append(source)


## tools/art/export_registered_sequence.py

SHA-256: 91c8e3943b62d35689025ac478c88f670fad29f88c4f216ceafdb9b13453d29d

12: def validate(config):
13:     names=list(config['frames'])
14:     if names not in [['idle','walk-a','attack','walk-b'],['idle','walk-a','carry-or-work','walk-b'],['normal','damaged','captured']]:
15:         raise ValueError('explicit semantic frame order required')
16:     size=None;hashes=[]
17:     for path in config['frames'].values():
18:         image=Image.open(path);alpha_report(image)
19:         tolerance=config.get('canvasPaddingTolerance',0)
20:         if tolerance not in (0,1,2):raise ValueError('only explicit up-to-two-pixel canvas padding is supported')
21:         if size and any(abs(size[i]-image.size[i])>tolerance for i in (0,1)):raise ValueError('shared authored canvas required; no independent fit')
22:         size=tuple(max(size[i],image.size[i]) for i in (0,1)) if size else image.size;hashes.append(hashlib.sha256(Path(path).read_bytes()).hexdigest())
23:     if len(set(hashes))!=len(hashes):raise ValueError('duplicate source frames')
24:     scale=config['scale'];origin=config['origin'];frame=config['frameSize']


## tools/art/publish_registered_sequence.py

SHA-256: ce067284c5360e4a8ac40d729a4733aed94aad5c4bb1923ca092b094900df3b2

28:     sequence = json.loads((export_dir / 'sequence.json').read_text())
29:     config = sequence['config']
30:     expected = record.get('frameOrder', record.get('stateOrder'))
31:     if list(config['frames']) != expected or len(expected) != record['frames']:
32:         raise ValueError('existing semantic order must remain unchanged')
33:     width, height = config['frameSize']
34:     pivot = sequence['logicalPivot']
35:     if record['category'] not in ('combat-unit', 'mission-unit', 'interactive-structure'):
36:         raise ValueError('only established sequence categories are supported')
37:     base = f"runtime-approved-v5/{record['category']}/{Path(record['png']).stem}"
38:     sources = []


## tools/art/export_overrides.py

SHA-256: 1e025037b5f3d02b7138e5790497a465b180ec25b7405084ab537294aaffd7a6

45: def validate_alpha(image: Image.Image, label: str):
46:     if image.mode != 'RGBA':
47:         raise ValueError(f'{label}: expected RGBA, got {image.mode}')
48:     alpha = image.getchannel('A')
49:     low, high = alpha.getextrema()
50:     if low != 0 or high < 240:
51:         raise ValueError(f'{label}: expected transparent exterior and near-opaque subject')
52:     w, h = image.size
53:     edge_pixels = [alpha.getpixel((x, y)) for y in (0, h - 1) for x in range(w)]
54:     edge_pixels += [alpha.getpixel((x, y)) for x in (0, w - 1) for y in range(1, h - 1)]
55:     edge_max = max(edge_pixels)
56:     # One 8-bit alpha step changes a composite channel by at most one level.
57:     # Preserve that source residue; stronger edge opacity needs an art review.
58:     if edge_max > 1:
59:         raise ValueError(f'{label}: outer edge alpha {edge_max} exceeds 1; subject or shadow may be clipped')
60:     return {
61:         'edgeMaxAlpha': edge_max,
62:         'edgeNonzeroPixels': sum(value > 0 for value in edge_pixels),
63:         'coreBoundingBox': list(alpha.point(lambda value: 255 if value >= 240 else 0).getbbox()),
64:     }
65: 
66: 

238:             'pixelDensity': density,
239:         }))
240:     return result
241: 
242: 
243: def plan(root: Path, catalog: Path):
244:     data = read_json(catalog)
245:     if data.get('schemaVersion') != 1 or not isinstance(data.get('assets'), dict):
246:         raise ValueError('expected schemaVersion 1 and assets object')
247:     known = records(root)
248:     planned = []
249:     targets = set()
250:     for topic, override in data['assets'].items():
251:         if topic not in known:
252:             raise ValueError(f'unknown topic: {topic}')
253:         pack, asset, hd = known[topic]
254:         category = asset['category']
255:         contracts = {'battle-prop': 64, 'equipment': 48, 'skill': 48, 'status': 32, 'hud': 32, 'faction-kit': 128}
256:         if category == 'narrative-static':
257:             if asset.get('kind') == 'scene':
258:                 exports = render_scene(root, pack, asset, hd, override)
259:             else:
260:                 rendered, provenance = compose_character_card(root, pack, asset, hd, override)
261:                 exports = [(asset, 1, rendered, provenance)]
262:         elif category not in contracts or asset.get('frames') or asset.get('variants'):
263:             raise ValueError(f'{topic}: only static battle-prop/equipment/skill/status/hud/faction-kit exports supported')
264:         else:
265:             side = contracts[category]
266:             anchor = asset.get('anchor')


## tools/art/assemble_approved_slice.py

SHA-256: d2127c7207d25b05487a16fb5c2fa8daeed0b8134378779e76cc35f129979b95

15: def alpha_report(image):
16:     if image.mode != 'RGBA':
17:         raise ValueError('A genuine RGBA source is required')
18:     alpha = image.getchannel('A')
19:     histogram = alpha.histogram()
20:     edge = max(max(alpha.crop(box).getdata()) for box in
21:         [(0,0,image.width,1),(0,image.height-1,image.width,image.height),
22:          (0,0,1,image.height),(image.width-1,0,image.width,image.height)])
23:     if not histogram[0] or sum(histogram[240:]) == 0 or edge > 1:
24:         raise ValueError(f'Invalid source transparency: perimeter maximum {edge}')
25:     return {'alphaRange':alpha.getextrema(),'transparentPixels':histogram[0],
26:         'nearOpaquePixels':sum(histogram[240:]),'edgeMax':edge,
27:         'visibleBounds':alpha.point(lambda value:255 if value >= 128 else 0).getbbox()}
28: 


## packages/story-candidate-01/src/units.ts

SHA-256: 5aec43c370aebde9587f6ef7ea564215806df9ce60a76939528e968a069ba36c

20: /** Named characters and faction troops used by the first three chapters. */
21: export const CANDIDATE_01_UNITS: readonly UnitDef[] = [
22:   unit({
23:     id: 'c01.laiya', name: '莱娅', weapons: ['c01.border-blade', 'c01.gray-oath'], value: 420,
24:     maxHp: 116, defense: 0.15, movement: 4, abilities: ['attack', 'capture', 'wait'],
25:     defaultReaction: 'support', formations: ['formation-defensive', 'formation-line', 'formation-loose'],

64:   unit({ id: 'c01.swordsman', name: '边境剑士', weapons: ['c01.border-blade'], value: 120, maxHp: 98, defense: 0.08, movement: 3, formations: ['formation-line', 'formation-loose'], tags: ['infantry'], blurb: '可靠的近战与占领骨干。' }),
65:   unit({ id: 'c01.banner-guard', name: '灰旗卫士', weapons: ['c01.guard-spear'], value: 170, maxHp: 112, defense: 0.14, movement: 3, defaultReaction: 'guard', formations: ['formation-defensive', 'formation-line'], tags: ['infantry', 'guard'], blurb: '以援护和阵线保护灰旗同伴。' }),
66:   unit({ id: 'c01.archer', name: '边境弓手', weapons: ['c01.ranger-bow'], value: 160, maxHp: 78, movement: 3, armorClass: 'unarmored', tags: ['infantry', 'ranged'], blurb: '适应丘陵与林地的轻装射手。' }),
67:   unit({ id: 'c01.knight', name: '王国骑士', weapons: ['c01.knight-lance', 'c01.knight-charge'], value: 340, maxHp: 116, defense: 0.17, movement: 6, movementClass: 'mounted', armorClass: 'heavy', abilities: ['attack', 'wait'], tags: ['cavalry'], blurb: '长距离冲锋的重装骑兵。' }),
68:   unit({ id: 'c01.legion-shield', name: '军团盾卫', weapons: ['c01.legion-spear'], value: 230, maxHp: 120, defense: 0.2, movement: 3, armorClass: 'heavy', defaultReaction: 'guard', formations: ['formation-defensive', 'formation-line'], tags: ['infantry', 'guard'], blurb: '帝国军团的稳固盾墙。' }),
69:   unit({ id: 'c01.gravekeeper', name: '守墓人', weapons: ['c01.lantern-staff', 'c01.oath-dispel'], value: 260, maxHp: 72, movement: 3, armorClass: 'unarmored', abilities: ['heal', 'attack', 'capture', 'wait'], tags: ['infantry', 'support', 'arcane', 'gravekeeper'], blurb: '治疗盟友并压制誓文亡灵。' }),


## packages/story-candidate-01/src/levels.ts

SHA-256: bedff69291689e17c7af3d11b0b6e66472e85bf04399920c2bec67497444e2ad

343: const ally = (key: string, unit: string): UnitSeed => ({ key, unit });
344: const foe = (key: string, unit: string): UnitSeed => ({ key, unit, owner: 2, facing: 'west' });
345: 
346: const PARTY = {
347:   laiya: hero('laiya', 'c01.laiya'),
348:   torren: hero('torren', 'c01.swordsman'),
349:   elin: hero('elin', 'c01.archer'),
350:   bran: hero('bran', 'c01.bran'),
351:   mirelle: hero('mirelle', 'c01.mirelle'),

401: const baseParty = (...extra: UnitSeed[]): UnitSeed[] => [
402:   { ...PARTY.laiya, formation: 'formation-defensive' },
403:   PARTY.torren,
404:   PARTY.elin,
405:   ...extra,
406: ];
407: 
408: const BATTLES: readonly BattleSpec[] = [
409:   {
410:     order: 1, chapter: 1, title: '双子丘陵', subtitle: '第一次指挥：夺取高地，也承担冒进的后果。', map: TWIN_HILLS,
411:     player: baseParty(
412:       ally('field-cleric', 'c01.gravekeeper'), ally('roderick-reserve', 'c01.roderick'),
413:       ally('ridge-scout', 'c01.archer'), ally('ridge-guard', 'c01.banner-guard'),
414:       ally('west-spear', 'c01.banner-guard'), ally('village-runner', 'c01.archer'),
415:       ally('camp-guard', 'c01.legion-shield'), ally('field-knight', 'c01.knight'),
416:     ),
417:     enemy: [
418:       foe('vanguard-1', 'c01.legion-shield'), foe('vanguard-2', 'c01.swordsman'), foe('vanguard-archer', 'c01.archer'), foe('vanguard-cain', 'c01.cain'),
419:       foe('north-shield', 'c01.legion-shield'), foe('south-sword', 'c01.swordsman'), foe('south-bow', 'c01.archer'),
420:       foe('camp-bow', 'c01.archer'),
421:     ],
422:     deployment: deployment(
423:       '3,9 5,8 3,11 2,10 4,11 5,5 4,6 1,9 7,8 4,7 6,11',
424:       '17,6 20,7 21,4 24,7 13,5 22,10 20,12 25,5',
425:     ),


## docs/story-candidates/candidate-01/art-assets/07-topic-catalog.md

SHA-256: 6c3fa34dfbe5a8df87a2b2f52f9b38c26e6299e44bcfe2ff563a884d1e27aac5

424: 
425: 本节列出《断冠之誓》的全部特效题材：
426: 
427: - `C01-FX-01`：斩击
428: - `C01-FX-02`：穿刺
429: - `C01-FX-03`：箭矢命中
430: - `C01-FX-04`：钝击
431: - `C01-FX-05`：普通火焰
432: - `C01-FX-06`：琥珀誓火
433: - `C01-FX-07`：攻城碎屑
434: - `C01-FX-08`：亡者记忆冲击
435: - `C01-FX-09`：秋雨
436: - `C01-FX-10`：河流浅水
437: - `C01-FX-11`：荒原强风
438: - `C01-FX-12`：山炉热浪
439: - `C01-FX-13`：焦土余火
440: - `C01-FX-14`：墓园污染
441: - `C01-FX-15`：银林灵火
442: - `C01-FX-16`：七塔灰光
443: - `C01-FX-17`：治疗
444: - `C01-FX-18`：受控
445: - `C01-FX-19`：隐蔽
446: - `C01-FX-20`：援护盾
447: - `C01-FX-21`：结构受损
448: - `C01-FX-22`：工程修复
449: - `C01-FX-23`：塔网失联
450: - `C01-FX-24`：据点占领

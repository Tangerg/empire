import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = dirname(fileURLToPath(import.meta.url));

const R = (x, y, w, h, fill, opacity = 1, extra = '') =>
  `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${fill}"${opacity === 1 ? '' : ` opacity="${opacity}"`}${extra}/>`;
const G = (body, extra = '') => `<g${extra ? ` ${extra}` : ''}>${body}</g>`;
const P = (points, fill, opacity = 1) =>
  `<polygon points="${points}" fill="${fill}"${opacity === 1 ? '' : ` opacity="${opacity}"`}/>`;
const L = (x1, y1, x2, y2, stroke, width = 1, opacity = 1) =>
  `<path d="M${x1} ${y1}L${x2} ${y2}" stroke="${stroke}" stroke-width="${width}"${opacity === 1 ? '' : ` opacity="${opacity}"`} fill="none"/>`;

function svg(viewW, viewH, width, height, label, body, defs = '') {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${viewW} ${viewH}" role="img" aria-label="${label}" shape-rendering="crispEdges" style="image-rendering:pixelated">
${defs ? `<defs>${defs}</defs>` : ''}
${body}
</svg>\n`;
}

function write(rel, content) {
  const target = join(ROOT, rel);
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, content);
  return rel;
}

const THEMES = {
  oath: {
    sky: '#303746', sky2: '#414a5a', far: '#5a6570', ink: '#171b23',
    stone: '#6f747c', stoneHi: '#9ca0a5', ground: '#4b433e', light: '#d5a14d',
    red: '#824344', blue: '#3e5872', gray: '#7b7d80', cloth: '#c2b8a7', green: '#51684d',
  },
  stars: {
    sky: '#252b45', sky2: '#42445e', far: '#665b70', ink: '#171827',
    stone: '#687b86', stoneHi: '#9bb4b7', ground: '#9a4f3f', light: '#69d8d2',
    red: '#c65b62', blue: '#3e7e91', gray: '#7f8797', cloth: '#dfc68e', green: '#5d906d',
  },
  grain: {
    sky: '#3d4554', sky2: '#59616b', far: '#777568', ink: '#1e1c1b',
    stone: '#706b61', stoneHi: '#a29a87', ground: '#5c4637', light: '#d69a4e',
    red: '#8d3e34', blue: '#334f5c', gray: '#77746b', cloth: '#bda57b', green: '#566b50',
  },
};

const CAST = {
  oath: [
    { id: 'laiya-18', label: '莱娅·维恩，18 岁边境见习旗官', skin: '#c98e70', skinHi: '#e0aa86', hair: '#6b3d35', uniform: '#3e5872', accent: '#7b7d80', metal: '#9ca0a5', hairStyle: 'braid', gear: 'flag' },
    { id: 'roderick', label: '罗德里克·赫恩，洛恩老骑士', skin: '#b9785d', skinHi: '#d39a78', hair: '#b9b6ac', uniform: '#4c5970', accent: '#d5a14d', metal: '#a9adb1', hairStyle: 'beard', gear: 'sword' },
    { id: 'kain', label: '凯恩·阿尔德，维尔萨青年将领', skin: '#c68767', skinHi: '#dda17e', hair: '#2a2529', uniform: '#824344', accent: '#d5a14d', metal: '#858b94', hairStyle: 'crop', gear: 'shield' },
    { id: 'mirelle', label: '米蕾尔·索恩，圣辉守墓人', skin: '#d2a07e', skinHi: '#e4b692', hair: '#8b765f', uniform: '#6a6577', accent: '#c2b8a7', metal: '#8e8b91', hairStyle: 'hood', gear: 'lantern' },
    { id: 'bran', label: '布兰·洛克，灰境猎人与斥候', skin: '#ad7057', skinHi: '#ce8f6d', hair: '#342b26', uniform: '#405447', accent: '#7b7d80', metal: '#8d9298', hairStyle: 'undercut', gear: 'crossbow' },
    { id: 'tasha', label: '塔莎·莫恩，自由佣兵团首领', skin: '#bf8061', skinHi: '#d99f78', hair: '#583632', uniform: '#786046', accent: '#824344', metal: '#8f9293', hairStyle: 'pony', gear: 'crossbow' },
    { id: 'sevien', label: '摄政王塞维恩，秩序理念的政治对手', skin: '#b8755d', skinHi: '#d09774', hair: '#4a4644', uniform: '#352f40', accent: '#d5a14d', metal: '#858891', hairStyle: 'crop', gear: 'sword' },
    { id: 'audren', label: '大祭司奥德伦，圣辉教廷领袖', skin: '#c28c70', skinHi: '#ddaa88', hair: '#d2c9b5', uniform: '#6a6577', accent: '#d5a14d', metal: '#9a9690', hairStyle: 'hood', gear: 'lantern' },
  ],
  stars: [
    { id: 'mira', label: '米拉·诺恩，赫沙见习巡猎士', skin: '#b8795e', skinHi: '#d99b76', hair: '#7b3e36', uniform: '#c39a62', accent: '#69d8d2', metal: '#657783', hairStyle: 'pony', gear: 'bladegun' },
    { id: 'roan', label: '罗安·塞里斯，巡猎队长', skin: '#aa705e', skinHi: '#c98e74', hair: '#8f9394', uniform: '#31576a', accent: '#d9d4bd', metal: '#657783', hairStyle: 'beard', gear: 'rifle' },
    { id: 'naim', label: '奈姆·阿尔卡，返星会指挥官', skin: '#9f6653', skinHi: '#c48668', hair: '#30262d', uniform: '#8c4655', accent: '#69d8d2', metal: '#687b86', hairStyle: 'undercut', gear: 'charge' },
    { id: 'talos-7', label: '塔洛斯七号，古代维护机器人', skin: '#768a91', skinHi: '#a4b9b7', hair: '#3c4b57', uniform: '#526774', accent: '#69d8d2', metal: '#9bb4b7', hairStyle: 'robot', gear: 'cannon' },
    { id: 'olo', label: '奥洛·汐文，涅瑞亚潮汐祭司', skin: '#9e6856', skinHi: '#c28669', hair: '#23434b', uniform: '#3e7e91', accent: '#c7d9c7', metal: '#647f88', hairStyle: 'topknot', gear: 'pole' },
    { id: 'kota', label: '珂塔，维尔达共生护林者', skin: '#9a765b', skinHi: '#bea07a', hair: '#395842', uniform: '#5d906d', accent: '#c65b62', metal: '#718876', hairStyle: 'braid', gear: 'vine' },
    { id: 'helo', label: '赫洛·班，远灯号船长', skin: '#a66a55', skinHi: '#c8886b', hair: '#574941', uniform: '#6a5a4f', accent: '#69d8d2', metal: '#657783', hairStyle: 'beard', gear: 'rifle' },
    { id: 'ishan', label: '伊珊·维洛，环庭数据分析员', skin: '#c58b6c', skinHi: '#dda987', hair: '#332e43', uniform: '#465b73', accent: '#69d8d2', metal: '#728794', hairStyle: 'bun', gear: 'charge' },
  ],
  grain: [
    { id: 'shen-li-22', label: '沈砺，22 岁河工与粮运差役', skin: '#b77b5e', skinHi: '#d19a75', hair: '#292523', uniform: '#6f5940', accent: '#334f5c', metal: '#7b756b', hairStyle: 'headcloth', gear: 'pole' },
    { id: 'lu-qinghe', label: '陆青禾，药铺账房与军医', skin: '#c28a69', skinHi: '#dca783', hair: '#242222', uniform: '#334f5c', accent: '#87966b', metal: '#6f6a61', hairStyle: 'bun', gear: 'ledger' },
    { id: 'han-yue', label: '韩岳，铁匠出身的重装先锋', skin: '#a96850', skinHi: '#c98765', hair: '#302522', uniform: '#7b4336', accent: '#bda57b', metal: '#77746b', hairStyle: 'headband', gear: 'hammer' },
    { id: 'pei-zhao', label: '裴昭，关陇门阀骑军将领', skin: '#bd8162', skinHi: '#d69d78', hair: '#252425', uniform: '#46566b', accent: '#8d3e34', metal: '#827d72', hairStyle: 'topknot', gear: 'spear' },
    { id: 'jiang-zhaoye', label: '江照夜，云梦水盟舰队主将', skin: '#b7755b', skinHi: '#d0916f', hair: '#24272b', uniform: '#2f5e68', accent: '#8d3e34', metal: '#77746b', hairStyle: 'pony', gear: 'spear' },
    { id: 'xiao-shen', label: '萧慎，律令军师与密察官', skin: '#ba8064', skinHi: '#d49d7a', hair: '#2b2928', uniform: '#45483f', accent: '#8d3e34', metal: '#706b61', hairStyle: 'topknot', gear: 'ledger' },
    { id: 'aletan', label: '阿勒坦，大朔草原轻骑将领', skin: '#9d644e', skinHi: '#bd8160', hair: '#292322', uniform: '#53614c', accent: '#bda57b', metal: '#7d786e', hairStyle: 'braid', gear: 'bow' },
    { id: 'wen-su', label: '闻素，随军史官', skin: '#c78d6c', skinHi: '#dfaa84', hair: '#252322', uniform: '#665b4e', accent: '#334f5c', metal: '#6f6a61', hairStyle: 'bun', gear: 'ledger' },
  ],
};

function motif(themeKey, t) {
  if (themeKey === 'oath') {
    return R(2, 2, 1, 7, t.light, .65) + R(3, 3, 1, 1, t.light) + R(1, 5, 3, 1, t.light, .45) +
      R(20, 3, 2, 1, t.stoneHi, .35) + R(19, 5, 3, 1, t.stoneHi, .25);
  }
  if (themeKey === 'stars') {
    return R(2, 2, 5, 1, t.light, .55) + R(2, 2, 1, 5, t.light, .55) + R(6, 2, 1, 5, t.light, .55) +
      R(3, 3, 3, 1, t.light, .3) + R(4, 4, 1, 2, t.light, .3) + R(20, 3, 1, 1, '#f3dca7', .7);
  }
  return R(2, 2, 4, 1, '#eee3c7', .55) + R(2, 3, 1, 4, '#eee3c7', .55) + R(5, 3, 1, 4, '#eee3c7', .55) +
    R(3, 7, 2, 1, t.light, .45) + R(19, 2, 2, 6, t.ink, .18);
}

function hairPixels(p) {
  const h = p.hair;
  switch (p.hairStyle) {
    case 'braid': return R(7, 6, 10, 3, h) + R(7, 8, 2, 7, h) + R(16, 7, 2, 6, h) + R(17, 12, 2, 8, h) + R(18, 19, 1, 3, h);
    case 'beard': return R(7, 5, 10, 3, h) + R(6, 7, 2, 5, h) + R(16, 7, 2, 5, h) + R(8, 13, 8, 3, h) + R(9, 16, 6, 2, h);
    case 'crop': return R(7, 5, 10, 4, h) + R(6, 7, 2, 4, h) + R(16, 7, 2, 3, h) + R(8, 5, 2, 1, p.skinHi);
    case 'hood': return R(6, 4, 12, 3, p.accent) + R(5, 6, 3, 11, p.accent) + R(16, 6, 3, 11, p.accent) + R(7, 5, 10, 2, h);
    case 'pony': return R(7, 5, 10, 3, h) + R(6, 7, 2, 6, h) + R(16, 6, 2, 5, h) + R(17, 8, 2, 8, h) + R(18, 14, 2, 4, h);
    case 'undercut': return R(7, 5, 10, 3, h) + R(6, 7, 3, 2, h) + R(15, 6, 3, 5, h) + R(7, 5, 3, 1, p.skinHi);
    case 'robot': return R(7, 5, 10, 3, p.metal) + R(6, 7, 2, 7, p.hair) + R(16, 7, 2, 7, p.hair) + R(9, 4, 6, 1, p.accent) + R(10, 8, 4, 1, p.accent);
    case 'headcloth': return R(7, 5, 10, 3, h) + R(6, 7, 12, 2, p.accent) + R(6, 9, 2, 5, h) + R(16, 8, 2, 4, h) + R(17, 9, 2, 6, p.accent);
    case 'bun': return R(8, 5, 9, 3, h) + R(6, 7, 3, 7, h) + R(16, 7, 2, 7, h) + R(11, 3, 5, 3, h) + R(12, 2, 3, 1, h);
    case 'headband': return R(7, 5, 10, 3, h) + R(6, 7, 12, 2, p.accent) + R(6, 9, 2, 5, h) + R(16, 8, 2, 4, h);
    case 'topknot': return R(7, 5, 10, 3, h) + R(6, 7, 2, 6, h) + R(16, 7, 2, 5, h) + R(11, 2, 4, 3, h) + R(12, 1, 2, 1, h);
    default: return R(7, 5, 10, 3, h);
  }
}

function portrait(themeKey, p) {
  const t = THEMES[themeKey];
  let body = R(0, 0, 48, 56, t.sky) + R(0, 31, 48, 25, t.sky2) + G(motif(themeKey, t), 'transform="scale(2)"');
  body += P('1,56 3,48 9,42 16,38 21,36 29,36 36,39 43,44 47,51 47,56', t.ink);
  body += P('4,56 6,47 15,40 22,38 29,38 39,44 44,51 45,56', p.uniform);
  body += P('4,56 6,48 14,42 18,42 13,56', p.metal, .85) + P('35,42 41,47 45,56 34,56 30,39', p.metal, .72);
  body += R(18, 32, 12, 8, p.skin) + R(20, 34, 8, 5, p.skinHi) + P('15,38 24,44 33,38 36,43 28,49 19,49 12,43', p.accent);
  body += R(17, 43, 14, 12, p.uniform) + R(23, 42, 3, 14, p.accent) + R(18, 46, 2, 8, p.metal, .7) + R(29, 46, 2, 8, t.ink, .28);

  if (p.hairStyle === 'robot') {
    body += P('13,12 17,7 33,7 37,12 37,28 32,35 17,35 12,28', p.hair);
    body += R(15, 11, 20, 17, p.metal) + R(17, 13, 16, 3, p.skinHi) + R(18, 18, 12, 3, p.accent) + R(20, 25, 8, 2, t.ink) + R(14, 28, 4, 5, p.hair) + R(32, 28, 4, 5, p.hair);
  } else {
    body += R(13, 15, 3, 14, p.skin) + R(34, 15, 3, 14, p.skin) + P('15,10 20,7 31,7 35,11 37,18 35,29 30,35 20,35 15,30 13,20', p.skin);
    body += P('16,12 20,9 31,9 34,13 34,26 31,32 27,35 27,13', '#925d4d', .34);
    body += R(17, 15, 7, 2, '#7e4d42', .35) + R(27, 15, 6, 2, '#7e4d42', .35);
    body += R(18, 19, 4, 2, t.ink) + R(29, 19, 4, 2, t.ink) + R(19, 19, 1, 1, '#efe0c4') + R(30, 19, 1, 1, '#efe0c4');
    body += R(24, 21, 2, 5, '#a66752', .75) + R(25, 26, 3, 1, p.skinHi) + R(21, 29, 8, 2, '#78483f') + R(23, 29, 4, 1, '#c77c68');
    switch (p.hairStyle) {
      case 'braid':
        body += P('12,15 14,9 19,5 31,5 36,10 38,18 35,20 34,14 29,11 23,12 17,10 16,20 12,22', p.hair) + R(35, 20, 5, 17, p.hair) + R(38, 34, 4, 5, p.hair) + R(39, 39, 3, 5, p.hair) + R(16, 9, 9, 3, p.skinHi, .16); break;
      case 'beard':
        body += P('12,16 14,9 20,5 31,5 36,10 38,18 34,17 32,11 17,11 15,18', p.hair) + P('15,27 19,32 24,36 30,33 35,27 33,35 28,40 21,38 16,34', p.hair) + R(17, 8, 11, 2, p.skinHi, .2); break;
      case 'crop':
        body += P('13,17 14,10 19,6 32,6 36,10 37,17 33,16 32,11 17,11 16,18', p.hair) + R(17, 7, 10, 2, p.skinHi, .13); break;
      case 'hood':
        body += P('10,18 12,9 19,4 31,4 38,10 40,23 37,36 33,34 35,17 31,10 19,10 15,17 16,34 11,36', p.accent) + R(16, 9, 18, 4, p.hair) + R(12, 25, 4, 13, t.ink, .25); break;
      case 'pony':
        body += P('12,18 14,9 20,5 32,6 37,12 38,20 34,18 33,11 17,11 16,22 12,23', p.hair) + R(35, 16, 6, 15, p.hair) + R(39, 27, 5, 8, p.hair) + R(16, 8, 9, 2, p.skinHi, .15); break;
      case 'undercut':
        body += P('13,15 15,9 21,5 34,7 37,13 37,21 33,19 32,11 22,10 17,14 16,21 12,22', p.hair) + R(14, 10, 6, 3, p.skinHi, .32); break;
      case 'headcloth':
        body += R(13, 8, 23, 6, p.hair) + R(11, 11, 27, 5, p.accent) + R(13, 16, 4, 12, p.hair) + R(34, 15, 4, 9, p.hair) + R(37, 14, 4, 17, p.accent) + R(39, 29, 3, 8, p.accent); break;
      case 'bun':
        body += P('12,18 14,10 20,6 33,7 37,12 38,20 34,18 33,11 18,11 16,22 12,24', p.hair) + R(23, 2, 10, 5, p.hair) + R(25, 1, 6, 2, p.hair) + R(29, 3, 3, 2, p.skinHi, .13); break;
      case 'headband':
        body += P('12,18 14,9 20,5 32,6 37,11 38,20 34,18 33,12 17,12 16,22 12,23', p.hair) + R(12, 11, 26, 4, p.accent) + R(35, 14, 6, 3, p.accent) + R(39, 16, 4, 2, p.accent); break;
      case 'topknot':
        body += P('12,18 14,10 20,6 33,7 37,12 38,20 34,18 33,11 18,11 16,22 12,24', p.hair) + R(22, 2, 9, 5, p.hair) + R(24, 0, 5, 3, p.hair) + R(23, 7, 8, 2, p.accent); break;
    }
  }

  switch (p.gear) {
    case 'flag': body += R(42, 6, 2, 49, '#544536') + R(44, 7, 4, 2, t.gray) + R(44, 9, 4, 10, t.gray) + R(44, 17, 3, 3, '#66686c'); break;
    case 'sword': body += R(40, 11, 2, 43, p.metal) + R(38, 16, 6, 2, t.light) + R(41, 8, 1, 4, '#e4e1d7'); break;
    case 'shield': body += P('2,39 12,36 15,43 13,55 4,55 1,48', p.accent) + P('4,41 10,39 12,44 10,52 5,53 3,48', p.uniform) + R(6, 43, 2, 8, t.light, .55); break;
    case 'lantern': body += R(39, 36, 7, 12, t.light, .85) + R(40, 33, 5, 3, p.metal) + R(40, 48, 5, 2, p.metal) + R(41, 38, 3, 7, '#f2d58c'); break;
    case 'bladegun': body += P('38,37 42,36 47,44 46,50 42,49 39,42', p.metal) + R(42, 40, 5, 3, p.accent) + R(5, 33, 11, 2, p.accent); break;
    case 'rifle': body += R(39, 15, 4, 40, p.metal) + R(42, 22, 6, 4, p.accent) + R(40, 16, 2, 15, p.skinHi, .35); break;
    case 'charge': body += R(2, 40, 11, 11, p.metal) + R(4, 42, 6, 5, p.accent) + R(42, 8, 2, 11, p.accent) + R(44, 9, 2, 2, '#d7f2eb'); break;
    case 'crossbow': body += R(38, 40, 10, 3, '#59402d') + R(42, 34, 2, 20, '#59402d') + P('37,35 43,40 48,35 47,39 43,44 38,39', p.metal) + R(42, 31, 1, 10, p.accent); break;
    case 'vine': body += R(38, 39, 3, 16, p.uniform) + R(41, 40, 6, 3, p.accent) + R(40, 36, 3, 5, t.green) + R(44, 33, 3, 5, t.green) + R(45, 42, 2, 6, t.green); break;
    case 'bow': body += R(41, 13, 2, 42, '#604430') + R(38, 17, 2, 8, '#8a6845') + R(44, 21, 2, 10, '#8a6845') + L(39, 17, 45, 31, '#d9c59a', 1) + R(34, 38, 7, 12, '#6c5138'); break;
    case 'cannon': body += R(37, 35, 11, 10, p.metal) + R(43, 38, 5, 4, p.accent) + R(38, 45, 4, 9, p.hair); break;
    case 'pole': body += R(42, 8, 2, 47, '#5c432e') + R(40, 8, 6, 2, p.metal) + R(42, 5, 2, 4, p.metal); break;
    case 'ledger': body += R(2, 39, 12, 15, '#8a6845') + R(4, 41, 8, 11, '#c4aa7d') + R(6, 42, 1, 9, p.accent) + R(8, 44, 3, 1, t.ink, .5) + R(8, 47, 3, 1, t.ink, .5); break;
    case 'hammer': body += R(40, 17, 3, 38, '#604430') + R(35, 12, 13, 7, p.metal) + R(37, 13, 8, 2, p.skinHi, .18); break;
    case 'spear': body += R(42, 7, 2, 48, '#604430') + P('39,7 43,0 47,7', p.metal) + R(42, 3, 2, 4, p.skinHi, .28); break;
  }
  for (const [x, y] of [[7,50],[10,46],[34,48],[38,52],[15,52]]) body += R(x, y, 1, 1, p.accent, .55);
  body += R(0, 0, 48, 2, t.ink) + R(0, 54, 48, 2, t.ink) + R(0, 0, 2, 56, t.ink) + R(46, 0, 2, 56, t.ink);
  return svg(48, 56, 96, 112, p.label, body);
}

function unitFrame(p, t, frame) {
  const ox = frame * 24;
  const bob = frame === 1 || frame === 3 ? 0 : 1;
  const footA = frame === 1 ? 2 : frame === 3 ? -2 : 0;
  let b = R(ox + 4, 29, 16, 2, t.ink, .3);
  if (p.hairStyle === 'robot') {
    b += P(`${ox+8},5 ${ox+11},3 ${ox+16},4 ${ox+18},7 ${ox+17},14 ${ox+9},14 ${ox+7},11`, p.metal) + R(ox + 9, 7 + bob, 7, 2, p.accent) + R(ox + 7, 14 + bob, 11, 11, p.uniform) + R(ox + 5, 15 + bob, 3, 10, p.metal) + R(ox + 18, 15 + bob, 3, 10, p.metal);
  } else {
    b += R(ox + 9, 6 + bob, 8, 8, p.skin) + R(ox + 8, 5 + bob, 10, 4, p.hair) + R(ox + 8, 8 + bob, 2, 7, p.hair) + R(ox + 10, 9 + bob, 2, 1, t.ink) + R(ox + 15, 9 + bob, 1, 1, t.ink) + R(ox + 8, 14 + bob, 11, 11, p.uniform) + R(ox + 6, 16 + bob, 3, 9, p.accent) + R(ox + 19, 16 + bob, 3, 9, p.metal);
  }
  b += R(ox + 10 + footA, 24 + bob, 3, 6 - bob, t.ink) + R(ox + 15 - footA, 24 + bob, 3, 6 - bob, t.ink) + R(ox + 9 + footA, 29, 5, 2, p.metal) + R(ox + 14 - footA, 29, 5, 2, p.metal);
  b += R(ox + 10, 16 + bob, 1, 7, p.skinHi, .35) + R(ox + 15, 17 + bob, 1, 6, t.ink, .28);
  if (['flag', 'pole', 'spear', 'rifle'].includes(p.gear)) b += R(ox + 21, 5, 2, 25, p.metal);
  if (p.gear === 'flag') b += R(ox + 17, 5, 5, 8, p.accent) + R(ox + 18, 6, 4, 1, t.stoneHi, .35);
  if (p.gear === 'hammer') b += R(ox + 18, 7, 6, 5, p.metal) + R(ox + 20, 11, 2, 17, '#604430');
  if (p.gear === 'charge') b += R(ox + 2, 17, 5, 7, p.metal) + R(ox + 4, 19, 2, 2, p.accent);
  if (p.gear === 'crossbow') b += R(ox + 17, 16, 7, 2, '#59402d') + R(ox + 20, 12, 2, 12, '#59402d') + R(ox + 18, 13, 5, 1, p.metal);
  if (p.gear === 'vine') b += R(ox + 18, 15, 3, 10, t.green) + R(ox + 20, 13, 3, 4, p.accent) + R(ox + 21, 10, 2, 4, t.green);
  if (p.gear === 'bow') b += R(ox + 21, 7, 2, 21, '#604430') + R(ox + 19, 10, 2, 5, '#8a6845') + R(ox + 22, 16, 2, 6, '#8a6845');
  if (p.gear === 'ledger') b += R(ox + 2, 16, 6, 8, '#9b754b') + R(ox + 4, 18, 3, 1, t.ink, .5);
  if (p.gear === 'shield') b += P(`${ox+2},16 ${ox+8},14 ${ox+9},25 ${ox+4},28 ${ox+1},23`, p.accent) + R(ox + 4, 18, 2, 6, t.light, .5);
  if (p.gear === 'cannon') b += R(ox + 18, 12, 6, 5, p.metal) + R(ox + 21, 14, 3, 2, p.accent);
  if (p.gear === 'bladegun') b += R(ox + 18, 16, 6, 3, p.metal) + R(ox + 22, 17, 2, 1, p.accent);
  if (p.gear === 'lantern') b += R(ox + 3, 19, 4, 5, t.light) + R(ox + 4, 18, 2, 1, p.metal);
  return b;
}

function unitSheet(themeKey, p) {
  const t = THEMES[themeKey];
  let body = '';
  for (let frame = 0; frame < 4; frame++) body += unitFrame(p, t, frame);
  return svg(96, 32, 192, 64, `${p.label}四帧战斗单位图集`, body);
}

function tower(t, variant) {
  let b = R(0, 0, 32, 32, 'none');
  if (variant === 'oath') {
    b += R(9, 27, 14, 3, t.ground) + R(11, 10, 10, 18, t.stone) + R(9, 8, 14, 4, t.stoneHi) + R(12, 4, 8, 5, t.stone) + R(14, 1, 4, 4, t.stoneHi);
    b += R(15, 4, 2, 20, t.ink, .35) + R(14, 11, 4, 8, t.light) + R(15, 12, 2, 6, '#f2d58c') + R(8, 7, 3, 2, t.light) + R(21, 7, 3, 2, t.light);
  } else if (variant === 'rain') {
    b += R(7, 28, 18, 2, t.ground) + R(14, 6, 4, 22, t.stone) + R(10, 5, 12, 3, t.stoneHi) + R(8, 3, 3, 4, t.stone) + R(21, 3, 3, 4, t.stone);
    b += R(15, 8, 2, 17, t.light, .65) + R(6, 8, 20, 1, t.ink, .5) + R(9, 11, 14, 1, t.ink, .35) + R(11, 14, 10, 1, t.ink, .25);
    b += R(13, 1, 6, 1, t.light) + R(13, 1, 1, 4, t.light) + R(18, 1, 1, 4, t.light) + R(15, 2, 2, 1, t.light);
  } else {
    b += R(4, 28, 24, 2, t.ground) + R(5, 16, 22, 12, '#5a4635') + R(3, 13, 26, 4, '#2d2926') + R(6, 10, 20, 4, '#6c4334') + R(9, 7, 14, 4, '#8d3e34');
    b += R(8, 18, 5, 10, '#8b765b') + R(19, 18, 5, 10, '#8b765b') + R(14, 20, 4, 8, t.ink) + R(6, 20, 2, 3, t.light, .7) + R(24, 20, 2, 3, t.light, .7);
  }
  return b;
}

function architectureDetail(variant, t) {
  let b = R(8, 59, 48, 2, t.ink, .28) + R(12, 61, 40, 1, t.ink, .14);
  if (['oath-tower', 'seven-citadel', 'archive', 'ring-node', 'rain-tower'].includes(variant)) {
    b += R(24, 31, 7, 1, t.stoneHi, .5) + R(37, 39, 4, 1, t.ink, .35) + R(21, 47, 5, 1, t.ink, .28) + R(31, 53, 6, 1, t.stoneHi, .3);
  }
  if (['granary', 'white-camp', 'linchuan', 'water-fort'].includes(variant)) {
    for (const x of [10, 18, 26, 34, 42, 50]) b += R(x, 25 + (x % 3), 5, 1, '#b27b55', .28);
    b += R(17, 43, 1, 9, '#d0b68b', .3) + R(45, 41, 1, 11, t.ink, .22);
  }
  if (variant === 'tree-city') {
    b += R(19, 20, 1, 13, '#8b6540', .55) + R(38, 16, 1, 10, '#88a477', .7) + R(25, 39, 14, 1, t.light, .45);
  }
  if (variant === 'farlight') {
    b += R(18, 33, 3, 2, t.light) + R(26, 33, 3, 2, t.light) + R(34, 33, 3, 2, t.light) + R(42, 33, 3, 2, t.light) + R(26, 23, 10, 1, '#d7e4df', .55);
  }
  return b;
}

function architecture(themeKey, id, label, variant) {
  const t = THEMES[themeKey];
  let b = R(0, 0, 32, 32, 'none');
  switch (variant) {
    case 'oath-tower': b += tower(t, 'oath'); break;
    case 'gray-camp':
      b += R(2, 27, 28, 3, t.ground) + P('5,25 12,13 20,25', '#77746f') + P('12,25 19,11 28,25', '#63656b') + R(12, 16, 1, 11, '#46392d') + R(13, 16, 5, 4, t.gray) + R(22, 15, 1, 12, '#46392d') + R(23, 15, 4, 3, t.gray) + R(5, 25, 23, 2, t.ink, .25); break;
    case 'tree-city':
      b += R(2, 28, 28, 2, t.ground) + R(13, 5, 7, 24, '#59412f') + R(8, 10, 6, 18, '#6b4c32') + R(19, 12, 5, 16, '#493527') + R(5, 5, 10, 7, t.green) + R(12, 2, 12, 8, '#65805e') + R(19, 7, 10, 8, t.green) + R(8, 17, 15, 2, t.stoneHi) + R(11, 15, 3, 5, t.light, .55); break;
    case 'seven-citadel':
      b += R(2, 28, 28, 2, t.ground) + R(7, 13, 18, 15, t.stone) + R(3, 17, 6, 11, t.stoneHi) + R(23, 17, 6, 11, t.stoneHi) + R(12, 7, 8, 21, '#5e626b') + R(15, 3, 2, 24, t.light) + R(14, 2, 4, 2, '#f0cf7c') + R(12, 19, 8, 9, t.ink); break;
    case 'rain-tower': b += tower(t, 'rain'); break;
    case 'farlight':
      b += R(3, 25, 26, 4, t.ink, .25) + R(5, 15, 22, 10, '#5f6f7d') + R(8, 11, 15, 5, '#7c929b') + R(12, 7, 7, 5, '#42586b') + R(2, 18, 5, 4, '#4c6577') + R(27, 18, 3, 3, '#4c6577') + R(9, 16, 2, 2, t.light) + R(13, 16, 2, 2, t.light) + R(17, 16, 2, 2, t.light) + R(21, 16, 2, 2, t.light) + R(12, 25, 3, 3, '#c65b62') + R(18, 25, 3, 3, '#c65b62'); break;
    case 'archive':
      b += R(3, 28, 26, 2, t.ground) + R(6, 12, 20, 16, '#6f8189') + R(9, 8, 14, 5, '#9bb4b7') + R(12, 4, 8, 5, '#657783') + R(15, 2, 2, 3, t.light) + R(10, 17, 12, 2, '#dce5db') + R(13, 20, 6, 8, t.ink) + R(5, 10, 2, 18, '#e7edf0', .3) + R(25, 10, 2, 18, '#e7edf0', .3); break;
    case 'ring-node':
      b += R(3, 27, 26, 3, t.ink, .25) + R(5, 15, 22, 12, '#596c78') + R(7, 12, 18, 4, '#7d9098') + R(10, 9, 12, 4, '#445666') + R(14, 4, 4, 6, t.stoneHi) + R(15, 5, 2, 20, t.light, .65) + R(6, 18, 20, 1, t.light, .4) + R(9, 21, 14, 1, t.light, .3); break;
    case 'granary': b += tower(t, 'granary'); break;
    case 'white-camp':
      b += R(2, 27, 28, 3, t.ground) + P('4,26 11,15 18,26', '#6e5a43') + P('14,26 22,13 29,26', '#465562') + R(8, 21, 2, 6, '#eee3c7') + R(7, 20, 4, 2, '#eee3c7') + R(24, 17, 1, 10, '#483529') + R(25, 17, 3, 3, '#eee3c7') + R(3, 25, 26, 2, t.ink, .25); break;
    case 'linchuan':
      b += R(3, 28, 26, 2, t.ground) + R(5, 15, 22, 13, '#836d50') + R(3, 12, 26, 4, '#2c2a28') + R(6, 9, 20, 4, '#6e4638') + R(8, 18, 4, 10, '#a29174') + R(20, 18, 4, 10, '#a29174') + R(14, 20, 4, 8, t.ink) + R(7, 17, 18, 1, t.light, .3); break;
    case 'water-fort':
      b += R(0, 24, 32, 8, '#3d6170') + R(3, 22, 26, 2, '#6f5940') + R(7, 12, 18, 10, '#675646') + R(5, 10, 22, 3, '#2f2a27') + R(10, 7, 12, 4, '#6e4638') + R(14, 15, 4, 7, t.ink) + R(3, 18, 2, 9, '#55402e') + R(27, 18, 2, 9, '#55402e') + R(1, 27, 7, 1, '#6f8991', .7) + R(23, 29, 8, 1, '#6f8991', .7); break;
  }
  return svg(64, 64, 128, 128, label, G(b, 'transform="scale(2)"') + architectureDetail(variant, t));
}

const BUILDINGS = {
  oath: [
    ['redstone-oath-tower', '赤石誓约烽塔', 'oath-tower'], ['grayflag-field-camp', '灰旗军野战营地', 'gray-camp'],
    ['silverwood-tree-city', '银林树城', 'tree-city'], ['astaria-seven-tower-citadel', '阿斯塔里亚七塔王城', 'seven-citadel'],
  ],
  stars: [
    ['zero-rain-tower', '赫沙零号雨塔', 'rain-tower'], ['farlight-ship', '远灯号走私船', 'farlight'],
    ['soler-archive-monastery', '索勒冰月档案修院', 'archive'], ['kaelon-ring-node', '凯隆环都星脉节点', 'ring-node'],
  ],
  grain: [
    ['county-granary', '淮右县仓', 'granary'], ['white-torch-camp', '白炬军行营', 'white-camp'],
    ['linchuan-government-hall', '临川官署与长期基地', 'linchuan'], ['cloud-dream-water-fort', '云梦大泽水寨', 'water-fort'],
  ],
};

function skyBands(t, horizon = 20) {
  return R(0, 0, 64, 7, t.sky) + R(0, 7, 64, 7, t.sky2) + R(0, 14, 64, horizon - 14, t.far);
}
function littleHuman(x, y, colors, pose = 0) {
  return R(x + 1, y, 2, 2, colors.skin) + R(x, y + 2, 4, 4, colors.body) +
    R(x + (pose ? 0 : 1), y + 6, 1, 3, colors.ink) + R(x + (pose ? 3 : 2), y + 6, 1, 3, colors.ink);
}
function hdDefs(t) {
  return `<filter id="far-soft" x="-10%" y="-10%" width="120%" height="120%"><feGaussianBlur stdDeviation="0.35"/></filter><filter id="light-glow" x="-80%" y="-80%" width="260%" height="260%"><feGaussianBlur stdDeviation="1.1"/></filter><linearGradient id="light-falloff" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${t.light}" stop-opacity=".26"/><stop offset="1" stop-color="${t.light}" stop-opacity="0"/></linearGradient>`;
}

function hashText(text) {
  let value = 2166136261;
  for (const ch of text) value = Math.imul(value ^ ch.charCodeAt(0), 16777619) >>> 0;
  return value;
}

function sceneDetail(themeKey, variant, t) {
  let seed = hashText(`${themeKey}:${variant}`);
  const next = () => {
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    return seed / 0xffffffff;
  };
  let b = '';
  const groundColors = themeKey === 'stars' ? ['#d07a58', '#633a37', t.light] : themeKey === 'grain' ? ['#8b6d51', '#312a26', '#b79b70'] : ['#7d7468', '#302e31', t.light];
  for (let i = 0; i < 54; i++) {
    const x = Math.floor(next() * 126) + 1;
    const y = Math.floor(next() * 15) + 56;
    b += R(x, y, next() > .76 ? 2 : 1, 1, groundColors[i % groundColors.length], i % 3 === 2 ? .28 : .45);
  }
  for (let i = 0; i < 12; i++) {
    const x = Math.floor(next() * 120) + 4;
    const y = Math.floor(next() * 24) + 5;
    b += R(x, y, 2 + Math.floor(next() * 6), 1, '#e5ddd0', .08 + next() * .08);
  }
  if (variant === 'gray-flag') {
    b += R(15, 31, 2, 11, '#ca7447', .62) + R(16, 28, 1, 4, '#e4a05c') + R(103, 36, 2, 8, '#be6a43', .55) + R(104, 33, 1, 4, '#e4a05c');
    for (const [x, y] of [[57,48],[61,46],[66,49],[72,47]]) b += R(x, y, 2, 1, '#c1b29f', .35);
  }
  if (variant === 'seven-towers') {
    for (const x of [10, 26, 42, 84, 100, 116]) b += R(x, 31, 3, 1, t.light, .55) + R(x + 1, 35, 1, 4, t.light, .35);
  }
  if (variant === 'no-rain') {
    for (const [x, y, w] of [[5,61,12],[31,66,15],[82,63,13],[105,68,10]]) b += L(x, y, x + w, y - 4, '#5e302b', 1, .8);
    b += R(65, 52, 4, 2, '#f1e3c0') + R(67, 50, 1, 2, '#f1e3c0');
  }
  if (variant === 'black-rain') {
    for (let x = 5; x < 125; x += 6) b += L(x, 5 + (x % 11), x - 2, 17 + (x % 11), '#151722', 1, .7);
    b += R(58, 8, 4, 34, t.light, .12, ' filter="url(#light-glow)"');
  }
  if (variant === 'ring-city') {
    for (const x of [17, 43, 73, 101]) b += R(x, 31, 3, 1, t.light, .8) + R(x + 1, 35, 1, 5, t.light, .45);
  }
  if (variant === 'empty-bowl') {
    b += R(58, 60, 12, 2, '#bda57b') + R(60, 58, 8, 2, '#7f664e') + R(62, 57, 4, 1, '#d8c49c');
  }
  if (variant === 'open-granary') {
    for (const x of [16, 36, 92, 112]) b += R(x, 37, 5, 5, '#f3e6c4') + R(x + 1, 42, 3, 4, t.light, .72);
    for (const [x, y] of [[32,65],[36,63],[40,67],[44,64],[48,66]]) b += R(x, y, 2, 1, '#d1b06d', .85);
  }
  if (variant === 'great-lake') {
    for (const [x, y, w] of [[2,54,22],[42,61,31],[78,56,25],[101,66,20]]) b += R(x, y, w, 1, '#79949b', .45);
    b += R(59, 38, 3, 4, '#e09a55', .8) + R(60, 35, 1, 4, '#f1c477');
  }
  return b;
}

function scene(themeKey, id, label, variant) {
  const t = THEMES[themeKey];
  let bg = skyBands(t), mid = '', front = '', fx = '';
  switch (variant) {
    case 'twin-hills':
      bg += R(42, 4, 6, 6, '#d5b77c') + R(43, 3, 4, 1, '#e3cf9a');
      mid += P('0,25 12,13 25,25', '#586657') + P('16,25 34,11 49,25', '#4d5b50') + P('38,25 54,15 64,25', '#68705d') + R(0, 24, 64, 5, '#59664d');
      front += R(0, 29, 64, 7, '#4a4b3e') + R(26, 24, 12, 4, '#79644b') + R(28, 22, 8, 2, '#8b765d') + littleHuman(14, 23, {skin:'#c98e70', body:t.blue, ink:t.ink}) + littleHuman(45, 23, {skin:'#c68767', body:t.red, ink:t.ink}, 1);
      fx += R(31, 13, 1, 15, t.light, .35);
      break;
    case 'gray-flag':
      bg = R(0, 0, 64, 10, '#29313c') + R(0, 10, 64, 10, '#3e4851') + R(0, 20, 64, 8, '#6d6256');
      mid += R(3, 19, 14, 10, '#473a32') + P('1,19 9,12 19,19', '#211e1d') + R(45, 20, 13, 9, '#493a30') + P('43,20 51,14 61,20', '#292322') + R(7, 15, 2, 7, '#b1623e', .65) + R(51, 17, 2, 6, '#b1623e', .55);
      front += R(0, 28, 64, 8, '#3e3733') + littleHuman(29, 21, {skin:'#c98e70', body:t.blue, ink:t.ink}) + R(33, 8, 1, 22, '#493b2e') + R(34, 8, 10, 7, t.gray) + R(34, 14, 8, 2, '#696b6e');
      fx += R(0, 0, 64, 36, 'url(#light-falloff)', .35) + R(8, 13, 1, 1, '#e09e5a', .6) + R(52, 14, 1, 1, '#e09e5a', .6);
      break;
    case 'seven-towers':
      bg = R(0, 0, 64, 9, '#20232f') + R(0, 9, 64, 10, '#343846') + R(0, 19, 64, 8, '#56515b');
      mid += R(25, 4, 14, 23, '#555b65') + R(28, 1, 8, 26, '#676d76') + R(31, 0, 2, 25, t.light, .55);
      for (const x of [4, 12, 20, 42, 50, 58]) mid += R(x, 10 + (x % 3), 4, 17, '#4a4f59') + R(x + 1, 7 + (x % 3), 2, 20, '#686d75') + R(x + 1, 12, 2, 6, t.light, .35);
      front += R(0, 27, 64, 9, '#292830') + P('0,36 20,25 34,36', '#34323a') + P('30,36 48,24 64,36', '#2d2c34');
      fx += R(30, 0, 4, 30, t.light, .16, ' filter="url(#light-glow)"');
      break;
    case 'no-rain':
      bg = R(0, 0, 64, 9, '#b26850') + R(0, 9, 64, 10, '#cf8860') + R(0, 19, 64, 7, '#d5a16b') + R(48, 3, 7, 7, '#f1d58c');
      mid += P('0,26 12,17 24,26', '#a15542') + P('17,26 36,15 50,26', '#91483b') + R(8, 17, 4, 12, '#647985') + R(5, 15, 10, 3, '#8aa2a5') + R(9, 9, 2, 18, t.light, .55) + R(6, 12, 8, 1, t.ink, .35);
      front += R(0, 27, 64, 9, '#8f4737') + L(2, 30, 8, 29, '#5f342d') + L(14, 32, 22, 29, '#5f342d') + L(43, 34, 52, 30, '#5f342d') + littleHuman(28, 23, {skin:'#b8795e', body:'#c39a62', ink:t.ink}) + R(34, 26, 2, 2, t.light);
      fx += R(9, 6, 2, 17, t.light, .13, ' filter="url(#light-glow)"');
      break;
    case 'black-rain':
      bg = R(0, 0, 64, 10, '#25293b') + R(0, 10, 64, 10, '#454355') + R(0, 20, 64, 7, '#6d5660');
      mid += R(27, 6, 6, 22, '#526976') + R(23, 5, 14, 3, '#82989a') + R(29, 2, 2, 25, t.light, .6) + R(20, 13, 20, 1, t.ink, .5);
      front += R(0, 27, 64, 9, '#783d38') + littleHuman(17, 22, {skin:'#b8795e', body:'#c39a62', ink:t.ink}) + littleHuman(43, 22, {skin:'#9f6653', body:'#8c4655', ink:t.ink}, 1);
      for (let x = 3; x < 63; x += 5) fx += L(x, 3 + (x % 7), x - 1, 8 + (x % 7), '#1b1b28', 1, .75);
      break;
    case 'ring-city':
      bg = R(0, 0, 64, 8, '#1e2337') + R(0, 8, 64, 11, '#34364f') + R(0, 19, 64, 8, '#5d5368') + R(4, 3, 2, 2, '#d7d8bf') + R(53, 5, 1, 1, '#d7d8bf');
      mid += R(8, 12, 8, 16, '#4e6170') + R(19, 8, 10, 20, '#657984') + R(34, 11, 8, 17, '#4d6471') + R(47, 6, 9, 22, '#6d7e84') + R(0, 17, 64, 2, t.light, .45) + R(7, 15, 50, 1, t.light, .25);
      front += R(0, 28, 64, 8, '#232637') + R(7, 29, 50, 1, t.light, .3) + littleHuman(28, 23, {skin:'#b8795e', body:'#c39a62', ink:t.ink});
      fx += R(0, 16, 64, 4, t.light, .12, ' filter="url(#light-glow)"');
      break;
    case 'empty-bowl':
      bg = R(0, 0, 64, 10, '#4d5661') + R(0, 10, 64, 9, '#687078') + R(0, 19, 64, 7, '#8a8170');
      mid += R(4, 15, 14, 12, '#59483a') + P('2,15 11,10 20,15', '#302b29') + R(46, 16, 13, 11, '#62503e') + P('44,16 52,11 61,16', '#342d29') + R(23, 19, 18, 8, '#715940');
      front += R(0, 27, 64, 9, '#574334') + littleHuman(10, 22, {skin:'#b77b5e', body:'#6f5940', ink:t.ink}) + littleHuman(46, 22, {skin:'#c28a69', body:'#334f5c', ink:t.ink}) + R(29, 29, 6, 2, '#bda57b') + R(30, 28, 4, 1, '#8c7356');
      fx += R(5, 12, 1, 8, '#c1d0d5', .15) + R(57, 11, 1, 8, '#c1d0d5', .15);
      break;
    case 'open-granary':
      bg = R(0, 0, 64, 10, '#27303b') + R(0, 10, 64, 10, '#3c4650') + R(0, 20, 64, 7, '#665b50');
      mid += R(5, 15, 54, 13, '#5d4735') + R(3, 12, 58, 4, '#2b2826') + R(10, 9, 44, 4, '#694536') + R(12, 17, 15, 11, '#1e1b19') + R(37, 17, 15, 11, '#1e1b19');
      front += R(0, 28, 64, 8, '#4b392e') + littleHuman(24, 22, {skin:'#b77b5e', body:'#6f5940', ink:t.ink}) + littleHuman(36, 22, {skin:'#c28a69', body:'#334f5c', ink:t.ink}) + P('17,34 21,25 25,34', '#c2a169');
      for (const x of [8, 18, 46, 56]) front += R(x, 19, 3, 3, '#eee3c7') + R(x + 1, 22, 1, 3, t.light, .7);
      fx += R(7, 18, 4, 5, t.light, .18, ' filter="url(#light-glow)"') + R(55, 18, 4, 5, t.light, .18, ' filter="url(#light-glow)"');
      break;
    case 'great-lake':
      bg = R(0, 0, 64, 9, '#202936') + R(0, 9, 64, 10, '#303d48') + R(0, 19, 64, 5, '#49545a');
      mid += P('0,24 11,18 22,24', '#26363c') + P('44,24 55,17 64,24', '#26363c') + R(0, 24, 64, 12, '#345462') + R(2, 28, 13, 1, '#567784') + R(37, 31, 20, 1, '#567784');
      front += P('11,30 20,23 34,30', '#5a4635') + R(16, 25, 13, 4, '#644e39') + R(21, 19, 2, 7, '#433128') + R(23, 19, 7, 3, '#eee3c7') + R(46, 23, 1, 11, '#433128') + R(47, 23, 5, 3, '#8d3e34');
      fx += R(0, 23, 64, 1, '#9a714f', .24) + R(29, 18, 2, 2, '#d17b45', .75) + R(30, 17, 1, 1, '#e5a05c');
      break;
  }
  const low = G(bg, 'id="background" filter="url(#far-soft)"') + G(mid, 'id="midground"') + G(front, 'id="foreground"') + G(fx, 'id="hd2d-fx"');
  const body = G(low, 'transform="scale(2)"') + G(sceneDetail(themeKey, variant, t), 'id="high-detail-pixels"');
  return svg(128, 72, 256, 144, label, body, hdDefs(t));
}

const SCENES = {
  oath: [
    ['twin-hills-dawn', '双子丘陵初战', 'twin-hills'], ['gray-flag-over-burned-village', '焚村后的第一面灰旗', 'gray-flag'], ['seven-towers-at-dusk', '七塔王城终局远景', 'seven-towers'],
  ],
  stars: [
    ['season-without-rain', '没有下雨的季节', 'no-rain'], ['black-rain-at-zero-tower', '零号雨塔黑雨之夜', 'black-rain'], ['kaelon-ring-city', '凯隆环都与星脉环', 'ring-city'],
  ],
  grain: [
    ['empty-bowl-market', '淮右市集与空碗', 'empty-bowl'], ['opening-the-county-granary', '白灯夜开县仓', 'open-granary'], ['great-lake-without-moon', '大泽无月水战', 'great-lake'],
  ],
};

function icon(themeKey, label, variant) {
  const t = THEMES[themeKey];
  let b = R(0, 0, 16, 16, t.sky) + R(1, 1, 14, 14, t.ink, .25);
  switch (variant) {
    case 'flag': b += R(5, 2, 1, 12, '#574536') + R(6, 3, 7, 6, t.gray) + R(6, 8, 5, 2, '#66686c'); break;
    case 'oathstone': b += R(4, 4, 8, 10, t.stone) + R(5, 2, 6, 3, t.stoneHi) + R(7, 5, 2, 7, t.light) + R(6, 7, 4, 1, t.light); break;
    case 'crown': b += R(3, 7, 10, 5, t.light) + R(3, 4, 2, 4, t.light) + R(7, 2, 2, 6, t.light) + R(11, 4, 2, 4, t.light) + R(7, 7, 2, 5, t.ink); break;
    case 'dragon-clasp': b += R(4, 5, 8, 6, t.red) + R(2, 4, 4, 3, t.red) + R(10, 4, 4, 3, t.red) + R(7, 3, 2, 10, t.light); break;
    case 'watercore': b += R(5, 3, 6, 10, '#4f7f91') + R(6, 2, 4, 2, t.stoneHi) + R(7, 5, 2, 6, t.light) + R(5, 13, 6, 1, t.ink); break;
    case 'triangle-key': b += R(3, 3, 10, 2, t.light) + R(3, 3, 2, 10, t.light) + R(11, 3, 2, 10, t.light) + R(5, 6, 6, 2, t.light) + R(7, 9, 2, 2, t.light); break;
    case 'firefly': b += R(5, 5, 6, 6, t.stoneHi) + R(6, 6, 4, 4, t.light) + R(3, 6, 2, 1, t.stoneHi) + R(11, 6, 2, 1, t.stoneHi) + R(7, 2, 2, 3, t.stone); break;
    case 'memory': b += R(6, 2, 4, 12, '#756980') + R(4, 5, 8, 6, '#756980') + R(7, 4, 2, 8, t.light) + R(6, 6, 4, 1, '#d6f2e9'); break;
    case 'grainbag': b += R(4, 5, 8, 9, t.cloth) + R(5, 3, 6, 3, '#8a6945') + R(7, 5, 2, 7, '#a68252') + R(5, 9, 6, 1, '#8a6945'); break;
    case 'white-lamp': b += R(5, 4, 6, 7, '#eee3c7') + R(6, 3, 4, 1, t.ink) + R(6, 11, 4, 1, t.light) + R(7, 12, 2, 2, '#604532'); break;
    case 'ledger': b += R(3, 3, 10, 11, '#96734e') + R(5, 4, 7, 9, '#c6ad7e') + R(6, 6, 5, 1, t.ink, .5) + R(6, 9, 5, 1, t.ink, .5) + R(4, 3, 1, 11, t.red); break;
    case 'seal': b += R(5, 3, 6, 6, t.red) + R(4, 8, 8, 4, '#5f392f') + R(6, 9, 4, 2, '#bd6a55') + R(7, 1, 2, 3, '#6a4b36'); break;
  }
  b += R(0, 0, 16, 1, t.ink) + R(0, 15, 16, 1, t.ink) + R(0, 0, 1, 16, t.ink) + R(15, 0, 1, 16, t.ink);
  return svg(16, 16, 64, 64, label, b);
}

const ICONS = {
  oath: [['gray-flag','灰旗','flag'], ['oath-stone','自由誓石','oathstone'], ['crown-fragment','誓约王冠碎片','crown'], ['dragon-alliance-clasp','巨龙盟约扣','dragon-clasp']],
  stars: [['water-core','净水芯','watercore'], ['star-vein-key','三角星脉密钥','triangle-key'], ['firefly-drone','萤火维修无人机','firefly'], ['memory-echo','留名者记忆晶体','memory']],
  grain: [['grain-sack','官粮袋','grainbag'], ['white-relief-lamp','白纸互助灯','white-lamp'], ['household-ledger','户籍与军功名册','ledger'], ['broken-official-seal','被砸开的官印','seal']],
};

function paletteSheet(themeKey) {
  const entries = Object.entries(THEMES[themeKey]);
  let body = R(0, 0, 64, 24, '#101116');
  entries.forEach(([name, color], index) => {
    const x = 2 + (index % 6) * 10;
    const y = 2 + Math.floor(index / 6) * 10;
    body += R(x, y, 8, 8, color) + R(x, y, 8, 1, '#ffffff', .18) + R(x, y + 7, 8, 1, '#000000', .25);
  });
  return svg(64, 24, 256, 96, `${themeKey} 有限色板`, body);
}

function gallery(themeKey, title) {
  const images = [
    ...CAST[themeKey].map((item) => [`characters/${item.id}-portrait.svg`, item.label]),
    ...CAST[themeKey].map((item) => [`units/${item.id}-walk-sheet.svg`, `${item.label}四帧单位`]),
    ...BUILDINGS[themeKey].map(([id, label]) => [`architecture/${id}.svg`, label]),
    ...SCENES[themeKey].map(([id, label]) => [`scenes/${id}.svg`, label]),
    ...ICONS[themeKey].map(([id, label]) => [`props/${id}.svg`, label]),
  ];
  const cards = images.map(([src, label]) => `<figure><div class="image"><img src="./${src}" alt="${label}"></div><figcaption>${label}<small>${src}</small></figcaption></figure>`).join('');
  return `<!doctype html><meta charset="utf-8"><title>${title} · SVG 像素素材预览</title><style>html{color-scheme:dark}body{margin:0;padding:28px;background:#11131a;color:#ebe4d6;font:14px/1.45 system-ui,sans-serif}h1{font-size:22px;margin:0 0 22px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:14px}figure{margin:0;background:#1c202a;border:1px solid #343b49;border-radius:8px;overflow:hidden}.image{display:grid;place-items:center;min-height:170px;padding:12px;background:repeating-conic-gradient(#222731 0 25%,#1d222b 0 50%) 50%/20px 20px}.image img{max-width:100%;max-height:190px;image-rendering:pixelated}figcaption{padding:10px 12px;font-weight:650}small{display:block;color:#8f99aa;font:11px/1.4 ui-monospace,monospace;margin-top:3px;overflow-wrap:anywhere}</style><h1>${title} · SVG 像素素材预览</h1><main class="grid">${cards}</main>`;
}

const outputs = [];
for (const [themeKey, cast] of Object.entries(CAST)) {
  const candidate = themeKey === 'oath' ? 'candidate-01' : themeKey === 'stars' ? 'candidate-02' : 'candidate-03';
  const draftRoot = `${candidate}/assets/draft-v1`;
  for (const p of cast) {
    outputs.push(write(`${draftRoot}/characters/${p.id}-portrait.svg`, portrait(themeKey, p)));
    outputs.push(write(`${draftRoot}/units/${p.id}-walk-sheet.svg`, unitSheet(themeKey, p)));
  }
  for (const [id, label, variant] of BUILDINGS[themeKey]) {
    outputs.push(write(`${draftRoot}/architecture/${id}.svg`, architecture(themeKey, id, label, variant)));
  }
  for (const [id, label, variant] of SCENES[themeKey]) {
    outputs.push(write(`${draftRoot}/scenes/${id}.svg`, scene(themeKey, id, label, variant)));
  }
  for (const [id, label, variant] of ICONS[themeKey]) {
    outputs.push(write(`${draftRoot}/props/${id}.svg`, icon(themeKey, label, variant)));
  }
  outputs.push(write(`${candidate}/assets/style/palette.svg`, paletteSheet(themeKey)));
  outputs.push(write(`${candidate}/assets/style/tokens.json`, `${JSON.stringify(THEMES[themeKey], null, 2)}\n`));
  const title = themeKey === 'oath' ? '断冠之誓' : themeKey === 'stars' ? '群星熄灭之前' : '布衣定鼎';
  outputs.push(write(`${draftRoot}/gallery.html`, gallery(themeKey, title)));
}

console.log(`Generated ${outputs.length} draft files.`);
for (const output of outputs) console.log(output);

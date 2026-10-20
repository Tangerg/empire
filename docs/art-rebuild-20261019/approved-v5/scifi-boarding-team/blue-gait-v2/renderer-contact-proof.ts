import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runtimeUnitPicture } from '../../../../../packages/game-ui/src/art/runtime-raster';
import { boardStripMarkup } from '../../../../../packages/game-ui/src/art/board-surface';
const out=resolve(import.meta.dirname);
const href='data:image/png;base64,'+readFileSync(resolve(out,'canonical64x48-sheet.png')).toString('base64');
const p=runtimeUnitPicture({href,frameWidth:64,frameHeight:48,frameCount:4,anchor:{x:32,y:47},idleFrame:0,walkFrames:[1,3],attackFrame:2},'#6a9eae');
if(!p.strip) throw new Error('Missing strip');
const names=['idle','walk-a','attack','walk-b'];
let s='<svg xmlns="http://www.w3.org/2000/svg" width="384" height="160" viewBox="0 0 384 160"><rect width="384" height="160" fill="#dad3b6"/>';
for(let row=0;row<2;row++) for(let i=0;i<4;i++){
 const x=32+i*96,y=38+row*80;
 s+=`<g transform="translate(${x} ${y})">${p.body}<defs><clipPath id="clip-${row}-${i}"><rect x="${p.strip.x}" y="${p.strip.y}" width="64" height="48"/></clipPath></defs><g clip-path="url(#clip-${row}-${i})">${boardStripMarkup(p.strip,i)}</g>`;
 if(row===1)s+='<path d="M-14 31H46 M16 23V38" stroke="#ad3460" stroke-width="0.4" fill="none"/><ellipse cx="16" cy="29.2" rx="12.5" ry="4.2" stroke="#ad3460" stroke-width="0.35" fill="none"/>';
 s+=`</g><text x="${x-12}" y="${y+39}" font-size="7">${names[i]}${row===1?' / guide':''}</text>`;
}
s+='</svg>';writeFileSync(resolve(out,'renderer-contact-proof.svg'),s);
writeFileSync(resolve(out,'renderer-contact-contract.json'),JSON.stringify({scope:'Shared runtimeUnitPicture + boardStripMarkup static helper diagnostic, not C02 gameplay or scene consumer',source:'packages/game-ui/src/art/runtime-raster.ts',scale:1,frameSize:[64,48],anchor:[32,47],cellFoot:p.foot,stripPlacement:[p.strip.x,p.strip.y],contactEllipse:{cx:16,cy:29.2,rx:12.5,ry:4.2,opacity:0.48},teamRing:{cx:16,cy:29.5,rx:11,ry:2.25},team:'#6a9eae',tint:p.strip.tint,rasterizerViewportGuard:"Explicit outer clip repeats declared64x48 strip viewport because Inkscape does not respect nested svg overflow reliably",candidate01CssApplied:false,reliefApplied:false},null,2)+'\n');

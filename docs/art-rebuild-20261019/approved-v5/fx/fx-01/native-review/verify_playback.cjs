const fs=require('fs');
const path=require('path');
const {chromium}=require('playwright');
(async()=>{
 const root=__dirname;
 const browser=await chromium.launch({executablePath:'/usr/bin/chromium',headless:true,args:['--no-sandbox','--disable-dev-shm-usage']});
 const page=await browser.newPage({viewport:{width:1180,height:1100},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file://'+path.join(root,'playback.html'));
 await page.waitForFunction(()=>document.body.dataset.ready==='true');
 const samples=[0,83.32,83.34,166.66,166.67,249.99,250,333.33,419.99,420,500];
 const timing=await page.evaluate(samples=>samples.map(ms=>({ms,phase:fxReview.phaseAt(ms)})),samples);
 const expected=[0,0,1,1,2,2,3,3,3,-1,-1];
 if(!timing.every((x,i)=>x.phase===expected[i]))throw new Error('Incorrect deterministic phase boundary');
 const screenshots=[];
 for(const ms of [0,100,180,250,333.34,419.99,420]){
  await page.evaluate(ms=>fxReview.renderAt(ms),ms);
  const name=`browser-staged-${String(ms).replace('.','_')}ms.png`;
  await page.screenshot({path:path.join(root,name),fullPage:true});screenshots.push(name);
 }
 // Verify the actual draw calls hold byte-identical fragment canvases until removal.
 const rendered=await page.evaluate(()=>{
  const canvas=document.querySelector('canvas[data-background="olive"][data-density="2"][data-display-size="64"]');
  const arr=[];
  for(const t of [250,333.34,419.99,420]){fxReview.renderAt(t);arr.push({time:t,pixels:Array.from(canvas.getContext('2d').getImageData(0,0,64,64).data)});}
  return arr;
 });
 const same=JSON.stringify(rendered[0].pixels)===JSON.stringify(rendered[1].pixels)&&JSON.stringify(rendered[0].pixels)===JSON.stringify(rendered[2].pixels);
 if(!same)throw new Error('Final frame pixels changed while held');
 const removed=rendered[3].pixels.every((v,i)=>v===[98,107,67,255][i%4]);
 if(!removed)throw new Error('Effect did not disappear at 420ms');
 await page.click('#play');await page.waitForFunction(()=>fxReview.currentTime===420);
 const playback=await page.evaluate(()=>fxReview.history);
 if(JSON.stringify(playback.map(x=>x.phase))!==JSON.stringify([0,1,2,3,-1]))throw new Error('Actual browser playback skipped/reordered a phase');
 await page.click('#repeat');
 await page.waitForFunction(()=>fxReview.history.length>=10);
 await page.click('#pause');
 const repeats=await page.evaluate(()=>fxReview.history);
 const dimensions=await page.evaluate(()=>Array.from(document.querySelectorAll('canvas')).map(c=>({background:c.dataset.background,density:Number(c.dataset.density),displaySize:Number(c.dataset.displaySize),intrinsic:[c.width,c.height],css:[c.getBoundingClientRect().width,c.getBoundingClientRect().height]})));
 await browser.close();
 const result={scope:'Headless Chromium staged preview only; no live game/renderer/hit-overlay test',browser:'Chromium',deviceScaleFactor:1,errors,
 deterministicTiming:timing,finalHoldPixelsIdentical250To419_99:same,clearAt420:removed,
 actualRequestAnimationFramePlayback:playback,actualRepeatedPreview:repeats,nativeCanvasDimensions:dimensions,screenshots};
 fs.writeFileSync(path.join(root,'browser-timing-validation.json'),JSON.stringify(result,null,2)+'\n');
 console.log(JSON.stringify({errors,phaseSequence:playback.map(x=>x.phase),actualTransitionTimes:playback.map(x=>x.time),held:same,removed,repeatSequence:repeats.map(x=>x.phase)}));
})().catch(e=>{console.error(e);process.exit(1)});

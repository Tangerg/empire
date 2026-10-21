// Every case from the former native-only future-payload gate is kept by name.
export function oldSaveCases(record:any, initial:any) {
 const selector=(zone?:string)=>zone?{zone}:{};
 const directive=(mode='guard')=>({type:'setUnitDirective',selector:selector(),directive:{mode,waypoints:[{x:0,y:0}]}});
 const spawnSource=(unit='soldier')=>({unit,owner:1,x:0,y:0});
 const spawn=(source=spawnSource())=>({type:'spawnUnits',units:[source]});
 const trigger=(effect:any)=>({id:'reference-probe',timing:'afterAction',once:true,condition:{type:'turnAtLeast',turn:999},effects:[effect]});
 function check(name:string,effect:any,oldAccepted:boolean,edit?:(s:any)=>void) {record(name,(s:any)=>{s.scenario.triggers.push(trigger(effect));edit?.(s);},undefined,oldAccepted);}
 function badDirective(name:string,edit:(d:any)=>void){const effect=directive();edit(effect.directive);check(name,effect,false);}
 check('unknown future directive',directive('unregistered-mode'),false);
 badDirective('unknown future directive zone',d=>d.zone='missing-zone');
 badDirective('offboard waypoint',d=>d.waypoints[0].x=-1);
 badDirective('fractional waypoint',d=>d.waypoints[0].x=0.5);
 badDirective('damaged waypoint list',d=>d.waypoints={});
 badDirective('missing mode',d=>delete d.mode);
 for(const mode of ['assault','guard','patrol','retreat'])check(`supported ${mode}`,directive(mode),true);
 check('selector missing zone',{...directive(),selector:selector('missing-zone')},false);
 for(const type of ['changeUnitOwner','changeMorale'])check(`${type} selector missing zone`,{type,selector:selector('missing-zone'),owner:1,amount:0},false);
 check('neutral and absent selector targets remain valid',{...directive(),selector:{owner:0,ids:[99999],keys:['not-arrived']}},true);
 const zoned:any=directive();zoned.directive.zone='empty-zone';check('empty declared zone',zoned,true,s=>s.scenario.zones['empty-zone']=[]);
 function badSpawn(name:string,edit:(s:any)=>void){const source=spawnSource();edit(source);check(name,spawn(source),false);}
 badSpawn('future unit type absent',s=>s.unit='missing-unit');badSpawn('future owner absent',s=>s.owner=999);badSpawn('future neutral owner absent',s=>s.owner=0);
 badSpawn('future cell offboard',s=>s.x=-1);badSpawn('future cell fractional',s=>s.y=0.5);
 badSpawn('future learned ability',s=>s.learnedAbilities=['unknown-ability']);badSpawn('future reaction',s=>s.reaction='unknown-reaction');
 badSpawn('future directive',s=>s.directive={mode:'unknown-mode'});badSpawn('future directive zone',s=>s.directive={mode:'guard',zone:'missing-zone'});
 badSpawn('future formation',s=>s.formation='unknown-formation');badSpawn('future career',s=>s.career='unknown-career');
 badSpawn('future account adapter',s=>s.resources={'unknown-resource':{current:1,capacity:null}});
 check('valid future default unit',spawn(),true);
 const occupied:any=spawnSource();Object.assign(occupied,{x:initial.units[0].x,y:initial.units[0].y,key:initial.units[0].key});
 check('future occupancy and key collision not pre-judged',spawn(occupied),true);
 check('missing former structure remains valid no-op target',{type:'damageStructure',id:'former-structure',amount:1},true);
 check('removing absent engagement remains valid no-op',{type:'removeEngagementRule',id:'former-rule'},true);
 check('unsupported effect still rejected',{type:'unknown-effect',id:'former-structure',amount:1},false);
 const effect=(type:string):any=>({type,selector:selector()});
 for(const type of ['addStatus','removeStatus']){
  check(`${type} unknown status`,{...effect(type),status:'unknown-status',duration:2},false);
  check(`${type} known status`,{...effect(type),status:'poisoned',duration:2},true);
 }
 for(const type of ['teleportUnits','restoreWithdrawnUnits','replaceTerrain','addOverlay','setElevation','addElevation']){
  const bad={...effect(type),zone:'unknown-zone',terrain:'plain',overlay:'flooded',value:1,amount:1};check(`${type} unknown zone`,bad,false);
  check(`${type} known empty zone`,{...bad,zone:'probe-zone'},true,s=>s.scenario.zones['probe-zone']=[]);
 }
 for(const [type,field] of [['replaceTerrain','terrain'],['addOverlay','overlay']])check(`${type} unknown content`,{...effect(type),[field]:'unknown-content'},false);
 for(const [type,field] of [['setElevation','value'],['addElevation','amount']])check(`${type} fractional value`,{...effect(type),[field]:1.5},false);
 check('forced move valid source',{...effect('forceMove'),source:{x:0,y:0},distance:1},true);
 check('forced move offboard source',{...effect('forceMove'),source:{x:-1,y:0},distance:1},false);
 check('forced move fractional distance',{...effect('forceMove'),source:{x:0,y:0},distance:1.5},false);
 check('cliff adjacent edge',{...effect('setCliffs'),edges:[{from:{x:0,y:0},to:{x:1,y:0}}]},true);
 check('cliff nonadjacent edge',{...effect('setCliffs'),edges:[{from:{x:0,y:0},to:{x:2,y:0}}]},false);
 check('cover offboard cell',{...effect('setDirectionalCover'),covers:[{at:{x:-1,y:0}}]},false);
 check('repair missing structure rejected',{type:'repairStructure',id:'former-structure',amount:1},false);
 check('move missing composite rejected',{type:'moveComposite',id:'unknown-composite',dx:0,dy:0},false);
 for(const type of ['activateObjective','cancelObjective','completeObjective','revealObjective']){
  check(`${type} unknown objective`,{type,player:1,id:'unknown-objective'},false);
  check(`${type} known nested objective`,{type,player:1,id:'route'},true);
 }
 check('unknown resource adapter',{type:'changeUnitResource',selector:selector(),resource:'unknown-resource',amount:1},false);
 check('known resource adapter',{type:'changeUnitResource',selector:selector(),resource:'momentum',amount:1},true);
 check('team unknown player',{type:'setPlayerTeam',player:999,team:1},false);
 function conditionCheck(name:string,condition:any,accepted:boolean){check(name,{type:'emitSignal',signal:'probe'},accepted,s=>s.scenario.triggers.at(-1).condition=condition);}
 conditionCheck('nested unknown condition',{type:'not',condition:{type:'unknown-condition'}},false);
 conditionCheck('nested condition missing zone',{type:'all',conditions:[{type:'unitInZone',zone:'unknown-zone',selector:selector()}]},false);
 conditionCheck('removed structure condition remains valid',{type:'structure',id:'former-structure',state:'destroyed'},true);
 conditionCheck('fractional cycle rejected',{type:'turnCycle',every:1.5},false);
}

export function oldCatalogCases(record:any,packs:any[]) {
 function future(name:string,edit:(u:any,w:any,c:any)=>void,oldAccepted=false){
  const changed=structuredClone(packs);
  const get=(family:string,id:string)=>structuredClone(changed.flatMap(p=>p[family]??[]).find(x=>x.id===id));
  const unit=get('units','soldier');Object.assign(unit,{id:'future-unit',weapons:['future-weapon']});
  const weapon=get('weapons','soldier_sword');weapon.id='future-weapon';
  const career=get('careers','militia');Object.assign(career,{id:'future-career',unitType:'future-unit'});
  edit(unit,weapon,career);changed.push({id:'reference-probe',version:1,units:[unit],weapons:[weapon],careers:[career]});
  record(name,changed,oldAccepted);
 }
 future('future type ability',(u)=>u.abilities.push('unknown-ability'));
 future('future default reaction',u=>u.defaultReaction='unknown-reaction');
 future('future weapon area',(_,w)=>w.area='unknown-area');
 future('future weapon effect',(_,w)=>w.hitEffects.push({type:'unknown-effect'}));
 future('future weapon account',(_,w)=>w.resources['unknown-resource']={current:1,capacity:null});
 future('future weapon cost',(_,w)=>w.resourceCosts.push({resource:'unknown-resource',amount:1}));
 future('future unit account',u=>u.resources['unknown-resource']={current:1,capacity:null});
 future('future mastered ability',(_,__,c)=>c.masteryAbilities.push('unknown-ability'));
 future('valid prospective content',()=>{},true);
}

export function oldLevelCases(record:any,original:any) {
 const spawnSource=()=>({unit:'soldier',owner:1,x:0,y:0});
 const trigger=(effect:any)=>({id:'future-capability',timing:'afterAction',condition:{type:'turnAtLeast',turn:999},effects:[effect]});
 function level(name:string,edit:(l:any)=>void,oldAccepted=false){const l=structuredClone(original);edit(l);record(name,l,oldAccepted);}
 level('unknown nested failOn condition',l=>l.players[0].objectives[0].condition={type:'not',condition:{type:'unknown-condition'}});
 function future(name:string,edit:(s:any)=>void,accepted=false){level(name,l=>{const s=spawnSource();edit(s);l.scenario.triggers.push(trigger({type:'spawnUnits',units:[s]}));},accepted);}
 future('unknown future learned ability',s=>s.learnedAbilities=['unknown-ability']);
 future('unknown future reaction',s=>s.reaction='unknown-reaction');future('unknown future directive',s=>s.directive={mode:'unknown-directive'});
 future('unknown future resource',s=>s.resources={'unknown-resource':{current:0,capacity:null}});
 future('supported future overrides',s=>Object.assign(s,{reaction:'counter',learnedAbilities:['heal'],directive:{mode:'guard'}}),true);
 level('unknown initial resource',l=>l.units[0].resources={'unknown-resource':{current:0,capacity:null}});
 level('unknown player resource',l=>l.players[0].resources['unknown-resource']={current:0,capacity:null});
 level('unknown base resource grant',l=>l.rules.baseResourceGrants=[{subject:'player',resource:'unknown-resource',amount:1}]);
 level('unknown scenario resource adapter',l=>l.scenario.triggers.push({...trigger({type:'changeUnitResource',selector:{},resource:'unknown-resource',amount:1}),id:'resource-probe'}));
}

'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs/promises'),os=require('node:os'),path=require('node:path');
const {Bridge}=require('../bridge.cjs');
const exe=process.env.FROST_CORE_EXE||path.resolve(__dirname,'../runtime/FrostTodo.Core.exe');
const emptyTask=(id,title)=>({Id:id,Title:title,Tags:['工作'],Due:'',Time:'',Urgency:0,Importance:0,Repeat:'none',WeekDays:[],Reminder:'',ReminderFired:false,Created:new Date().toISOString()});
test('Chromium task service preserves native task behavior, isolated persistence and failures',async()=>{
 const dir=await fs.mkdtemp(path.join(os.tmpdir(),'frost-electron-test-'));const bridge=new Bridge(exe,dir);
 try{
  let state=await bridge.call('snapshot');assert.equal(state.Data.Settings.Font,'desktop');assert.equal(state.Data.Settings.TextSize,16);assert.equal(state.Data.Settings.Opacity,100);
  const daily={...emptyTask('daily','每日任务'),Repeat:'daily'};state=await bridge.call('saveTask',{task:daily,baseline:null});assert.ok(state.Views.daily.includes('today'));assert.equal(state.Data.Tasks[0].Due,'');
  state=await bridge.call('complete',{id:'daily'});assert.ok(!state.Views.daily.includes('today'));assert.ok(state.Dates.daily>state.Today);state=await bridge.call('undo');assert.ok(state.Views.daily.includes('today'));
  const second=emptyTask('second','Second <script>');state=await bridge.call('saveTask',{task:second});assert.equal(state.Data.Tasks[0].Title,second.Title);
  const baseline=structuredClone(state.Data.Tasks[0]),edited={...baseline,Title:'已修改中文 Aa'};state=await bridge.call('saveTask',{task:edited,baseline});assert.equal(state.Data.Tasks[0].Title,edited.Title);
  state=await bridge.call('reorder',{source:'daily',target:'second',after:false,visible:['daily','second']});assert.deepEqual(state.Data.Tasks.map(t=>t.Id),['daily','second']);
  state=await bridge.call('tagAdd',{tag:'自定义'});assert.ok(state.Data.Themes.includes('自定义'));state=await bridge.call('tagVisible',{tag:'自定义',visible:false});assert.ok(!state.Data.Settings.QuickTags.includes('自定义'));
  await assert.rejects(bridge.call('tagAdd',{tag:'自定义'}));assert.equal((await bridge.call('snapshot')).Data.Tasks.length,2);
  state=await bridge.call('settings',{Font:'yozai',TextSize:20,GptTheme:'dark',QuickViews:['very-important','very-urgent']});assert.equal(state.Data.Settings.TextSize,20);assert.equal(state.Data.Settings.GptTheme,'dark');
  const before=await bridge.call('export');await assert.rejects(bridge.call('saveTask',{task:{...emptyTask('invalid','错误时间'),Time:'18:30'}}));assert.equal(await bridge.call('export'),before);
  await assert.rejects(bridge.call('settings',{Unknown:'bad'}));assert.equal(await bridge.call('export'),before);
  const copy=JSON.parse(before);copy.Tasks.push(emptyTask('imported','新导入'));copy.Settings.Font='cubic';state=await bridge.call('import',{text:JSON.stringify(copy)});assert.equal(state.Data.Tasks.length,3);assert.equal(state.Data.Settings.Font,'yozai');
  state=await bridge.call('import',{text:JSON.stringify(copy)});assert.equal(state.Data.Tasks.length,3);
  const exported=await bridge.call('export');assert.ok(await fs.stat(path.join(dir,'tasks.json.previous')));await bridge.close();
  const restarted=new Bridge(exe,dir);try{state=await restarted.call('snapshot');assert.equal(state.Data.Settings.Font,'yozai');assert.equal(state.Data.Tasks.length,3);assert.deepEqual(state.Data.Tasks.map(t=>t.Id),['daily','second','imported']);assert.equal(state.Data.Settings.TextSize,20);}finally{await restarted.close();}
  assert.equal(JSON.parse(exported).Version,1);
 }finally{await bridge.close();}
});
test('first migration copies a profile without changing source or reseeding later',async()=>{
 const root=await fs.mkdtemp(path.join(os.tmpdir(),'frost-migration-test-')),seed=path.join(root,'source'),destination=path.join(root,'destination');
 const original=new Bridge(exe,seed);let source;try{await original.call('saveTask',{task:{...emptyTask('source','原版任务'),Repeat:'daily'}});await original.call('settings',{Tint:'#7B8AD7',Font:'cubic',TextRendering:'pixel'});source=await original.call('export');}finally{await original.close();}
 const copy=new Bridge(exe,destination,seed);try{let state=await copy.call('snapshot');assert.equal(state.Data.Tasks[0].Id,'source');assert.equal(state.Data.Settings.Font,'desktop');assert.equal(state.Data.Settings.TextRendering,'desktop');assert.equal(state.Data.Settings.Tint,'#7B8AD7');assert.equal(await fs.readFile(path.join(seed,'tasks.json'),'utf8'),source);await copy.call('delete',{id:'source'});}finally{await copy.close();}
 const again=new Bridge(exe,destination,seed);try{assert.equal((await again.call('snapshot')).Data.Tasks.length,0);assert.equal(await fs.readFile(path.join(seed,'tasks.json'),'utf8'),source);}finally{await again.close();}
});

'use strict';
const {app,BrowserWindow,Tray,Menu,nativeImage,ipcMain,dialog,globalShortcut,screen,protocol,net,nativeTheme,Notification,session}=require('electron');
const path=require('node:path'),fs=require('node:fs'),{pathToFileURL}=require('node:url');
const {Bridge}=require('./bridge.cjs'),{assetPath,safeBounds,countFor,transparentFor,snapDot,restoreDot}=require('./policy.cjs');
protocol.registerSchemesAsPrivileged([{scheme:'frost',privileges:{standard:true,secure:true,supportFetchAPI:true}}]);
const root=__dirname,assetRoot=app.isPackaged?root:path.join(root,'..'),local=process.env.LOCALAPPDATA||app.getPath('userData');
const explicit=process.argv.indexOf('--data-dir');
const profile=explicit>=0?path.resolve(process.argv[explicit+1]):path.join(local,'FrostTodo-Chromium');
fs.mkdirSync(path.join(profile,'browser'),{recursive:true});app.setPath('userData',path.join(profile,'browser'));app.setName('FrostTodo Chromium');
let main,dot,tray,bridge,state,quitting=false,collapsed=false,currentCount=0,route='main',hotkey='',timer,boundsTimer,busyTick=false,replacing=false,movingUntil=0,pendingNotices=[];
const retiring=new WeakSet(),transparency=new WeakMap();let queue=Promise.resolve();const notices=new Set();
function serialize(action){const next=queue.then(action);queue=next.catch(()=>{});return next;}
function core(op,arg){return serialize(()=>bridge.call(op,arg));}
const seeded=explicit<0?['FrostTodo-GPT','FrostTodo'].map(name=>path.join(local,name)).find(dir=>fs.existsSync(path.join(dir,'tasks.json'))):null;
function runtimePath(){return path.join(app.isPackaged?root.replace(/app\.asar$/,'app.asar.unpacked'):root,'runtime','FrostTodo.Core.exe');}
function sendState(){const value={...state,Dark:nativeTheme.shouldUseDarkColors,Hotkey:hotkey,CurrentCount:currentCount};for(const win of [main,dot])if(win&&!win.isDestroyed())win.webContents.send('frost:state',value);}
function updateState(next){state=next;sendState();}
function handle(win){return win.getNativeWindowHandle().readBigUInt64LE().toString();}
function surface(){const p=state.Data.Settings;return p.GptTheme==='dark'||p.GptTheme==='system'&&nativeTheme.shouldUseDarkColors?'#212121':p.GptTheme==='custom'?p.Tint:'#FFFFFF';}
async function applyWindow(){
 if(!main||main.isDestroyed())return;const p=state.Data.Settings;main.setAlwaysOnTop(p.Mode==='top');
 main.setBackgroundColor(transparentFor(p)?'#00000000':surface());
 await core('native',{handle:handle(main),mode:p.Mode,tint:surface(),opacity:transparentFor(p)?p.Opacity:100});
}
function protect(win){
 win.webContents.setWindowOpenHandler(()=>({action:'deny'}));win.webContents.on('will-navigate',e=>e.preventDefault());win.webContents.on('will-attach-webview',e=>e.preventDefault());
 win.webContents.setZoomFactor(1);
}
async function createMain(previous){
 const p=state.Data.Settings,bounds=previous&&!previous.isDestroyed()?previous.getBounds():safeBounds(p,screen.getAllDisplays());
 const win=new BrowserWindow({...bounds,minWidth:300,minHeight:340,maxWidth:1600,maxHeight:1800,show:false,frame:false,transparent:transparentFor(p),backgroundColor:transparentFor(p)?'#00000000':surface(),roundedCorners:false,hasShadow:false,resizable:true,title:'FrostTodo · Chromium',icon:path.join(assetRoot,'Assets','FrostTodo.ico'),webPreferences:{preload:path.join(root,'preload.cjs'),contextIsolation:true,sandbox:true,nodeIntegration:false,spellcheck:false,backgroundThrottling:true}});
 main=win;transparency.set(win,transparentFor(p));protect(win);
 win.on('close',e=>{if(!quitting&&!retiring.has(win)){e.preventDefault();win.webContents.send('frost:command','collapse');}});
 win.on('closed',()=>{if(main===win)main=null;});
 function persistBounds(){clearTimeout(boundsTimer);boundsTimer=setTimeout(()=>{if(win.isDestroyed()||retiring.has(win))return;const b=win.getBounds();core('settings',{Width:b.width,Height:b.height,Left:b.x,Top:b.y}).then(next=>{state=next;}).catch(report);},400);}
 win.on('resize',persistBounds);win.on('move',persistBounds);win.on('will-move',()=>movingUntil=Date.now()+800);win.on('will-resize',()=>movingUntil=Date.now()+800);
 win.on('blur',()=>{if(state?.Data.Settings.Mode==='bottom'&&!retiring.has(win))applyWindow().catch(report);});
 await win.loadURL('frost://app/index.html#'+route);
 await applyWindow();
 if(!collapsed){if(p.Mode==='bottom')win.showInactive();else win.show();}
 if(previous&&!previous.isDestroyed()){retiring.add(previous);previous.destroy();}sendState();
}
async function refreshWindow(){
 if(!main||main.isDestroyed())return;
 if(transparency.get(main)!==transparentFor(state.Data.Settings)){
  if(replacing)return;replacing=true;try{await createMain(main);}finally{replacing=false;}
 }else await applyWindow();
}
function show(){collapsed=false;if(dot&&!dot.isDestroyed())dot.hide();if(main){if(main.isMinimized())main.restore();state.Data.Settings.Mode==='bottom'?main.showInactive():main.show();if(state.Data.Settings.Mode!=='bottom')main.focus();applyWindow().catch(report);}}
async function collapse(){collapsed=true;main.hide();if(state.Data.Settings.Collapse==='dot'){await createDot();dot.showInactive();}else if(dot)dot.hide();}
async function createDot(){
 if(dot&&!dot.isDestroyed()){sendState();return;}
 const p=state.Data.Settings,{x,y}=restoreDot(p,screen.getAllDisplays(),screen.getPrimaryDisplay());
 dot=new BrowserWindow({x,y,width:52,height:52,show:false,frame:false,transparent:true,roundedCorners:false,resizable:false,skipTaskbar:true,alwaysOnTop:true,hasShadow:false,webPreferences:{preload:path.join(root,'preload.cjs'),contextIsolation:true,sandbox:true,nodeIntegration:false}});protect(dot);
 dot.on('close',e=>{if(!quitting){e.preventDefault();dot.hide();}});await dot.loadURL('frost://app/dot.html');sendState();
}
function command(value){show();main.webContents.send('frost:command',value);}
function menuTemplate(){const p=state.Data.Settings;return [
 {label:'打开待办  '+hotkey,click:show},{label:'新建任务',click:()=>command('new')},{type:'separator'},
 {label:'外观设置',click:()=>command('appearance')},{label:'功能设置',click:()=>command('settings')},{label:'标签管理',click:()=>command('tags')},
 {type:'separator'},...['top','normal','bottom'].map((mode,i)=>({label:['保持置顶','普通窗口','保持置底'][i],type:'radio',checked:p.Mode===mode,click:()=>settings({Mode:mode}).catch(report)})),{type:'separator'},{label:'收起',click:()=>command('collapse')},{label:'退出',click:()=>command('quit')}];}
async function settings(patch){const old=transparentFor(state.Data.Settings);updateState(await core('settings',patch));
 // Finish the IPC reply before retiring its renderer.
 setTimeout(()=>refreshWindow().catch(report),old===transparentFor(state.Data.Settings)?0:120);return state;
}
function report(error){if(quitting)return;const message=String(error?.message||error);if(main&&!main.isDestroyed())main.webContents.send('frost:command',{error:message});}
async function quit(){if(quitting)return;quitting=true;clearInterval(timer);clearTimeout(boundsTimer);globalShortcut.unregisterAll();if(bridge){if(main&&!main.isDestroyed()){const b=main.getBounds();await core('settings',{Width:b.width,Height:b.height,Left:b.x,Top:b.y}).catch(()=>{});}await bridge.close().catch(()=>{});}if(tray)tray.destroy();app.quit();}
function notify(tasks){
 pendingNotices=tasks;
 const fallback=()=>{if(tray)tray.displayBalloon({title:'待办提醒',content:tasks.slice(0,4).map(t=>t.Title).join('\n'),icon:nativeImage.createFromPath(path.join(assetRoot,'Assets','FrostTodo.ico'))});};
 const notification=new Notification({title:tasks.length>1?`有 ${tasks.length} 项待办提醒`:'待办提醒',body:tasks.slice(0,4).map(t=>t.Title).join('\n'),icon:path.join(assetRoot,'Assets','FrostTodo.png'),timeoutType:'never'});notices.add(notification);
 notification.on('click',()=>{show();main.webContents.send('frost:command',{reminders:tasks});});notification.on('close',()=>notices.delete(notification));notification.on('failed',()=>{notices.delete(notification);fallback();});try{notification.show();}catch{fallback();}
 if(main&&!main.isDestroyed())main.webContents.send('frost:command',{reminders:tasks});
}
const taskOps=new Set(['saveTask','complete','delete','undo','reorder','tagAdd','tagRemove','tagVisible']);
async function action(event,name,arg){
 const sender=[main,dot].find(w=>w&&!w.isDestroyed()&&w.webContents===event.sender);if(!sender||event.senderFrame!==event.sender.mainFrame||!event.senderFrame.url.startsWith('frost://app/'))throw new Error('Invalid sender');
 if(sender===dot&&!['snapshot','restore','dotMove','dotMenu'].includes(name))throw new Error('Action is not available');
 if(name==='snapshot')return {...state,Dark:nativeTheme.shouldUseDarkColors,Hotkey:hotkey};
 if(taskOps.has(name)){updateState(await core(name,arg));return state;}
 if(name==='settings')return settings(arg);
 if(name==='resetAppearance'){updateState(await core('resetAppearance'));setTimeout(()=>refreshWindow().catch(report),120);return state;}
 if(name==='route'){route=['main','edit','appearance','settings','tags','filter'].includes(arg)?arg:'main';return true;}
 if(name==='currentCount'){currentCount=Math.max(0,Math.min(20000,Number(arg)||0));if(dot&&!dot.isDestroyed())dot.webContents.send('frost:state',{...state,Dark:nativeTheme.shouldUseDarkColors,CurrentCount:currentCount});return true;}
 if(name==='menu'){Menu.buildFromTemplate(menuTemplate()).popup({window:main});return true;}
 if(name==='collapse'){await collapse();return true;}if(name==='restore'){show();return true;}if(name==='quit'){setImmediate(quit);return true;}
 if(name==='dotMenu'){const p=state.Data.Settings;Menu.buildFromTemplate([{label:'展开待办',click:show},...['all','today','overdue','important','urgent','current','none'].map((value,i)=>({label:['所有待办','今日待办','逾期待办','重要待办','紧急待办','当前筛选','不显示数字'][i],type:'radio',checked:p.Count===value,click:()=>settings({Count:value}).catch(report)})),{label:'完全隐藏',click:()=>dot.hide()},{label:'退出',click:()=>command('quit')}]).popup({window:dot});return true;}
 if(name==='dotMove'){
  if(!dot)return true;const pos=screen.getCursorScreenPoint(),offset=key=>Number.isFinite(arg?.[key])?Math.max(0,Math.min(52,arg[key])):26;
  let x=Math.round(pos.x-offset('offsetX')),y=Math.round(pos.y-offset('offsetY'));
  if(arg?.end)({x,y}=snapDot({x,y,width:52,height:52},screen.getDisplayNearestPoint(pos).workArea));
  dot.setPosition(x,y);if(arg?.end)updateState(await core('settings',{DotLeft:x,DotTop:y}));return true;
 }
 if(name==='background'){
  if(arg?.remove){updateState(await core('background',{path:''}));}else{const result=await dialog.showOpenDialog(main,{properties:['openFile'],filters:[{name:'背景图片',extensions:['png','jpg','jpeg','bmp']}]});if(result.canceled)return state;updateState(await core('background',{path:result.filePaths[0]}));}setTimeout(()=>refreshWindow().catch(report),120);return state;
 }
 if(name==='export'){const result=await dialog.showSaveDialog(main,{defaultPath:'FrostTodo-备份.json',filters:[{name:'待办备份',extensions:['json']}]});if(!result.canceled)await fs.promises.writeFile(result.filePath,await core('export'),'utf8');return !result.canceled;}
 if(name==='import'){const result=await dialog.showOpenDialog(main,{properties:['openFile'],filters:[{name:'待办备份',extensions:['json']}]});if(result.canceled)return state;const file=result.filePaths[0];if((await fs.promises.stat(file)).size>40*1024*1024)throw new Error('备份文件超过 40 MB');updateState(await core('import',{text:await fs.promises.readFile(file,'utf8')}));return state;}
 if(name==='snooze'){updateState(await core('snooze',arg));return state;}
 throw new Error('Unknown action');
}
if(!app.requestSingleInstanceLock()){app.quit();}else{
 app.on('second-instance',show);app.on('window-all-closed',()=>{});app.on('activate',show);
 app.on('before-quit',e=>{if(!quitting){e.preventDefault();command('quit');}});
 app.whenReady().then(async()=>{
  protocol.handle('frost',request=>{try{let file=assetPath(root,request.url);if(!app.isPackaged){const relative=path.relative(root,file);if(relative==='icons.json')file=path.join(assetRoot,'Assets','GptIcons.json');else if(relative.startsWith('Fonts'+path.sep)||relative.startsWith('Assets'+path.sep))file=path.join(assetRoot,relative);}return net.fetch(pathToFileURL(file).toString());}catch{return new Response('Not found',{status:404});}});
  session.defaultSession.setPermissionRequestHandler((_wc,_permission,callback)=>callback(false));session.defaultSession.setPermissionCheckHandler(()=>false);
  bridge=new Bridge(runtimePath(),profile,seeded);state=await core('snapshot');
  // New Chromium profiles always start with desktop typography.
  if(!fs.existsSync(path.join(profile,'chromium-profile-v1'))){updateState(await core('settings',{Font:'desktop',TextSize:16,TextWeight:'normal',TextRendering:'desktop',TextContrast:100,TextSpacing:0,TextLineGap:4}));await fs.promises.writeFile(path.join(profile,'chromium-profile-v1'),'1');}
  ipcMain.handle('frost:action',action);
  for(const key of ['Control+Alt+Space','Control+Alt+T','Control+Shift+F12'])if(globalShortcut.register(key,()=>{if(main?.isVisible())command('collapse');else show();})){hotkey=key.replace('Control','Ctrl');break;}
  if(!hotkey)hotkey='托盘图标';await createMain();
  tray=new Tray(nativeImage.createFromPath(path.join(assetRoot,'Assets','FrostTodo.ico')));tray.setToolTip('FrostTodo Chromium');tray.on('click',show);tray.on('right-click',()=>tray.popUpContextMenu(Menu.buildFromTemplate(menuTemplate())));tray.on('balloon-click',()=>command({reminders:pendingNotices}));
  nativeTheme.on('updated',()=>{sendState();refreshWindow().catch(report);});
  timer=setInterval(async()=>{if(busyTick||quitting)return;busyTick=true;try{const result=await core('tick');if(result.Changed)updateState(result.State);if(result.Notices.length)notify(result.Notices);if(state.Data.Settings.Mode==='bottom'&&main?.isVisible()&&Date.now()>movingUntil)await applyWindow();}catch(e){report(e);}finally{busyTick=false;}},2000);
 }).catch(async error=>{dialog.showErrorBox('FrostTodo 无法启动',String(error.message||error)+'\n现有数据文件已保留。');await quit();});
}

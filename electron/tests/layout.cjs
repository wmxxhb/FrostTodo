// Run with Electron; renders isolated layout fixtures without showing any window.
const {app,BrowserWindow}=require('electron');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict');
const report=process.argv[2]||path.join(os.tmpdir(),'frost-layout-report.json');
app.setPath('userData',fs.mkdtempSync(path.join(os.tmpdir(),'frost-layout-')));
app.whenReady().then(async()=>{
 const css=fs.readFileSync(path.resolve(__dirname,'../styles.css'),'utf8');
 const win=new BrowserWindow({show:false,frame:false,width:420,height:640,webPreferences:{offscreen:true,sandbox:true,contextIsolation:true,nodeIntegration:false}});
 const results=[];
 for(const [width,height] of [[300,340],[420,380],[420,640]])for(const size of [12,16,22])for(const page of ['appearance','settings','edit','main'])for(const notice of [false,true]){
  win.setContentSize(width,height);
  const tabs=page==='main'?'<button>所有任务</button><button>今日任务</button>':'';
  const content=Array.from({length:35},(_,i)=>`<label class="field"><span class="label">设置或任务 ${i+1}</span><input value="内容示例"></label>`).join('');
  const html=`<!doctype html><html lang="zh-CN"><head><style>${css}</style></head><body style="--size:${size}px"><div id="frame"><header id="header">${page}</header><nav id="tabs">${tabs}</nav><main id="content">${content}</main><div id="toast" role="status" ${notice?'':'hidden'}><div class="row"><span>已恢复默认设置。这是一条较长的操作提示。</span><button>撤销</button></div></div><footer id="footer"><span></span><button id="done">完成</button></footer></div></body></html>`;
  await win.loadURL('data:text/html;charset=utf-8,'+encodeURIComponent(html));
  for(const position of ['top','bottom']){
   const bounds=await win.webContents.executeJavaScript(`(()=>{const c=document.querySelector('#content');c.scrollTop=${position==='bottom'?'c.scrollHeight':'0'};const t=document.querySelector('#toast').getBoundingClientRect(),f=document.querySelector('#footer').getBoundingClientRect(),b=document.querySelector('#done').getBoundingClientRect();return{toastBottom:t.bottom,footerTop:f.top,footerBottom:f.bottom,buttonRight:b.right,buttonBottom:b.bottom,buttonLeft:b.left,height:innerHeight,width:innerWidth,scrollHeight:c.scrollHeight,clientHeight:c.clientHeight}})()`);
   const label=JSON.stringify({width,height,size,page,notice,position,...bounds});
   if(notice)assert.ok(bounds.toastBottom<=bounds.footerTop+.5,label);
   assert.ok(bounds.footerBottom<=bounds.height+.5&&bounds.buttonBottom<=bounds.height+.5,label);
   assert.ok(bounds.buttonLeft>=0&&bounds.buttonRight<=bounds.width,label);
   assert.ok(bounds.clientHeight>0&&bounds.scrollHeight>bounds.clientHeight,label);
   results.push({width,height,size,page,notice,position,...bounds});
  }
 }
 fs.writeFileSync(report,JSON.stringify({pass:results.length,cases:results},null,2));win.destroy();app.quit();
}).catch(error=>{fs.writeFileSync(report,JSON.stringify({error:String(error.stack||error)},null,2));app.exit(1);});

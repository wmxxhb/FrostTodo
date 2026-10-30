'use strict';
const path=require('node:path');
function assetPath(root,url){
 const parsed=new URL(url);if(parsed.protocol!=='frost:'||parsed.hostname!=='app')throw new Error('Invalid asset origin');
 const relative=decodeURIComponent(parsed.pathname).replace(/^\//,'')||'index.html';
 const file=path.resolve(root,relative);if(!file.startsWith(path.resolve(root)+path.sep))throw new Error('Invalid asset path');
 if(relative.includes('\\')||relative.includes(':')||! /^(index\.html|dot\.html|renderer\.js|view-model\.mjs|dot\.js|styles\.css|dot\.css|icons\.json|Fonts\/[\w.-]+|Assets\/FrostTodo\.(png|ico))$/.test(relative))throw new Error('Asset is not exposed');return file;
}
function safeBounds(saved,displays){
 const width=Math.max(300,Math.min(1600,Math.round(saved.Width||420))),height=Math.max(340,Math.min(1800,Math.round(saved.Height||640)));
 let x=Math.round(saved.Left),y=Math.round(saved.Top);const primary=displays[0].workArea;
 if(!Number.isFinite(x)||!Number.isFinite(y)||x<0&&y<0||!displays.some(({workArea:a})=>x+width>a.x+40&&x<a.x+a.width-40&&y+height>a.y+40&&y<a.y+a.height-40)){x=primary.x+primary.width-width-32;y=primary.y+48;}
 return {x,y,width,height};
}
function countFor(state,view,current){if(view==='none')return -1;if(view==='current')return current;return state.Data.Tasks.filter(t=>view.startsWith('tag:')?!t.Done&&t.Tags.includes(view.slice(4)):state.Views[t.Id]?.includes(view)).length;}
function transparentFor(p){return p.Opacity<100&&!p.BackgroundImage;}
// Electron screen and window coordinates are already in DIPs, including mixed-DPI monitors.
function dotInArea(bounds,area,snap){
 const width=bounds.width||52,height=bounds.height||52;
 const left=area.x+6,right=Math.max(left,area.x+area.width-width-6),top=area.y+6,bottom=Math.max(top,area.y+area.height-height-6);
 let x=Math.max(left,Math.min(right,Math.round(bounds.x))),y=Math.max(top,Math.min(bottom,Math.round(bounds.y)));
 if(snap){if(Math.min(Math.abs(x-left),Math.abs(right-x))<=35)x=x-left<=right-x?left:right;if(Math.min(Math.abs(y-top),Math.abs(bottom-y))<=35)y=y-top<=bottom-y?top:bottom;}
 return {x,y};
}
function snapDot(bounds,area){return dotInArea(bounds,area,true);}
function restoreDot(saved,displays,primary=displays[0]){
 const x=saved.DotLeft,y=saved.DotTop;
 const fallback=()=>dotInArea({x:primary.workArea.x+primary.workArea.width-58,y:primary.workArea.y+primary.workArea.height*.6},primary.workArea,false);
 if(!Number.isFinite(x)||!Number.isFinite(y)||x===-1&&y===-1)return fallback();
 const overlap=area=>Math.max(0,Math.min(x+52,area.x+area.width)-Math.max(x,area.x))*Math.max(0,Math.min(y+52,area.y+area.height)-Math.max(y,area.y));
 const best=displays.reduce((a,b)=>overlap(a.workArea)>=overlap(b.workArea)?a:b);
 return overlap(best.workArea)>0?dotInArea({x,y},best.workArea,false):fallback();
}
module.exports={assetPath,safeBounds,countFor,transparentFor,snapDot,restoreDot};

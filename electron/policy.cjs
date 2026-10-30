'use strict';
const path=require('node:path');
const {fileURLToPath}=require('node:url');
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
module.exports={assetPath,safeBounds,countFor,transparentFor};

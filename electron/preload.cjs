'use strict';
const {contextBridge,ipcRenderer}=require('electron');
const actions=new Set(['snapshot','resetAppearance','saveTask','complete','delete','undo','reorder','settings','tagAdd','tagRemove','tagVisible','background','import','export','collapse','quit','menu','route','currentCount','dotMove','restore','dotMenu','snooze']);
contextBridge.exposeInMainWorld('frost',Object.freeze({
 call:(action,arg=null)=>{if(!actions.has(action))return Promise.reject(new Error('Unknown action'));return ipcRenderer.invoke('frost:action',action,arg);},
 onState:callback=>{const handler=(_event,state)=>callback(state);ipcRenderer.on('frost:state',handler);return ()=>ipcRenderer.removeListener('frost:state',handler);},
 onAction:callback=>{const handler=(_event,action)=>callback(action);ipcRenderer.on('frost:command',handler);return ()=>ipcRenderer.removeListener('frost:command',handler);}
}));

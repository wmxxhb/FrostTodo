'use strict';
const {spawn}=require('node:child_process');
const readline=require('node:readline');
class Bridge {
 constructor(exe,dir,seed){
  this.id=0;this.pending=new Map();this.closed=false;
  this.child=spawn(exe,[dir,...(seed?[seed]:[])],{windowsHide:true,stdio:['pipe','pipe','pipe']});
  this.child.stdout.setEncoding('utf8');this.child.stdin.on('error',()=>{});
  readline.createInterface({input:this.child.stdout}).on('line',line=>{
   let message;try{message=JSON.parse(line.replace(/^\uFEFF/,''));}catch{return this.fail(new Error('任务服务返回无效响应'));}
   if(message.fatal)return this.fail(new Error(message.fatal));
   const request=this.pending.get(message.id);if(!request)return;this.pending.delete(message.id);clearTimeout(request.timer);
   message.ok?request.resolve(message.result):request.reject(new Error(message.error));
  });
  this.child.on('error',e=>this.fail(e));this.child.on('exit',()=>this.fail(new Error('任务服务已结束，请重启应用')));
  this.child.stderr.on('data',()=>{});
 }
 fail(error){this.closed=true;this.failure=error;for(const request of this.pending.values()){clearTimeout(request.timer);request.reject(error);}this.pending.clear();}
 call(op,arg=null){
  if(this.closed)return Promise.reject(this.failure||new Error('任务服务已关闭'));
  const id=++this.id;
  return new Promise((resolve,reject)=>{const timer=setTimeout(()=>{this.pending.delete(id);reject(new Error('任务服务响应超时，请重试'));},30000);this.pending.set(id,{resolve,reject,timer});this.child.stdin.write(JSON.stringify({id,op,arg})+'\n');});
 }
 async close(){if(this.closed)return;try{await this.call('close');}finally{this.child.stdin.end();}}
}
module.exports={Bridge};

'use strict';
const fs=require('node:fs/promises'),path=require('node:path'),packager=require('@electron/packager');
(async()=>{
 const root=__dirname,stage=await fs.mkdtemp(path.join(root,'app-stage-'));
 for(const name of ['main.cjs','bridge.cjs','policy.cjs','preload.cjs','renderer.js','view-model.mjs','index.html','styles.css','dot.html','dot.js','dot.css'])await fs.copyFile(path.join(root,name),path.join(stage,name));
 const metadata={...require('./package.json')},electronVersion=metadata.devDependencies.electron;delete metadata.devDependencies;delete metadata.scripts;await fs.writeFile(path.join(stage,'package.json'),JSON.stringify(metadata,null,2));
 for(const name of ['Assets','ThirdParty'])await fs.cp(path.join(root,'..',name),path.join(stage,name),{recursive:true});
 await fs.copyFile(path.join(root,'..','Assets','GptIcons.json'),path.join(stage,'icons.json'));
 // Native child programs cannot execute inside an asar archive.
 await fs.cp(path.join(root,'runtime'),path.join(stage,'runtime'),{recursive:true});
 const outputs=await packager({dir:stage,out:path.join(root,'dist'),name:'FrostTodo',platform:'win32',arch:'x64',electronVersion,electronZipDir:process.env.FROST_ELECTRON_ZIP_DIR,icon:path.join(root,'..','Assets','FrostTodo.ico'),overwrite:true,asar:{unpackDir:'runtime'},prune:true,appVersion:metadata.version,win32metadata:{CompanyName:'wmxxhb',FileDescription:'FrostTodo',ProductName:'FrostTodo'},quiet:true});
 console.log(outputs[0]);
})().catch(e=>{console.error(e.message);process.exitCode=1;});

'use strict';
const {test}=require('node:test'),assert=require('node:assert/strict');
const {snapDot,restoreDot}=require('../policy.cjs');
test('released dot snaps near each work-area edge and leaves free positions alone',()=>{
 const area={x:0,y:0,width:1920,height:1040};
 assert.deepEqual(snapDot({x:20,y:200,width:52,height:52},area),{x:6,y:200});
 assert.deepEqual(snapDot({x:1845,y:200,width:52,height:52},area),{x:1862,y:200});
 assert.deepEqual(snapDot({x:400,y:25,width:52,height:52},area),{x:400,y:6});
 assert.deepEqual(snapDot({x:400,y:970,width:52,height:52},area),{x:400,y:982});
 assert.deepEqual(snapDot({x:400,y:200,width:52,height:52},area),{x:400,y:200});
});
test('dot stays above taskbars and visible at corners or beyond screen limits',()=>{
 const area={x:80,y:40,width:1200,height:700};
 assert.deepEqual(snapDot({x:-300,y:-200,width:52,height:52},area),{x:86,y:46});
 assert.deepEqual(snapDot({x:3000,y:2000,width:52,height:52},area),{x:1222,y:682});
});
test('negative monitor coordinates and DIP coordinates work without double DPI scaling',()=>{
 const displays=[{workArea:{x:0,y:0,width:1920,height:1040},scaleFactor:1},{workArea:{x:-1536,y:-864,width:1536,height:824},scaleFactor:1.25}];
 assert.deepEqual(restoreDot({DotLeft:-1400,DotTop:-700},displays,displays[0]),{x:-1400,y:-700});
 assert.deepEqual(snapDot({x:-1520,y:-800,width:52,height:52},displays[1].workArea),{x:-1530,y:-800});
 const recovered=restoreDot({DotLeft:30000,DotTop:30000},displays,displays[0]);assert.equal(recovered.x,1862);assert.ok(recovered.y>=6&&recovered.y<=982);
});
test('tag type follows global font size or an explicit saved override',async()=>{
 const {tabFontSize}=await import('../view-model.mjs');
 assert.equal(tabFontSize({TextSize:12,TabTextSize:0}),10.5);
 assert.equal(tabFontSize({TextSize:22,TabTextSize:0}),19.25);
 assert.equal(tabFontSize({TextSize:22,TabTextSize:16}),16);
});

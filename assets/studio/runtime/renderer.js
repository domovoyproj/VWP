import {program,grid,texture,bindTexture,target,image,quadVertex} from './gl.js';
import {Masks,channels} from './masks.js';
import {layerVertex,layerFragment,bloomFragment,finalFragment,particleVertex,particleFragment} from './shaders.js';

const canvas=document.querySelector('#scene'),brush=document.querySelector('#brush');
const notify=data=>window.chrome?.webview?.postMessage(data);
const tau=Math.PI*2;
let current=null,loading=0,clockTimer=0;

class Renderer{
  constructor(project,base,quality,options={}){
    this.project=structuredClone(project);this.base=base;this.quality=quality;this.options=options;this.framing=options.framing||{fit:'Fill',focusX:.5,focusY:.5};
    this.settings={...project.settings};this.paused=!!options.manual;this.manual=!!options.manual;
    this.time=0;this.cursor=[0,0];this.targetCursor=[0,0];this.audio=0;this.layers=[];this.frames=0;this.decodedBytes=0;
    this.lastTick=0;this.lastDraw=0;this.samples=[];this.frameCosts=[];this.textureBytes=0;this.maskTool=null;
    this.gl=canvas.getContext('webgl2',{alpha:false,antialias:false,powerPreference:'high-performance',premultipliedAlpha:false,depth:false,stencil:false,preserveDrawingBuffer:false});
    if(!this.gl)throw Error('Для живых сцен нужен WebGL 2. Обнови драйвер видеокарты и WebView2.');
    const gl=this.gl;this.hdr=!!gl.getExtension('EXT_color_buffer_float');
    this.layerShader=program(gl,layerVertex,layerFragment);this.bloomShader=program(gl,quadVertex,bloomFragment);this.finalShader=program(gl,quadVertex,finalFragment);this.particleShader=program(gl,particleVertex,particleFragment);
    this.mesh=grid(gl);this.quad=grid(gl,1,1);this.buffers=[];
    const debug=gl.getExtension('WEBGL_debug_renderer_info');this.adapter=debug?gl.getParameter(debug.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER);
    this.makeParticles();
  }
  async initialize(){
    const gl=this.gl;
    for(const definition of this.project.layers){
      const img=await image(new URL(definition.source,this.base));
      if(img.width>4096||img.height>4096)throw Error('Изображение слоя превышает 4096 пикселей.');
      this.decodedBytes+=img.width*img.height*4;if(this.decodedBytes>320*1024*1024)throw Error('Исходные слои превышают бюджет декодирования 320 МБ. Уменьши их разрешение.');
      const bitmap=this.scaledImage(img),bytes=bitmap.width*bitmap.height*4*4/3+1024*576*4;
      if(this.textureBytes+bytes>240*1024*1024)throw Error('Сцена превышает лимит текстур 240 МБ. Уменьши изображения или количество слоёв.');
      const masks=new Masks(gl);await masks.load(definition,this.base);
      const handle=texture(gl,bitmap,{mipmap:true,premult:true});
      this.textureBytes+=bytes;
      this.layers.push({definition,image:img,texture:handle,masks});
    }
    if(this.textureBytes>300*1024*1024)throw Error('Сцена требует слишком много памяти видеокарты.');
    this.resize();this.render();this.schedule();
    return this.stats();
  }
  scaledImage(img){
    const scale=Math.min(1,this.quality.maxHeight*16/9/img.width,this.quality.maxHeight/img.height);
    if(scale>=1)return img;
    const output=document.createElement('canvas');output.width=Math.max(1,Math.round(img.width*scale));output.height=Math.max(1,Math.round(img.height*scale));
    const context=output.getContext('2d');context.imageSmoothingQuality='high';context.drawImage(img,0,0,output.width,output.height);return output;
  }
  changeQuality(value){
    const changed=this.quality.maxHeight!==value.maxHeight;if(this.quality.fps!==value.fps){this.samples=[];this.lastDraw=0;}this.quality=value;
    if(changed){this.textureBytes=0;for(const layer of this.layers){this.gl.deleteTexture(layer.texture);const img=this.scaledImage(layer.image);layer.texture=texture(this.gl,img,{mipmap:true,premult:true});this.textureBytes+=img.width*img.height*4*4/3+1024*576*4;}}
    this.resize();this.render();
  }
  resize(){
    const cssWidth=canvas.clientWidth||960,cssHeight=canvas.clientHeight||540;
    const ratio=Math.min(devicePixelRatio||1,this.quality.maxHeight/cssHeight);
    const width=Math.max(2,Math.round(cssWidth*ratio)),height=Math.max(2,Math.round(cssHeight*ratio));
    if(canvas.width===width&&canvas.height===height&&this.buffers.length&&this.bufferMode===this.quality.bloom)return;
    canvas.width=width;canvas.height=height;brush.width=cssWidth;brush.height=cssHeight;
    this.buffers.forEach(buffer=>buffer.dispose());this.buffers=[];
    this.sceneTarget=target(this.gl,width,height,this.hdr);this.bufferMode=this.quality.bloom;
    const bloomWidth=this.quality.bloom?Math.max(2,width>>1):1,bloomHeight=this.quality.bloom?Math.max(2,height>>1):1;
    this.bloomA=target(this.gl,bloomWidth,bloomHeight,this.hdr);
    this.bloomB=target(this.gl,bloomWidth,bloomHeight,this.hdr);
    this.buffers.push(this.sceneTarget,this.bloomA,this.bloomB);
    this.reframe();
    this.render();
  }
  reframe(){
    const width=canvas.width,height=canvas.height,aspect=16/9,scale=(this.framing.fit==='Fit'?Math.min:Math.max)(width/(aspect*1000),height/1000),stageWidth=aspect*1000*scale,stageHeight=1000*scale;
    this.frame=[(width-stageWidth)/width*this.framing.focusX,(height-stageHeight)/height*this.framing.focusY,stageWidth/width,stageHeight/height];
  }
  makeParticles(){
    if(this.particleVao){this.gl.deleteVertexArray(this.particleVao);this.gl.deleteBuffer(this.particleBuffer);}
    const gl=this.gl,count=Math.min(400,this.project.weather?.count||0),values=new Float32Array(count*4);
    let seed=937187;for(let i=0;i<values.length;i++){seed=(Math.imul(seed,1664525)+1013904223)>>>0;values[i]=seed/4294967296}
    this.particleCount=count;this.particleVao=gl.createVertexArray();this.particleBuffer=gl.createBuffer();gl.bindVertexArray(this.particleVao);gl.bindBuffer(gl.ARRAY_BUFFER,this.particleBuffer);gl.bufferData(gl.ARRAY_BUFFER,values,gl.STATIC_DRAW);gl.enableVertexAttribArray(0);gl.vertexAttribPointer(0,4,gl.FLOAT,false,0,0);gl.bindVertexArray(null);
  }
  begin(target){const gl=this.gl;gl.bindFramebuffer(gl.FRAMEBUFFER,target?.framebuffer||null);gl.viewport(0,0,target?.width||canvas.width,target?.height||canvas.height)}
  render(){
    if(this.disposed||!this.sceneTarget)return;
    const started=performance.now(),gl=this.gl,s=this.settings;
    const phase=tau*((this.time%this.project.duration)/this.project.duration);
    this.begin(this.sceneTarget);gl.disable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);gl.clearColor(.002,.004,.008,1);gl.clear(gl.COLOR_BUFFER_BIT);gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);
    const p=this.layerShader;p.use();p.i('uImage',0);p.i('uMask',1);p.v2('uResolution',[canvas.width,canvas.height]);p.v4('uFrame',this.frame);p.f('uTime',phase);p.v2('uCursor',this.cursor);p.f('uParallax',this.maskTool?0:s.parallax);p.f('uWind',s.wind*s.motion);p.f('uMotion',s.motion);p.f('uAudio',s.audioReactive?this.audio:0);
    this.layers.forEach((layer,index)=>{
      const d=layer.definition;if(!d.visible||d.opacity<=0)return;
      bindTexture(gl,0,layer.texture);bindTexture(gl,1,layer.masks.texture);p.v4('uRect',d.rect);p.f('uOpacity',d.opacity);p.f('uDepth',d.depth);p.f('uStrength',d.strength);p.v2('uSun',d.sun||[.75,.4]);p.f('uWaterline',d.waterline??.48);p.i('uKind',{foliage:1,cloth:2,character:3,rain:4}[d.motion]||0);p.i('uAnchor',{top:0,bottom:1,center:2}[d.anchor]??1);p.i('uMaskView',this.maskTool?.layer===index?this.maskTool.channel:-1);this.mesh.draw();
    });
    this.drawParticles(phase);gl.disable(gl.BLEND);
    if(this.quality.bloom&&s.bloom>.001){
      this.begin(this.bloomA);const bloom=this.bloomShader;bloom.use();bindTexture(gl,0,this.sceneTarget.handle);bloom.i('uSource',0);bloom.v2('uStep',[1/canvas.width,0]);bloom.i('uExtract',1);this.quad.draw();
      this.begin(this.bloomB);bindTexture(gl,0,this.bloomA.handle);bloom.v2('uStep',[0,1/this.bloomA.height]);bloom.i('uExtract',0);this.quad.draw();
    }else{this.begin(this.bloomB);gl.clearColor(0,0,0,1);gl.clear(gl.COLOR_BUFFER_BIT)}
    this.begin(null);const final=this.finalShader;final.use();bindTexture(gl,0,this.sceneTarget.handle);bindTexture(gl,1,this.bloomB.handle);final.i('uScene',0);final.i('uBloom',1);final.f('uExposure',s.exposure);final.f('uBloomAmount',s.bloom);this.quad.draw();
    this.frames++;this.frameCosts.push(performance.now()-started);if(this.frameCosts.length>240)this.frameCosts.shift();
  }
  drawParticles(phase){
    const weather=this.project.weather;if(!weather||weather.kind==='none'||this.settings.motion===0)return;
    const gl=this.gl,p=this.particleShader;p.use();p.f('uTime',phase);p.f('uHeight',canvas.height*this.frame[3]);p.f('uKind',{snow:1,fireflies:2,dust:3}[weather.kind]||1);p.f('uWind',this.settings.wind);p.f('uDensity',this.quality.particles);p.v4('uRegion',weather.region);p.v4('uFrame',this.frame);p.v2('uCursor',this.cursor);p.v3('uColor',weather.color.match(/\w\w/g).map(hex=>Math.pow(parseInt(hex,16)/255,2.2)));gl.bindVertexArray(this.particleVao);gl.drawArrays(gl.POINTS,0,Math.floor(this.particleCount*this.quality.particles));
  }
  schedule(){
    if(this.disposed||this.paused||this.manual)return;
    this.animation=requestAnimationFrame(now=>{
      if(this.disposed||this.paused)return;
      const delta=this.lastTick?Math.min((now-this.lastTick)/1000,.1):0;this.lastTick=now;this.time+=delta;
      const smoothing=1-Math.exp(-delta*5);for(let i=0;i<2;i++)this.cursor[i]+=(this.targetCursor[i]-this.cursor[i])*smoothing;
      if(now-this.lastDraw>=1000/this.quality.fps-1.5){
        if(this.lastDraw){this.samples.push(now-this.lastDraw);if(this.samples.length>240)this.samples.shift()}
        this.lastDraw=now;this.render();
      }
      this.schedule();
    });
  }
  pause(value){this.paused=value;cancelAnimationFrame(this.animation);this.lastTick=0;this.lastDraw=0;if(!value)this.schedule();else this.render()}
  seek(seconds){this.time=Math.max(0,seconds);this.render();return {time:this.time,frames:this.frames}}
  settingsChanged(value){Object.assign(this.settings,value);Object.assign(this.project.settings,value);this.render();updateClock()}
  weatherChanged(value){this.project.weather=structuredClone(value);this.makeParticles();this.render()}
  layerChanged(index,value){if(!this.layers[index])return;Object.assign(this.layers[index].definition,value);Object.assign(this.project.layers[index],value);this.render()}
  stats(){
    const sorted=[...this.samples].sort((a,b)=>a-b),costs=[...this.frameCosts].sort((a,b)=>a-b);const percentile=(values,p)=>values[Math.min(values.length-1,Math.floor(values.length*p))]||0;
    return {adapter:this.adapter,hdr:this.hdr,width:canvas.width,height:canvas.height,frames:this.frames,time:this.time,paused:this.paused,textureMb:this.textureBytes/1048576,targetMb:this.buffers.reduce((sum,b)=>sum+b.bytes,0)/1048576,fps:this.paused?0:sorted.length?1000/(sorted.reduce((a,b)=>a+b,0)/sorted.length):0,frameP95:percentile(sorted,.95),submitP95:percentile(costs,.95),layers:this.layers.length,frame:this.frame};
  }
  masks(){return this.layers.map(layer=>layer.masks.export())}
  state(){return {project:this.project,settings:this.settings,masks:this.masks(),time:this.time,paused:this.paused}}
  async restore(state){
    const wasPaused=this.paused;this.pause(true);
    if(state.project){this.project=state.project;state.project.layers.forEach((definition,i)=>{if(this.layers[i])this.layers[i].definition=definition})}
    if(state.settings)Object.assign(this.settings,state.settings);
    for(let i=0;i<(state.masks||[]).length;i++)for(let channel=0;channel<4;channel++){
      const value=state.masks[i][channels[channel]];if(!value||!this.layers[i])continue;
      const img=await image('data:image/png;base64,'+value);this.layers[i].masks.canvases[channel].getContext('2d').drawImage(img,0,0,1024,576);
    }
    this.layers.forEach(layer=>layer.masks.upload());this.time=state.time||0;this.pause(state.paused??wasPaused);
  }
  dispose(){
    this.disposed=true;cancelAnimationFrame(this.animation);const gl=this.gl;this.layers.forEach(layer=>{gl.deleteTexture(layer.texture);layer.masks.dispose()});this.buffers.forEach(buffer=>buffer.dispose());[this.layerShader,this.bloomShader,this.finalShader,this.particleShader].forEach(p=>p.dispose());this.mesh.dispose();this.quad.dispose();gl.deleteVertexArray(this.particleVao);gl.deleteBuffer(this.particleBuffer);
  }
}

async function load(payload){
  const generation=++loading;current?.dispose();current=null;
  const renderer=new Renderer(payload.project,payload.base,payload.quality,payload.options);
  try{const stats=await renderer.initialize();if(generation!==loading){renderer.dispose();return}current=renderer;updateClock();notify({type:'ready',stats})}
  catch(error){renderer.dispose();fail(error)}
}
function fail(error){const label=document.querySelector('#failure');label.hidden=false;label.textContent=String(error.message||error);notify({type:'error',message:label.textContent})}
function updateClock(){const element=document.querySelector('#clock');element.hidden=!current?.settings.clock;if(element.hidden)return;const now=new Date();element.querySelector('strong').textContent=now.toLocaleTimeString('ru-RU',{hour:'2-digit',minute:'2-digit'});element.querySelector('span').textContent=now.toLocaleDateString('ru-RU',{weekday:'long',day:'numeric',month:'long'})}

window.studio={load,live:()=>{if(current){current.manual=false;current.pause(false)}},seek:seconds=>current?.seek(seconds),pause:value=>current?.pause(value),settings:value=>current?.settingsChanged(value),weather:value=>current?.weatherChanged(value),layer:(index,value)=>current?.layerChanged(index,value),stats:()=>current?.stats(),project:()=>current?.project,state:()=>current?.state(),restore:state=>current?.restore(state),masks:()=>current?.masks(),quality:value=>current?.changeQuality(value),tool:value=>{if(current){current.maskTool=value;brush.hidden=!value;current.render()}},undo:()=>{if(current?.maskTool){current.layers[current.maskTool.layer].masks.history(true);current.render();notify({type:'dirty'})}},redo:()=>{if(current?.maskTool){current.layers[current.maskTool.layer].masks.history(false);current.render();notify({type:'dirty'})}}};
window.chrome?.webview?.addEventListener('message',event=>{
  const data=event.data;
  if(data.type==='load')load(data);
  if(data.type==='pause')current?.pause(data.value);
  if(data.type==='settings')current?.settingsChanged(data.value);
  if(data.type==='input'&&current){current.targetCursor=data.cursor;current.audio=data.audio||0}
  if(data.type==='quality'&&current)current.changeQuality(data.value);
  if(data.type==='framing'&&current){current.framing=data.value;current.reframe();current.render()}
});
addEventListener('resize',()=>current?.resize());
canvas.addEventListener('pointermove',event=>{if(current&&current.options.preview){const rect=canvas.getBoundingClientRect();current.targetCursor=[(event.clientX-rect.left)/rect.width-.5,(event.clientY-rect.top)/rect.height-.5]}});
canvas.addEventListener('webglcontextlost',event=>{event.preventDefault();if(current){current.pause(true);notify({type:'contextlost'})}});
canvas.addEventListener('webglcontextrestored',()=>notify({type:'reload'}));
let painting=false,lastPoint=null;
function paint(event){
  if(!painting||!current?.maskTool)return;
  const tool=current.maskTool,rect=brush.getBoundingClientRect(),layer=current.layers[tool.layer];
  const fx=current.frame,x=((event.clientX-rect.left)/rect.width-fx[0])/fx[2],y=((event.clientY-rect.top)/rect.height-fx[1])/fx[3];
  const u=(x-layer.definition.rect[0])/layer.definition.rect[2],v=(y-layer.definition.rect[1])/layer.definition.rect[3];
  if(u<0||u>1||v<0||v>1)return;
  const from=lastPoint||[u,v],steps=Math.max(1,Math.ceil(Math.hypot(u-from[0],v-from[1])*600));
  for(let i=1;i<=steps;i++)layer.masks.paint(tool.channel,from[0]+(u-from[0])*i/steps,from[1]+(v-from[1])*i/steps,tool.radius||.045,!!tool.erase,layer.definition.rect[3]/layer.definition.rect[2]);
  lastPoint=[u,v];layer.masks.upload(tool.channel);current.render();
}
brush.addEventListener('pointerdown',event=>{if(!current?.maskTool)return;brush.focus({preventScroll:true});notify({type:'focuscanvas'});painting=true;lastPoint=null;brush.setPointerCapture(event.pointerId);current.layers[current.maskTool.layer].masks.begin(current.maskTool.channel);paint(event)});
canvas.addEventListener('pointerdown',()=>{canvas.focus({preventScroll:true});notify({type:'focuscanvas'})});
brush.addEventListener('pointermove',paint);
brush.addEventListener('pointerup',()=>{painting=false;lastPoint=null;notify({type:'dirty'})});
brush.addEventListener('pointermove',event=>{
  if(!current?.maskTool)return;const ctx=brush.getContext('2d'),rect=brush.getBoundingClientRect(),layer=current.layers[current.maskTool.layer];ctx.clearRect(0,0,brush.width,brush.height);ctx.beginPath();ctx.arc(event.clientX-rect.left,event.clientY-rect.top,(current.maskTool.radius||.045)*rect.height*layer.definition.rect[3]*current.frame[3],0,tau);ctx.strokeStyle='rgba(0,0,0,.65)';ctx.lineWidth=3;ctx.stroke();ctx.strokeStyle='rgba(255,255,255,.9)';ctx.lineWidth=1;ctx.stroke();
});
brush.addEventListener('pointerleave',()=>brush.getContext('2d').clearRect(0,0,brush.width,brush.height));
addEventListener('keydown',event=>{
  if(event.ctrlKey&&event.code==='KeyZ'){event.preventDefault();event.shiftKey?window.studio.redo():window.studio.undo();}
  if(event.code==='Space'){event.preventDefault();notify({type:'togglepause'});}
  if(event.code==='Escape'){event.preventDefault();notify({type:'escape'});}
});
clockTimer=setInterval(updateClock,1000);
setInterval(()=>{if(current)notify({type:'stats',stats:current.stats()})},2000);
addEventListener('unhandledrejection',event=>fail(event.reason));addEventListener('error',event=>fail(event.error||event.message));
notify({type:'boot'});

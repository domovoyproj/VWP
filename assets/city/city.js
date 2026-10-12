import * as T from 'three';
import { createRoad } from './road.js';
import { mergeGeometries } from './vendor/BufferGeometryUtils.js';
import { HDRLoader } from './vendor/HDRLoader.js';
import { createArchitecture } from './architecture.js';
import { createActors } from './actors.js';
import { createCanal } from './canal.js';

const params=new URLSearchParams(location.search),preview=params.get('preview')==='1',quality=params.get('quality')||'Quality';
const manual=params.get('manual')==='1';
const adaptive=quality==='Auto',high=quality==='Quality',eco=quality==='Eco',fps=quality==='Quality'?60:eco?15:30;
const canvas=document.querySelector('#scene'),send=o=>window.chrome?.webview?.postMessage(o);
const palettes=[{name:'NEON WARD',a:0x6cbdcf,b:0xda699b,fog:0x192b40},{name:'SAKURA QUARTER',a:0xdf8dad,b:0x959bd8,fog:0x29283e},{name:'GLACIER DISTRICT',a:0x83caba,b:0x6aa4ce,fog:0x152f3e}];
let palette=Number(params.get('palette')||0)%3,paused=false,active=false,flying=false,raining=true,yaw=0,pitch=-.025,elapsed=0,frames=0,auto=true,distance=0;
let world=new T.Vector3(5.55,1.92,22),origin=new T.Vector3(),lastTime=0,lastRender=0,lastStats=0,steps=new Set(),chunks=new Map(),renderCalls=0,renderTriangles=0;
const seed=(x,z)=>{let s=(Math.imul(x,73856093)^Math.imul(z,19349663)^81721)>>>0;return()=>{s=(Math.imul(s,1664525)+1013904223)>>>0;return s/4294967296}};
const renderer=new T.WebGLRenderer({canvas,antialias:high,powerPreference:eco?'low-power':'high-performance'});
renderer.info.autoReset=false;
function pixelBudget(){return quality==='Quality'?devicePixelRatio:eco?Math.min(.65,1280/innerWidth,720/innerHeight):Math.min(devicePixelRatio,1,1600/innerWidth,900/innerHeight);}
renderer.setPixelRatio(pixelBudget());renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1;
renderer.shadowMap.enabled=!eco;renderer.shadowMap.type=T.PCFSoftShadowMap;renderer.shadowMap.autoUpdate=false;renderer.shadowMap.needsUpdate=true;
const scene=new T.Scene(),camera=new T.PerspectiveCamera(62,1,.15,900);
const ambient=new T.HemisphereLight(0x8baccb,0x17202c,.86);scene.add(ambient);
const moonlight=new T.DirectionalLight(0xb3c7e4,1.6);moonlight.position.set(-40,120,-65);scene.add(moonlight);scene.add(moonlight.target);moonlight.castShadow=!eco;moonlight.shadow.mapSize.set(high?2048:1024,high?2048:1024);Object.assign(moonlight.shadow.camera,{left:-65,right:65,top:65,bottom:-65,near:1,far:260});moonlight.shadow.normalBias=.04;moonlight.shadow.bias=-.0001;
const lightMap=await new HDRLoader().loadAsync('./textures/city-light.hdr');const pmrem=new T.PMREMGenerator(renderer);const environment=pmrem.fromEquirectangular(lightMap);scene.environment=environment.texture;scene.environmentIntensity=.34;lightMap.dispose();pmrem.dispose();
const accentLight=new T.PointLight(palettes[palette].a,110,48,1.5);accentLight.position.set(0,7,-18);scene.add(accentLight);
const warmLight=new T.PointLight(palettes[palette].b,90,35,1.5);warmLight.position.set(9,5,-35);scene.add(warmLight);
const box=new T.BoxGeometry(1,1,1),pole=new T.CylinderGeometry(.09,.12,1,8),sphere=new T.SphereGeometry(1,14,10);
const metal=new T.MeshStandardMaterial({color:0x586575,roughness:.64,metalness:.08}),concrete=new T.MeshStandardMaterial({color:0xc5ccd3,roughness:.94}),glass=new T.MeshStandardMaterial({color:0x233e4c,metalness:.22,roughness:.25});
const lit=(color,intensity=3)=>new T.MeshStandardMaterial({color,emissive:color,emissiveIntensity:intensity,roughness:.4});
let cyan=lit(palettes[palette].a,1.35),pink=lit(palettes[palette].b,1.35),white=lit(0xffdfb6,1.5),red=lit(0xc9324a,2);
const mesh=(group,geometry,material,x,y,z,sx=1,sy=1,sz=1)=>{const m=new T.Mesh(geometry,material);m.position.set(x,y,z);m.scale.set(sx,sy,sz);group.add(m);return m;};
const textureLoader=new T.TextureLoader();
const [apartmentTexture,officeTexture,shopTexture,commuterTexture,treeTexture,townhouseTexture]=await Promise.all(['textures/apartment.png','textures/office-color.jpg','textures/shop.png','textures/commuter.png','textures/tree.png','textures/townhouse.png'].map(p=>textureLoader.loadAsync(p)));
for(const texture of [apartmentTexture,officeTexture,shopTexture,townhouseTexture]){texture.colorSpace=T.SRGBColorSpace;texture.wrapS=texture.wrapT=T.RepeatWrapping;texture.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());}
const shopMaterial=new T.MeshBasicMaterial({map:shopTexture});
treeTexture.colorSpace=T.SRGBColorSpace;
const treeMaterial=new T.ShaderMaterial({side:T.DoubleSide,uniforms:{image:{value:treeTexture},time:{value:0}},vertexShader:`uniform float time;varying vec2 texUV;void main(){texUV=uv;vec3 center=(modelMatrix*vec4(position-vec3((uv.x-.5)*5.,(uv.y-.5)*7.,0.),1.)).xyz;vec3 right=vec3(viewMatrix[0][0],viewMatrix[1][0],viewMatrix[2][0]);vec3 w=center+right*((uv.x-.5)*5.+sin(time*.8+center.x*.1)*uv.y*uv.y*.05)+vec3(0.,(uv.y-.5)*7.,0.);gl_Position=projectionMatrix*viewMatrix*vec4(w,1.);}`,fragmentShader:`uniform sampler2D image;varying vec2 texUV;void main(){vec4 c=texture2D(image,texUV);if(c.a<.3)discard;gl_FragColor=vec4(c.rgb,c.a);}`});
const pavingCanvas=document.createElement('canvas');pavingCanvas.width=pavingCanvas.height=512;const paving=pavingCanvas.getContext('2d'),pavingRandom=seed(15,9);paving.fillStyle='#333a41';paving.fillRect(0,0,512,512);for(let y=0;y<512;y+=16)for(let x=-16;x<512;x+=32){const c=63+Math.floor(pavingRandom()*16);paving.fillStyle=`rgb(${c},${c+5},${c+9})`;paving.fillRect(x+(y%32?16:0)+1,y+1,30,14);}const pavingTexture=new T.CanvasTexture(pavingCanvas);pavingTexture.colorSpace=T.SRGBColorSpace;pavingTexture.wrapS=pavingTexture.wrapT=T.RepeatWrapping;pavingTexture.repeat.set(6,6);concrete.map=pavingTexture;

// Moonlit clouds and city glow use the same world-space atmosphere as the facades.
const skyMaterial=new T.ShaderMaterial({side:T.BackSide,depthWrite:false,toneMapped:false,uniforms:{time:{value:0},haze:{value:new T.Color(palettes[palette].fog)}},vertexShader:`varying vec3 direction;void main(){direction=position;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,fragmentShader:`uniform float time;uniform vec3 haze;varying vec3 direction;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(hash(i),hash(i+vec2(1.,0.)),f.x),mix(hash(i+vec2(0.,1.)),hash(i+1.),f.x),f.y);}float fbm(vec2 p){float a=.5,n=0.;for(int i=0;i<5;i++){n+=a*noise(p);p=p*2.03+3.7;a*=.5;}return n;}
void main(){vec3 d=normalize(direction);float h=max(0.,d.y);vec3 c=mix(haze*2.0,vec3(.015,.029,.069),smoothstep(0.,.58,h));vec2 q=d.xz/(h+.22)*2.;float cloud=smoothstep(.42,.76,fbm(q+vec2(time*.003,0.)));float filament=fbm(q*3.);c=mix(c,vec3(.065,.092,.143)*(1.+filament*.8),cloud*.7);float stars=step(.9989,hash(floor(d.xz/(h+.05)*420.)))*smoothstep(.18,.65,h)*(1.-cloud);c+=stars*.27;gl_FragColor=vec4(c,1.);}`});
const sky=mesh(scene,new T.SphereGeometry(700,24,16),skyMaterial,0,0,0);

const architecture=await createArchitecture(scene,renderer,seed,palettes[palette].a,treeMaterial,{apartment:apartmentTexture,shop:shopTexture,townhouse:townhouseTexture});
const canal= createCanal(scene,architecture);
function batchBlock(group){architecture.batch(group);}
function recycle(){const cx=Math.floor(world.x/40),cz=Math.floor(world.z/40),radius=eco?2:3;
if(Math.abs(world.x-origin.x)>800||Math.abs(world.z-origin.z)>800){origin.set(cx*40,0,cz*40);for(const g of chunks.values())g.position.set((g.userData.ix+.5)*40-origin.x,0,(g.userData.iz+.5)*40-origin.z);}
const wanted=new Set();for(let z=cz-radius;z<=cz+radius;z++)for(let x=cx-radius;x<=cx+radius;x++){const key=x+':'+z,detailed=eco?(x<=cx&&x>=cx-1&&z<=cz&&z>=cz-1):Math.abs(x-cx)<=1&&Math.abs(z-cz)<=1;wanted.add(key);const existing=chunks.get(key);if(existing&&existing.userData.detailed!==detailed){scene.remove(existing);architecture.release(existing);chunks.delete(key);}if(!chunks.has(key)){const g=architecture.block(x,z,detailed);g.position.set((x+.5)*40-origin.x,0,(z+.5)*40-origin.z);scene.add(g);chunks.set(key,g);}}
for(const [key,g] of chunks)if(!wanted.has(key)){scene.remove(g);architecture.release(g);chunks.delete(key);}architecture.updateSkyline(cx,cz,origin);}

const road=createRoad(scene,quality==='Quality',eco,palettes[palette].fog);
const reflection=road.reflection;
const moon=mesh(scene,sphere,new T.MeshBasicMaterial({color:0xcbd9ff,toneMapped:false}),-120,135,-350,12,12,12);
const rail=new T.Group();scene.add(rail);
mesh(rail,box,architecture.dark,0,10.1,0,650,.8,5.5);
for(const z of [-2.5,2.5]){mesh(rail,box,metal,0,10.9,z,650,.15,.1);mesh(rail,box,white,0,10.65,z,650,.025,.04);}
for(let x=-300;x<=300;x+=20){for(const z of [-2.45,2.45]){mesh(rail,box,metal,x,11.15,z,.05,.6,.06);mesh(rail,box,metal,x,11.48,z,20,.04,.04);}mesh(rail,box,concrete,x+7,4.8,4.6,1.15,9.6,1.15);mesh(rail,box,metal,x+7,9.5,2.2,2,.5,7.4);}
for(let x=-325;x<325;x+=.65)mesh(rail,box,metal,x,10.7,0,.2,.08,4.5);
batchBlock(rail);
const train=new T.Group();scene.add(train);
const trainMetal=new T.MeshStandardMaterial({color:0xb2bec8,metalness:.78,roughness:.29});

const trainShape=new T.Shape();trainShape.moveTo(-1.3,-1.25);trainShape.lineTo(1.3,-1.25);trainShape.quadraticCurveTo(1.55,-1.25,1.55,-1.0);trainShape.lineTo(1.55,.8);trainShape.quadraticCurveTo(1.5,1.38,1.08,1.45);trainShape.lineTo(-1.08,1.45);trainShape.quadraticCurveTo(-1.5,1.38,-1.55,.8);trainShape.lineTo(-1.55,-1);trainShape.quadraticCurveTo(-1.55,-1.25,-1.3,-1.25);
const carriageGeometry=new T.ExtrudeGeometry(trainShape,{depth:12,bevelEnabled:true,bevelThickness:.12,bevelSize:.08,bevelSegments:3,curveSegments:8});carriageGeometry.translate(0,0,-6);carriageGeometry.rotateY(Math.PI/2);
for(let i=0;i<4;i++){const carriage=new T.Group();train.add(carriage);carriage.position.x=i*12.6;
mesh(carriage,carriageGeometry,trainMetal,0,0,0);
for(const side of [-1,1]){mesh(carriage,box,cyan,0,-.52,side*1.66,11.9,.17,.025);mesh(carriage,box,metal,0,-1,side*1.66,11.9,.025,.035);
// The bevel extends the shell to 1.63 m: keep every window outside it.
for(let x=-4.6;x<5;x+=2.3){mesh(carriage,box,architecture.dark,x,.28,side*1.68,1.72,1.28,.035);const pane=architecture.detailPanel(carriage,1.5,1.1,x,.28,side*1.715,2);if(side<0)pane.rotation.y=Math.PI;mesh(carriage,box,metal,x,.28,side*1.74,.05,1.15,.02);}
for(const x of [-3.45,3.45]){mesh(carriage,box,metal,x,0,side*1.72,.74,2.35,.035);mesh(carriage,box,glass,x,.3,side*1.76,.56,.95,.025);mesh(carriage,box,metal,x,0,side*1.8,.018,2.2,.03);}}
for(const x of [-4,4]){mesh(carriage,box,architecture.dark,x,-1.56,0,1.7,.4,2);for(const z of [-1.1,1.1])mesh(carriage,new T.CylinderGeometry(.32,.32,.18,16),metal,x,-1.5,z).rotation.x=Math.PI/2;}
mesh(carriage,box,metal,0,1.54,0,4,.25,1.5);
if(i<3)mesh(carriage,box,architecture.dark,6.25,0,0,.45,1.8,2.35);}
mesh(train,box,glass,-6.16,.36,0,.055,1.35,2.28);for(const z of [-1.02,1.02])mesh(train,box,white,-6.18,-.55,z,.06,.15,.27);
batchBlock(train);
// Realtime shadows cache the fixed architecture. A moving train must not leave
// its old silhouette in that cache; offline exports refresh shadows each frame.
train.traverse(o=>{if(o.isMesh)o.castShadow=manual;});
const actors=await createActors(scene,eco,params.get('bake')==='1');
const rainCount=eco?400:1800,rainPositions=new Float32Array(rainCount*6);const random=seed(31,88);for(let i=0;i<rainCount;i++){let x=(random()-.5)*90,y=random()*55,z=(random()-.5)*90;rainPositions.set([x,y,z,x+.08,y-.8,z+.03],i*6);}
const rainGeometry=new T.BufferGeometry();rainGeometry.setAttribute('position',new T.BufferAttribute(rainPositions,3));
const rainMaterial=new T.ShaderMaterial({transparent:true,depthWrite:false,uniforms:{time:{value:0}},vertexShader:`uniform float time;varying float opacity;void main(){vec3 p=position;p.y=mod(p.y-time*19.+10000.,55.);opacity=.16;gl_Position=projectionMatrix*modelViewMatrix*vec4(p,1.);}`,fragmentShader:`varying float opacity;void main(){gl_FragColor=vec4(.55,.76,.94,opacity);}`});
const rain=new T.LineSegments(rainGeometry,rainMaterial);rain.frustumCulled=false;scene.add(rain);

const target=new T.WebGLRenderTarget(1,1,{samples:high?2:0,type:T.HalfFloatType}),postScene=new T.Scene(),postCamera=new T.OrthographicCamera(-1,1,1,-1,0,1);
const bloomA=new T.WebGLRenderTarget(1,1,{type:T.HalfFloatType,depthBuffer:false}),bloomB=bloomA.clone();
const quadVertex=`varying vec2 uvp;void main(){uvp=uv;gl_Position=vec4(position.xy,0.,1.);}`;
const bloomScene=new T.Scene(),quadGeometry=new T.PlaneGeometry(2,2);
const extract=new T.ShaderMaterial({depthTest:false,depthWrite:false,toneMapped:false,uniforms:{image:{value:target.texture}},vertexShader:quadVertex,fragmentShader:`uniform sampler2D image;varying vec2 uvp;void main(){vec3 c=texture2D(image,uvp).rgb;float luminance=max(c.r,max(c.g,c.b));gl_FragColor=vec4(c*smoothstep(.7,1.65,luminance),1.);}`});
const blur=new T.ShaderMaterial({depthTest:false,depthWrite:false,toneMapped:false,uniforms:{image:{value:bloomA.texture},direction:{value:new T.Vector2()}},vertexShader:quadVertex,fragmentShader:`uniform sampler2D image;uniform vec2 direction;varying vec2 uvp;void main(){vec3 c=texture2D(image,uvp).rgb*.227027;c+=(texture2D(image,uvp+direction*1.384615).rgb+texture2D(image,uvp-direction*1.384615).rgb)*.316216;c+=(texture2D(image,uvp+direction*3.230769).rgb+texture2D(image,uvp-direction*3.230769).rgb)*.070270;gl_FragColor=vec4(c,1.);}`});
const bloomQuad=new T.Mesh(quadGeometry,extract);bloomScene.add(bloomQuad);
const post=new T.ShaderMaterial({depthTest:false,depthWrite:false,toneMapped:false,uniforms:{image:{value:target.texture},bloom:{value:bloomA.texture},texel:{value:new T.Vector2(1,1)}},vertexShader:quadVertex,fragmentShader:`uniform sampler2D image;uniform sampler2D bloom;uniform vec2 texel;varying vec2 uvp;vec3 film(vec3 v){return clamp((v*(2.51*v+.03))/(v*(2.43*v+.59)+.14),0.,1.);}void main(){vec3 base=texture2D(image,uvp).rgb;vec3 a=texture2D(image,uvp+vec2(texel.x,0.)).rgb,b=texture2D(image,uvp-vec2(texel.x,0.)).rgb,c1=texture2D(image,uvp+vec2(0.,texel.y)).rgb,d=texture2D(image,uvp-vec2(0.,texel.y)).rgb;vec3 lo=min(base,min(min(a,b),min(c1,d))),hi=max(base,max(max(a,b),max(c1,d)));base=clamp(base*1.32-(a+b+c1+d)*.08,lo,hi);vec3 glow=texture2D(bloom,uvp).rgb;vec2 p=uvp*2.-1.;float vignette=1.-dot(p,p)*.075;vec3 c=film((base+glow*.16)*vignette*.85);c=1.055*pow(c,vec3(1./2.4))-.055;gl_FragColor=vec4(max(c,vec3(0.)),1.);}`});if(eco)post.fragmentShader=post.fragmentShader.replace('vec3 glow=texture2D(bloom,uvp).rgb;','vec3 glow=vec3(0.);');postScene.add(new T.Mesh(quadGeometry,post));
function resize(){const w=innerWidth,h=innerHeight;renderer.setPixelRatio(pixelBudget());renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();const size=renderer.getDrawingBufferSize(new T.Vector2());target.setSize(Math.max(1,Math.round(size.x*renderScale)),Math.max(1,Math.round(size.y*renderScale)));bloomA.setSize(Math.max(1,Math.ceil(target.width/4)),Math.max(1,Math.ceil(target.height/4)));bloomB.setSize(bloomA.width,bloomA.height);post.uniforms.texel.value.set(1/target.width,1/target.height);}addEventListener('resize',resize);
function recolor(){scene.background=new T.Color(palettes[palette].fog);scene.fog=new T.FogExp2(palettes[palette].fog,.009);skyMaterial.uniforms.haze.value.setHex(palettes[palette].fog);cyan.color.setHex(palettes[palette].a);cyan.emissive.copy(cyan.color);pink.color.setHex(palettes[palette].b);pink.emissive.copy(pink.color);accentLight.color.setHex(palettes[palette].a);warmLight.color.setHex(palettes[palette].b);architecture.recolor(palettes[palette].fog);document.querySelector('#district').textContent=palettes[palette].name;}
function toast(text){const el=document.querySelector('#toast');el.textContent=text;el.classList.add('visible');clearTimeout(toast.timer);toast.timer=setTimeout(()=>el.classList.remove('visible'),2400);}
function state(){return{x:world.x,y:world.y,z:world.z,yaw,pitch,palette,distance,flying,raining};}
function restore(s){if(!s)return;for(const n of ['x','y','z','yaw','pitch','distance'])if(s[n]!==undefined&&!Number.isFinite(s[n]))return;world.set(s.x??5.55,Math.max(1.65,Math.min(180,s.y??1.92)),s.z??22);yaw=s.yaw??0;pitch=Math.max(-1.25,Math.min(1.25,s.pitch??-.025));distance=s.distance??0;flying=!!s.flying;raining=s.raining!==false;palette=Math.abs(Math.floor(s.palette??palette))%3;recolor();recycle();}
window.city={state,metrics:()=>({frames,chunks:chunks.size,drawCalls:renderCalls,triangles:renderTriangles,resolution:renderer.getDrawingBufferSize(new T.Vector2()).toArray(),memory:renderer.info.memory,adapter:renderer.getContext().getParameter(renderer.getContext().getExtension('WEBGL_debug_renderer_info')?.UNMASKED_RENDERER_WEBGL||renderer.getContext().RENDERER),actors:actors.count,canal:canal.metrics(),architecture:architecture.metrics(),sceneResolution:[target.width,target.height],renderScale:renderScale,captureMode:manual?'offline':'realtime',observedFps:manual?null:1000/frameAverage,walkingPose:actors.inspect(),world:[world.x,world.y,world.z],paused}),move:(x,z)=>{world.x+=x;world.z+=z;recycle();},pause:v=>paused=v,interact:()=>{palette=(palette+1)%3;recolor();toast('Район сменил освещение · '+palettes[palette].name);},restore};
window.city.exportMotions=()=>actors.exportMotions();
window.city.renderAt=(seconds,pose)=>{if(!manual||!Number.isFinite(seconds))return;elapsed=seconds;auto=false;if(pose)restore(pose);frame(0,true);};
window.chrome?.webview?.addEventListener('message',async e=>{if(e.data.type==='pause')paused=!!e.data.value;if(e.data.type==='state')restore(e.data.value);if(manual&&e.data.type==='render'){window.city.renderAt(e.data.time,e.data.pose);await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));send({type:'frame',id:e.data.id});}});
document.querySelector('#enter').onclick=()=>canvas.requestPointerLock();document.querySelector('#close').onclick=()=>send({type:'close',state:state()});
addEventListener('pointerlockchange',()=>{const wasActive=active;active=document.pointerLockElement===canvas;auto=!active;steps.clear();document.body.classList.toggle('playing',active);if(wasActive&&!active&&params.get('explore')==='1')send({type:'close',state:state()});});
addEventListener('mousemove',e=>{if(active){yaw-=e.movementX*.002;pitch=Math.max(-1.25,Math.min(1.25,pitch-e.movementY*.002));}});
addEventListener('keydown',e=>{if(!active)return;steps.add(e.code);if(['KeyW','KeyA','KeyS','KeyD','Space'].includes(e.code))e.preventDefault();if(e.repeat)return;if(e.code==='KeyF'){flying=!flying;toast(flying?'Полёт · Space вверх / Ctrl вниз':'Прогулка по улицам');}if(e.code==='KeyR'){raining=!raining;toast(raining?'Дождь включён':'Ясная ночь');}if(e.code==='KeyE'){if(Math.abs(world.z-(Math.round((world.z+40)/160)*160-40))<12){world.z-=320;toast('Экспресс доставил тебя в следующий квартал');}else window.city.interact();}if(e.code==='KeyHome'){restore({x:5.55,y:1.92,z:22,palette});toast('Вернулся к началу прогулки');}});
addEventListener('keyup',e=>steps.delete(e.code));addEventListener('blur',()=>{steps.clear();if(document.pointerLockElement)document.exitPointerLock();});
document.body.classList.toggle('wallpaper',!preview);recolor();restore(window.initialCityState);
// Start Auto within the budget of integrated graphics; scale up after measured frames.
let frameAverage=1000/fps,scaleTimer=0,renderScale=adaptive?.65:1;
let ready=false,shadowCell='';
const reflectionProjection=new T.Matrix4();let reflectionTime=-Infinity;
if(reflection){const reflect=reflection.onBeforeRender.bind(reflection);reflection.onBeforeRender=(...args)=>{if(manual||elapsed-reflectionTime>=1/20||frames<4){reflect(...args);reflectionProjection.copy(reflection.material.uniforms.textureMatrix.value).multiply(reflection.matrixWorld.clone().invert());reflectionTime=elapsed;}else reflection.material.uniforms.textureMatrix.value.copy(reflectionProjection).multiply(reflection.matrixWorld);};}

function frame(now,forced=false){if(!manual&&!forced)requestAnimationFrame(frame);if(!forced&&paused){lastTime=lastRender=now;return;}if(!forced&&now-lastRender<1000/fps-.5)return;const dt=forced?0:Math.min(.08,(now-(lastTime||now))/1000);const interval=now-lastRender;lastTime=now;lastRender=now;elapsed+=dt;
if(!manual&&!forced&&frames>10&&interval>0){frameAverage=frameAverage*.95+interval*.05;if(adaptive&&now-scaleTimer>2000){scaleTimer=now;const budget=1000/fps;let next=renderScale;if(frameAverage>budget*1.22)next=Math.max(.45,renderScale-.08);else if(frameAverage<budget*1.06)next=Math.min(1,renderScale+.04);if(Math.abs(next-renderScale)>.01){renderScale=next;resize();}}}
const old=world.clone();if(active){const speed=(steps.has('ShiftLeft')||steps.has('ShiftRight')?22:7)*dt,forward=(steps.has('KeyW')?1:0)-(steps.has('KeyS')?1:0),side=(steps.has('KeyD')?1:0)-(steps.has('KeyA')?1:0);world.x+=(-Math.sin(yaw)*forward+Math.cos(yaw)*side)*speed;world.z+=(-Math.cos(yaw)*forward-Math.sin(yaw)*side)*speed;
if(!flying){const qx=Math.abs(((world.x%40)+60)%40-20),qz=Math.abs(((world.z%40)+60)%40-20);const river=Math.abs(((world.x+80)%160+160)%160-80);if((qx>6.2&&qz>6.2)||(river<4.6&&qz>4.12))world.copy(old);world.y+=((qz<4.12?1.72:1.92)-world.y)*Math.min(1,dt*4);}else{world.y+=((steps.has('Space')?1:0)-(steps.has('ControlLeft')?1:0))*speed;world.y=Math.max(1.72,Math.min(160,world.y));}}
else if(auto){world.z-=dt*.95;world.x+=(Math.round(world.x/40)*40+5.55+Math.sin(elapsed*.04)*.2-world.x)*Math.min(1,dt*.1);world.y+=(1.92-world.y)*Math.min(1,dt*.6);yaw+=(-yaw)*Math.min(1,dt*.3);pitch+=(-.025-pitch)*Math.min(1,dt*.3);}distance+=world.distanceTo(old);recycle();camera.position.copy(world).sub(origin);camera.rotation.order='YXZ';camera.rotation.set(pitch,yaw,0);
road.update(elapsed,camera,origin,palettes[palette].fog);canal.update(elapsed,world,origin);sky.position.copy(camera.position);skyMaterial.uniforms.time.value=elapsed;moon.position.x=camera.position.x-120;moon.position.z=camera.position.z-350;
rail.position.set(Math.round(world.x/40)*40-origin.x,0,Math.floor((world.z+40)/160)*160-40-origin.z);train.position.set(rail.position.x+((elapsed*11+200)%430)-215,12.35,rail.position.z);
accentLight.position.set(camera.position.x-4,7,camera.position.z-18);warmLight.position.set(camera.position.x+8,5,camera.position.z-32);
actors.update(elapsed,world,origin);
const shadowKey=Math.round(world.x/40)+':'+Math.round(world.z/40)+':'+origin.x+':'+origin.z;if(manual||shadowKey!==shadowCell){const sx=Math.round(world.x/40)*40-origin.x,sz=Math.round(world.z/40)*40-origin.z;moonlight.position.set(sx-40,120,sz-65);moonlight.target.position.set(sx,0,sz);renderer.shadowMap.needsUpdate=true;shadowCell=shadowKey;}
rain.visible=raining;rain.position.set(camera.position.x,0,camera.position.z);rainMaterial.uniforms.time.value=elapsed;treeMaterial.uniforms.time.value=elapsed;
renderer.info.reset();renderer.setRenderTarget(target);renderer.render(scene,camera);renderCalls=renderer.info.render.calls;renderTriangles=renderer.info.render.triangles;if(!eco){bloomQuad.material=extract;renderer.setRenderTarget(bloomA);renderer.render(bloomScene,postCamera);
bloomQuad.material=blur;blur.uniforms.image.value=bloomA.texture;blur.uniforms.direction.value.set(1/bloomA.width,0);renderer.setRenderTarget(bloomB);renderer.render(bloomScene,postCamera);
blur.uniforms.image.value=bloomB.texture;blur.uniforms.direction.value.set(0,1/bloomA.height);renderer.setRenderTarget(bloomA);renderer.render(bloomScene,postCamera);
}
renderer.setRenderTarget(null);renderer.render(postScene,postCamera);frames++;
if(!ready&&frames>3){ready=true;send({type:'ready'});}if(now-lastStats>1000){document.querySelector('#stats').textContent=Math.round(distance)+' m · '+quality.toUpperCase()+' · '+Math.round(1000/frameAverage)+' FPS';lastStats=now;}}
addEventListener('error',e=>{send({type:'error',message:e.message});document.querySelector('#error').hidden=false;document.querySelector('#error').textContent='Не удалось запустить город.\n'+e.message;});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();send({type:'contextlost'});});
if(!manual)setInterval(()=>send({type:'state',value:state()}),3000);
resize();recycle();
if(manual){for(let i=0;i<4;i++)frame(0,true);}else requestAnimationFrame(frame);

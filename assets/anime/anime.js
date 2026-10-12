import * as T from 'three';
const params=new URLSearchParams(location.search),id=Number(params.get('scene')),manual=params.get('manual')==='1';
const entries=await fetch('catalog.json').then(r=>r.json()),entry=entries.find(e=>e.id===id);
if(!entry)throw new Error('Unknown artwork '+id);
const send=value=>window.chrome?.webview?.postMessage(value);
const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true,preserveDrawingBuffer:true});
renderer.setPixelRatio(1);renderer.outputColorSpace=T.SRGBColorSpace;
const image=await new T.TextureLoader().loadAsync(id+'.png');image.colorSpace=T.SRGBColorSpace;image.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());
const kinds=['rain','petals','stars','window-rain','wind','fireflies','sea','embers','snow-window','dust','underwater'];
const waterBounds={1101:[0,.77],1102:[.43,1],1104:[0,1],1106:[.32,.95],1108:[0,.66],1115:[.22,.91]};
const bounds=waterBounds[id]||[0,1];
const uniforms={image:{value:image},phase:{value:0},kind:{value:kinds.indexOf(entry.effect)},water:{value:entry.water},sky:{value:entry.sky},waterBounds:{value:new T.Vector2(...bounds)},accent:{value:new T.Color(entry.accent)}};
const material=new T.ShaderMaterial({uniforms,vertexShader:'varying vec2 uvp;void main(){uvp=uv;gl_Position=vec4(position.xy,0.,1.);}',fragmentShader:`
uniform sampler2D image;uniform float phase,water,sky;uniform int kind;uniform vec2 waterBounds;uniform vec3 accent;varying vec2 uvp;
float hash(float n){return fract(sin(n*127.13)*43758.5453);}
void main(){
 vec2 p=vec2(uvp.x,1.-uvp.y),q=uvp;
 float wet=smoothstep(water,water+.055,p.y)*smoothstep(waterBounds.x,waterBounds.x+.04,p.x)*(1.-smoothstep(waterBounds.y-.04,waterBounds.y,p.x));
 float skyMask=(1.-smoothstep(max(0.,sky-.09),sky,p.y))*step(.01,sky);
 q.x+=wet*(sin(p.y*190.+phase*3.)*.0014+sin(p.y*79.+p.x*31.-phase*2.)*.0008);
 q.y+=wet*sin(p.x*52.+p.y*40.+phase*2.)*.0006;
 q.x+=skyMask*sin(p.y*12.+phase)*.002;
 vec3 col=texture2D(image,clamp(q,vec2(.001),vec2(.999))).rgb;
 col+=wet*(sin(p.y*173.+phase*3.+p.x*25.)*.012+sin(p.y*66.-phase*2.)*.008);
 float windowMask=(1.-smoothstep(.51,.57,p.x))*(1.-smoothstep(.65,.73,p.y));
 if(kind==10){
  float rays=pow(.5+.5*sin(p.x*29.+p.y*12.+phase*2.),5.);
  float caustic=pow(.5+.5*sin(p.x*63.+p.y*37.+phase*3.)*sin(p.x*29.-p.y*41.-phase*2.),7.);
  col+=vec3(.12,.21,.23)*(rays*.24+caustic*.36)*(windowMask+smoothstep(.70,.92,p.y));
 }
 if(kind==3||kind==8||kind==9||kind==2){
  float beam=pow(.5+.5*sin(p.x*22.+p.y*9.+phase),4.)*(1.-p.y*.65);
  col+=accent*.09*beam*(kind==3||kind==8?windowMask:1.)*smoothstep(.12,.7,dot(col,vec3(.3,.5,.2)));
 }
 if(kind==0||kind==3){
  vec2 grid=vec2(p.x*145.+p.y*8.,p.y*32.+phase/6.28318530718*64.);
  vec2 cell=floor(grid),f=fract(grid);
  float rain=step(.62,hash(cell.x))*exp(-pow((f.x-.5)*90.,2.))*smoothstep(.04,.16,f.y)*(1.-smoothstep(.58,.82,f.y));
  col+=vec3(.14,.18,.22)*rain*(kind==3?windowMask:1.);
 }
 for(int i=0;i<128;i++){
  if(kind==8)break;
  if(i>=48&&kind!=8)break;
  float n=float(i),speed=(kind==7||kind==10)?-1.:1.;
  vec2 center=fract(vec2(hash(n+2.)+sin(phase+n)*.023,hash(n+83.)+phase/6.28318530718*speed));
  if(kind==5)center=vec2(hash(n+2.)+sin(phase+n)*.025,hash(n+83.)+cos(phase+n)*.03);
  vec2 d=(p-center)*vec2(1.7777778,1.);
  float fade=smoothstep(0.,.045,center.y)*(1.-smoothstep(.955,1.,center.y));
  if(kind==1){float a=phase*2.+n;d=mat2(cos(a),-sin(a),sin(a),cos(a))*d;float petal=exp(-dot(d*vec2(1.,2.3),d*vec2(1.,2.3))*150000.);col=mix(col,vec3(.95,.69,.82),petal*.65*fade);}
  else if(kind==5||kind==7||kind==8||kind==9||kind==10||kind==2){
   float dotLight=exp(-dot(d,d)*(kind==8?22000.:kind==10?95000.:85000.));
   vec3 colour=kind==7?vec3(1.,.41,.12):kind==8?vec3(.85,.93,1.):kind==5?vec3(.69,1.,.4):accent;
   float brightness=kind==8?.5+.3*sin(phase*2.+n):.25+.25*sin(phase*2.+n);
   col+=colour*dotLight*brightness*fade*(kind==8?windowMask:1.);
  }
 }
 gl_FragColor=vec4(max(col,0.),1.);
 #include <colorspace_fragment>
}`});
const scene=new T.Scene(),camera=new T.OrthographicCamera(-1,1,1,-1,.01,10);camera.position.z=2;
scene.add(new T.Mesh(new T.PlaneGeometry(2,2),material));
// Snow is drawn only in its small particle footprints, instead of running 128
// particle calculations for every pixel of the 4K background.
let snowMaterial;
if(entry.effect==='snow-window'){
 const geometry=new T.BufferGeometry();
 geometry.setAttribute('position',new T.BufferAttribute(new Float32Array(128*3),3));
 geometry.setAttribute('particle',new T.BufferAttribute(Float32Array.from({length:128},(_,n)=>n),1));
 snowMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,toneMapped:false,
  uniforms:{phase:uniforms.phase,viewport:{value:new T.Vector2(innerWidth,innerHeight)}},
  vertexShader:`attribute float particle;uniform float phase;uniform vec2 viewport;varying float glow;
   float hash(float n){return fract(sin(n*127.13)*43758.5453);}
   void main(){float n=particle;vec2 center=fract(vec2(hash(n+2.)+sin(phase+n)*.023,hash(n+83.)+phase/6.28318530718));
    float fade=smoothstep(0.,.045,center.y)*(1.-smoothstep(.955,1.,center.y));glow=(.5+.3*sin(phase*2.+n))*fade;
    gl_Position=vec4(center.x*2.-1.,1.-center.y*2.,0.,1.);gl_PointSize=viewport.y*7./sqrt(22000.);
   }`,
  fragmentShader:`uniform vec2 viewport;varying float glow;
   void main(){vec2 p=vec2(gl_FragCoord.x/viewport.x,1.-gl_FragCoord.y/viewport.y);
    float windowMask=(1.-smoothstep(.51,.57,p.x))*(1.-smoothstep(.65,.73,p.y));
    vec2 d=(gl_PointCoord-.5)*7./sqrt(22000.);
    float alpha=exp(-dot(d,d)*22000.)*glow*windowMask;
    if(alpha<.002)discard;gl_FragColor=vec4(.85,.93,1.,alpha);
   }`});
 const flakes=new T.Points(geometry,snowMaterial);flakes.frustumCulled=false;flakes.renderOrder=1;scene.add(flakes);
}
let time=0,frames=0,paused=false;
function resize(){renderer.setSize(innerWidth,innerHeight,false);snowMaterial?.uniforms.viewport.value.set(innerWidth,innerHeight);}addEventListener('resize',resize);resize();
function renderAt(seconds){time=((seconds%12)+12)%12;uniforms.phase.value=time/12*Math.PI*2;renderer.render(scene,camera);frames++;}
window.living={renderAt,metrics:()=>({frames,time,resolution:renderer.getDrawingBufferSize(new T.Vector2()).toArray(),kind:entry.effect}),pause:value=>paused=value};
window.chrome?.webview?.addEventListener('message',async e=>{if(e.data.type==='pause')paused=!!e.data.value;if(e.data.type==='render'){renderAt(e.data.time);await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));send({type:'frame',id:e.data.id});}});
renderAt(0);send({type:'ready'});
const start=performance.now();function animate(now){if(!paused)renderAt((now-start)/1000);requestAnimationFrame(animate);}if(!manual)requestAnimationFrame(animate);

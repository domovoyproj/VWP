import * as T from 'three';

const params=new URLSearchParams(location.search),id=Number(params.get('scene')),manual=params.get('manual')==='1';
const catalog=await fetch('catalog.json').then(r=>r.json()),entry=catalog.find(e=>e.id===id);
if(!entry)throw new Error('Unknown vibe '+id);
const send=value=>window.chrome?.webview?.postMessage(value);
const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true,preserveDrawingBuffer:true});
renderer.setPixelRatio(1);renderer.outputColorSpace=T.SRGBColorSpace;
const image=await new T.TextureLoader().loadAsync(entry.background||id+'.png');
image.colorSpace=T.SRGBColorSpace;image.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());
const effects=['rain','petals','stars','vinyl','wind','fireflies','sea','dust','mist','orbit','aurora','prism','liquid','embers'];
const kind=effects.indexOf(entry.effect);
const phase={value:0},viewport={value:new T.Vector2(innerWidth,innerHeight)};
const waterRect=entry.waterRect||[0,entry.water,1,1];
const uniforms={image:{value:image},phase,kind:{value:kind},sky:{value:entry.sky},waterRect:{value:new T.Vector4(...waterRect)},accent:{value:new T.Color(entry.accent)},disc:{value:new T.Vector4(...(entry.disc||[.695,.696,.155,.046]))}};
const backgroundMaterial=new T.ShaderMaterial({depthTest:false,depthWrite:false,uniforms,
 vertexShader:'varying vec2 uvp;void main(){uvp=uv;gl_Position=vec4(position.xy,0.,1.);}',
 fragmentShader:`
uniform sampler2D image;uniform float phase,sky;uniform int kind;uniform vec4 waterRect,disc;uniform vec3 accent;varying vec2 uvp;
float hash(float n){return fract(sin(n*127.13)*43758.5453);}
float maskRect(vec2 p,vec4 r){return smoothstep(r.x,r.x+.018,p.x)*(1.-smoothstep(r.z-.018,r.z,p.x))*smoothstep(r.y,r.y+.03,p.y)*(1.-smoothstep(r.w-.02,r.w,p.y));}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(hash(i.x+i.y*63.),hash(i.x+1.+i.y*63.),f.x),mix(hash(i.x+(i.y+1.)*63.),hash(i.x+1.+(i.y+1.)*63.),f.x),f.y);}
void main(){
 vec2 p=vec2(uvp.x,1.-uvp.y),q=uvp;
 float wet=maskRect(p,waterRect),skyMask=(1.-smoothstep(max(0.,sky-.08),sky,p.y))*step(.01,sky);
 q.x+=wet*(sin(p.y*195.+phase*3.)*.0026+sin(p.y*83.+p.x*38.-phase*2.)*.0013);
 q.y+=wet*sin(p.x*69.+p.y*50.+phase*2.)*.0011;
 q.x+=skyMask*(sin(p.y*15.+phase)*.006+sin(p.x*8.-phase)*.0018);
 if(kind==7&&p.y>.43){q.x+=sin(p.y*105.+phase*2.)*.0019*smoothstep(.43,.80,p.y);}
 if(kind==12){q.x+=sin(p.y*13.+phase)*.011+sin(p.x*9.-phase*2.)*.004;q.y+=sin(p.x*15.-phase)*.007;}
 vec3 col=texture2D(image,clamp(q,vec2(.001),vec2(.999))).rgb;
 col+=wet*(sin(p.y*160.+p.x*29.+phase*3.)*.020+sin(p.y*73.-phase*2.)*.012);
 if(kind==0||kind==3){
  float rainMask=kind==3?maskRect(p,vec4(.045,.02,.495,.58)):1.;
  vec2 grid=vec2(p.x*176.+p.y*12.,p.y*38.+phase/6.28318530718*114.);
  vec2 cell=floor(grid),f=fract(grid);
  float rain=step(.59,hash(cell.x))*exp(-pow((f.x-.5)*65.,2.))*smoothstep(.02,.14,f.y)*(1.-smoothstep(.66,.88,f.y));
  col+=vec3(.21,.27,.34)*rain*rainMask;
 }
 if(kind==3){
  vec2 d=(p-disc.xy)/disc.zw;float r=length(d),angle=phase*3.;
  vec2 turned=mat2(cos(angle),-sin(angle),sin(angle),cos(angle))*d;
  vec2 samplep=disc.xy+turned*disc.zw;
  float label=(1.-smoothstep(.34,.39,r));
  vec3 rotated=texture2D(image,vec2(samplep.x,1.-samplep.y)).rgb;
  float stripe=smoothstep(.12,.18,turned.x)*(1.-smoothstep(.22,.28,turned.x))*smoothstep(-.18,-.12,turned.y)*(1.-smoothstep(.25,.29,turned.y));
  rotated=mix(rotated,vec3(.07,.035,.024),stripe*.85);col=mix(col,rotated,label*.93);
 }
 if(kind==7||kind==8){
  vec2 flow=vec2(p.x*7.+cos(phase)*.8,p.y*6.+sin(phase)*.6);
  float fog=noise(flow)*.65+noise(flow*2.+3.)*.35;
  float ribbon=exp(-pow((p.y-(.61+sin(p.x*5.+phase)*.08))*8.,2.));
  vec3 tint=kind==8?vec3(.28,.36,.32):vec3(.40,.28,.14);
  col=mix(col,tint,smoothstep(.38,.82,fog)*ribbon*(kind==8?.28:.18));
 }
 if(kind==10){
  float curtain=0.;
  for(int band=0;band<3;band++){float n=float(band);float center=.21+n*.095+sin(p.x*7.+phase+n)*.043+sin(p.x*17.-phase*2.+n)*.011;float width=.027+n*.008;curtain+=exp(-pow((p.y-center)/width,2.))*(.42+.58*pow(.5+.5*sin(p.x*78.+phase*3.+n),2.));}
  col+=mix(vec3(.018,.32,.20),vec3(.16,.08,.33),.5+.5*sin(p.x*5.+phase))*curtain*skyMask;
 }
 if(kind==11){float rays=pow(.5+.5*sin(p.x*43.+p.y*19.+phase*2.)*sin(p.x*17.-p.y*27.-phase),6.);col+=vec3(.14,.18,.23)*rays*(wet+.20);}
 if(kind==2||kind==7||kind==13){float beam=pow(.5+.5*sin(p.x*16.+p.y*6.+phase),5.);col+=accent*beam*.065*smoothstep(.09,.60,dot(col,vec3(.3,.5,.2)));}
 if(kind==13){
  vec2 lamp=(p-vec2(.82,.66))*vec2(4.,7.),sconce=(p-vec2(.18,.40))*vec2(5.,5.);
  float glow=exp(-dot(lamp,lamp))+exp(-dot(sconce,sconce))*.45;
  float flicker=.5+.5*sin(phase*3.+p.y*2.);col+=vec3(.29,.11,.035)*glow*flicker;
  float moonbeam=exp(-pow((p.x-(.78-p.y*.20))*8.,2.))*smoothstep(.35,.85,p.y);
  col+=vec3(.09,.13,.19)*moonbeam*(.5+.5*sin(phase));
 }
 gl_FragColor=vec4(max(col,0.),1.);
 #include <colorspace_fragment>
}`});
const scene=new T.Scene(),camera=new T.OrthographicCamera(-1,1,1,-1,.01,20);camera.position.z=5;
const background=new T.Mesh(new T.PlaneGeometry(2,2),backgroundMaterial);background.renderOrder=-2;scene.add(background);

// Particle sprites cost only their footprints, rather than 48 calculations at every 4K pixel.
let particleMaterial,particles;
if([1,2,5,7,8,13].includes(kind)){
 const count=kind==1?72:kind==8?60:84,geometry=new T.BufferGeometry();
 geometry.setAttribute('position',new T.BufferAttribute(new Float32Array(count*3),3));
 geometry.setAttribute('particle',new T.BufferAttribute(Float32Array.from({length:count},(_,n)=>n),1));
 particleMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,toneMapped:false,uniforms:{phase,viewport,kind:{value:kind},accent:uniforms.accent},
  vertexShader:`attribute float particle;uniform float phase;uniform vec2 viewport;uniform int kind;varying float glow,spin;float hash(float n){return fract(sin(n*127.13)*43758.5453);}void main(){float n=particle;vec2 p=vec2(hash(n+4.),hash(n+31.));if(kind==5||kind==2){p+=vec2(sin(phase+n)*.025,cos(phase+n)*.021);}else{float speed=kind==13?-1.:1.;p=fract(p+vec2(sin(phase+n)*.031,phase/6.28318530718*speed));}float fade=smoothstep(0.,.055,p.y)*(1.-smoothstep(.945,1.,p.y));glow=fade*(.40+.40*sin(phase*2.+n));spin=phase*2.+n;gl_Position=vec4(p.x*2.-1.,1.-p.y*2.,0.,1.);gl_PointSize=viewport.y*(kind==1?.015:kind==8?.009:.011)*(0.65+hash(n)*.55);}`,
  fragmentShader:`uniform int kind;uniform vec3 accent;varying float glow,spin;void main(){vec2 d=gl_PointCoord-.5;float a;if(kind==1){d=mat2(cos(spin),-sin(spin),sin(spin),cos(spin))*d;a=exp(-dot(d*vec2(2.3,5.),d*vec2(2.3,5.)));}else a=exp(-dot(d,d)*24.);a*=glow;if(a<.002)discard;vec3 c=kind==1?vec3(.98,.65,.79):kind==5?vec3(.59,.88,.39):kind==13?vec3(1.,.43,.18):accent;gl_FragColor=vec4(c,a);}`});
 particles=new T.Points(geometry,particleMaterial);particles.frustumCulled=false;particles.renderOrder=2;scene.add(particles);
}

// A real rotating sphere, with illumination fixed in world space, sits outside the station window.
let planet;
if(kind==9){
 const settings=entry.planet||{x:.69,y:.28,radius:.155};
 const planetMaterial=new T.ShaderMaterial({uniforms:{phase},vertexShader:`varying vec3 normalp,point;varying vec2 u;void main(){u=uv;point=position;normalp=normalize(normalMatrix*normal);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
  fragmentShader:`varying vec3 normalp,point;varying vec2 u;uniform float phase;float h(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(h(i),h(i+vec2(1.,0.)),f.x),mix(h(i+vec2(0.,1.)),h(i+1.),f.x),f.y);}void main(){vec2 q=vec2(fract(u.x+phase/6.28318530718),u.y);float swirl=noise(q*vec2(19.,28.))*.65+noise(q*vec2(41.,61.))*.35;float bands=.5+.5*sin(q.y*103.+swirl*7.);vec3 base=mix(vec3(.045,.115,.20),vec3(.28,.50,.56),bands);base=mix(base,vec3(.73,.66,.49),smoothstep(.67,.86,swirl)*.63);vec3 normal=normalize(normalp);float light=max(0.,dot(normal,normalize(vec3(-.7,.45,1.))));float rim=pow(1.-max(0.,normal.z),3.);vec3 c=base*(.045+light*.98)+vec3(.08,.19,.31)*rim;gl_FragColor=vec4(c,1.);#include <colorspace_fragment>}`.replace(';#include',';\n#include')});
 planet=new T.Mesh(new T.SphereGeometry(settings.radius*2,96,64),planetMaterial);planet.position.set(settings.x*2.-1.,1.-settings.y*2.,.1);planet.scale.x=innerHeight/innerWidth;planet.userData.radius=settings.radius;planet.renderOrder=0;scene.add(planet);
}

// The character is an independent transparent layer. Its plate, scenery and props stay still.
let character,characterMaterial;
if(entry.character){
 const spec=entry.character,texture=await new T.TextureLoader().loadAsync('characters/'+spec.file);texture.colorSpace=T.SRGBColorSpace;
 const size=texture.image,box=spec.box||[0,0,1,1],aspect=(size.width*(box[2]-box[0]))/(size.height*(box[3]-box[1]));
 characterMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,uniforms:{image:{value:texture},phase,clip:{value:new T.Vector4(...box)},eyes:{value:new T.Vector4(...(spec.eyes||[.48,.12,.57,.12]))},eyeRadius:{value:new T.Vector2(...(spec.eyeRadius||[.026,.012]))}},
  vertexShader:`varying vec2 uvp;uniform float phase;void main(){uvp=uv;vec3 p=position;float y=uv.y;float breath=sin(phase*2.);p.x+=sin(phase)*.0035*y*y;p.y+=breath*.0025*smoothstep(.35,.75,y);gl_Position=projectionMatrix*modelViewMatrix*vec4(p,1.);}`,
  fragmentShader:`uniform sampler2D image;uniform float phase;uniform vec4 clip,eyes;uniform vec2 eyeRadius;varying vec2 uvp;float mask(vec2 p,vec2 c,vec2 r){return 1.-smoothstep(.75,1.,length((p-c)/r));}void main(){vec2 p=vec2(uvp.x,1.-uvp.y),q=p;float hair=1.-smoothstep(.12,.34,p.y);q.x+=sin(phase*2.+p.y*17.)*.0035*hair;float chest=mask(p,vec2(.51,.35),vec2(.26,.19));q.x+=(p.x-.51)*sin(phase*2.)*.007*chest;float hand=mask(p,vec2(.47,.48),vec2(.22,.14));q.y+=sin(phase)*.004*hand;float blink=pow(max(0.,cos(phase*2.-.8)),48.);for(int n=0;n<2;n++){vec2 c=n==0?eyes.xy:eyes.zw;float m=mask(p,c,eyeRadius);float folded=c.y+(p.y-c.y)*(.9+blink*6.);q.y=mix(q.y,folded,m);}vec2 tex=vec2(mix(clip.x,clip.z,q.x),1.-mix(clip.y,clip.w,q.y));vec4 col=texture2D(image,clamp(tex,vec2(0.),vec2(1.)));if(col.a<.002)discard;gl_FragColor=col;#include <colorspace_fragment>}`.replace(';#include',';\n#include')});
 const height=spec.height*2,width=height*aspect*innerHeight/innerWidth;
 character=new T.Mesh(new T.PlaneGeometry(width,height,16,24),characterMaterial);character.position.set(spec.x*2.-1.,1.-spec.y*2.,.3);character.userData={aspect,height,x:spec.x,y:spec.y};character.renderOrder=1;scene.add(character);
}

let frames=0,time=0,paused=false,characterOnly=false;
function resize(){renderer.setSize(innerWidth,innerHeight,false);viewport.value.set(innerWidth,innerHeight);if(planet)planet.scale.x=innerHeight/innerWidth;if(character){character.scale.x=(character.userData.height*character.userData.aspect*innerHeight/innerWidth)/character.geometry.parameters.width;}}
addEventListener('resize',resize);resize();
function renderAt(seconds){time=((seconds%12)+12)%12;phase.value=time/12*Math.PI*2;background.visible=!characterOnly;if(particles)particles.visible=!characterOnly;if(planet)planet.visible=!characterOnly;renderer.render(scene,camera);frames++;}
window.living={renderAt,metrics:()=>({frames,time,resolution:renderer.getDrawingBufferSize(new T.Vector2()).toArray(),kind:entry.effect,particles:particles?.geometry.attributes.particle.count||0,character:!!character,planet:!!planet}),pause:value=>paused=value};
if(manual)window.living.characterOnly=value=>{characterOnly=!!value;renderer.setClearColor(0x090c16,1);};
window.chrome?.webview?.addEventListener('message',async e=>{if(e.data.type==='pause')paused=!!e.data.value;if(e.data.type==='render'){renderAt(e.data.time);if(manual)renderer.getContext().finish();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));send({type:'frame',id:e.data.id});}});
renderAt(0);send({type:'ready'});
const start=performance.now();function animate(now){if(!paused)renderAt((now-start)/1000);requestAnimationFrame(animate);}if(!manual)requestAnimationFrame(animate);

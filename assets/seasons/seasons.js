import * as T from 'three';

const params=new URLSearchParams(location.search),id=Number(params.get('scene')),manual=params.get('manual')==='1';
const catalog=await fetch('catalog.json').then(r=>r.json()),entry=catalog.find(e=>e.id===id);
if(!entry)throw new Error('Unknown seasonal wallpaper '+id);
const send=value=>window.chrome?.webview?.postMessage(value);
const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true,preserveDrawingBuffer:true});
renderer.setPixelRatio(1);renderer.outputColorSpace=T.SRGBColorSpace;
const image=await new T.TextureLoader().loadAsync(entry.background||id+'.png');image.colorSpace=T.SRGBColorSpace;
image.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());
const phase={value:0},viewport={value:new T.Vector2(innerWidth,innerHeight)};
const panes=(entry.panes||[entry.weatherRect,entry.weatherRect,entry.weatherRect]).map(r=>new T.Vector4(...r));
const uniforms={image:{value:image},phase,water:{value:new T.Vector4(...entry.waterRect)},waterEdges:{value:new T.Vector4(...(entry.waterEdges||[entry.waterRect[0],entry.waterRect[0],entry.waterRect[2],entry.waterRect[2]]))},panes:{value:panes},weatherRect:{value:new T.Vector4(...entry.weatherRect)},steam:{value:new T.Vector4(...entry.steam)},glow:{value:new T.Vector2(...entry.glow)},sky:{value:entry.sky},rain:{value:entry.weather==='rain'?1:0}};
const backgroundMaterial=new T.ShaderMaterial({depthTest:false,depthWrite:false,uniforms,
 vertexShader:'varying vec2 uvp;void main(){uvp=uv;gl_Position=vec4(position.xy,0.,1.);}',
 fragmentShader:`
uniform sampler2D image;uniform float phase,sky,rain;uniform vec4 water,waterEdges,weatherRect,steam,panes[3];uniform vec2 glow;varying vec2 uvp;
float h(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(h(i),h(i+vec2(1.,0.)),f.x),mix(h(i+vec2(0.,1.)),h(i+1.),f.x),f.y);}
float rect(vec2 p,vec4 r){return smoothstep(r.x,r.x+.012,p.x)*(1.-smoothstep(r.z-.012,r.z,p.x))*smoothstep(r.y,r.y+.015,p.y)*(1.-smoothstep(r.w-.015,r.w,p.y));}
void main(){
 vec2 p=vec2(uvp.x,1.-uvp.y),q=uvp;float t=clamp((p.y-water.y)/max(.001,water.w-water.y),0.,1.);vec4 bounds=vec4(mix(waterEdges.x,waterEdges.y,t),water.y,mix(waterEdges.z,waterEdges.w,t),water.w);float wet=rect(p,bounds);
 q.x+=wet*(sin(p.y*175.+phase*3.)*.0016+sin(p.y*83.+p.x*43.-phase*2.)*.0009);
 q.y+=wet*sin(p.x*68.+p.y*37.+phase*2.)*.0008;
 float skyMask=(1.-smoothstep(max(0.,sky-.07),sky,p.y))*step(.01,sky);
 q.x+=skyMask*sin(p.y*18.+phase)*.0035;
 vec3 col=texture2D(image,clamp(q,vec2(.001),vec2(.999))).rgb;
 col+=wet*(sin(p.y*170.+p.x*21.+phase*3.)*.016+sin(p.y*93.-phase*2.)*.010);
 if(steam.z>.001){
  vec2 s=(p-steam.xy)/steam.zw;float plume=exp(-s.x*s.x*3.)*(1.-smoothstep(-.08,.25,s.y))*smoothstep(-1.,-.75,s.y);
  vec2 drift=vec2(cos(phase)*.6,sin(phase)*.9);float wisps=noise(s*vec2(3.,5.)+drift)*.65+noise(s*vec2(7.,11.)-drift)*.35;
  col=mix(col,vec3(.69,.74,.77),plume*smoothstep(.32,.75,wisps)*.30);
 }
 vec2 d=(p-glow)*vec2(7.,7.);float warmth=exp(-dot(d,d))*(.45+.18*sin(phase*2.)+.05*sin(phase*5.));
 col+=vec3(.06,.025,.009)*warmth;
 if(rain>.5){
  vec2 grid=vec2(p.x*165.+p.y*17.,p.y*31.+phase/6.28318530718*93.);vec2 cell=floor(grid),f=fract(grid);
  float drop=step(.45,h(vec2(cell.x,4.)))*exp(-pow((f.x-.5)*46.,2.))*smoothstep(.04,.16,f.y)*(1.-smoothstep(.63,.91,f.y));
  float glass=max(rect(p,panes[0]),max(rect(p,panes[1]),rect(p,panes[2])));col+=vec3(.16,.20,.25)*drop*glass;
 }
 gl_FragColor=vec4(max(col,0.),1.);
 #include <colorspace_fragment>
}`});
const scene=new T.Scene(),camera=new T.OrthographicCamera(-1,1,1,-1,.01,20);camera.position.z=5;
const background=new T.Mesh(new T.PlaneGeometry(2,2),backgroundMaterial);background.renderOrder=-1;scene.add(background);

// Three depth bands in one draw call: translucent distant flakes, sharp foreground
// flakes or actual maple silhouettes. Integer fall speeds keep the 12 s seam exact.
let particles;
if(entry.weather!=='rain'){
 const leafCanvas=document.createElement('canvas');leafCanvas.width=leafCanvas.height=160;
 const ctx=leafCanvas.getContext('2d');
 const outline=[[80,8],[86,30],[95,26],[95,46],[116,24],[113,44],[128,40],[122,57],[148,55],[131,70],[143,77],[120,87],[125,99],[99,96],[92,110],[85,105],[86,140],[79,145],[76,109],[68,114],[62,99],[36,106],[40,91],[15,86],[29,75],[10,64],[39,63],[29,48],[43,51],[42,31],[63,50],[62,31],[71,35]];
 ctx.beginPath();outline.forEach(([x,y],n)=>n?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.closePath();
 const pigment=ctx.createLinearGradient(40,20,110,130);pigment.addColorStop(0,'#EFBB52');pigment.addColorStop(.4,'#D27125');pigment.addColorStop(.78,'#922B16');pigment.addColorStop(1,'#5F2519');ctx.fillStyle=pigment;ctx.fill();ctx.strokeStyle='#732916';ctx.lineWidth=1.8;ctx.stroke();
 ctx.strokeStyle='rgba(103,41,17,.75)';ctx.lineWidth=1.4;
 for(const [x,y] of [[80,12],[115,28],[145,57],[143,78],[123,99],[40,103],[16,84],[14,65],[43,34]]){ctx.beginPath();ctx.moveTo(80,103);ctx.lineTo(x,y);ctx.stroke();}
 ctx.strokeStyle='rgba(245,196,109,.6)';ctx.lineWidth=.7;ctx.beginPath();ctx.moveTo(79,140);ctx.lineTo(79,18);ctx.stroke();
 const leaf=new T.CanvasTexture(leafCanvas);leaf.colorSpace=T.SRGBColorSpace;
 const count=entry.weather==='snow'?210:52,geometry=new T.BufferGeometry();
 geometry.setAttribute('position',new T.BufferAttribute(new Float32Array(count*3),3));
 geometry.setAttribute('particle',new T.BufferAttribute(Float32Array.from({length:count},(_,n)=>n),1));
 const material=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,uniforms:{phase,viewport,leaf:{value:leaf},panes:uniforms.panes,weatherRect:uniforms.weatherRect,snow:{value:entry.weather==='snow'?1:0}},
  vertexShader:`attribute float particle;uniform float phase,snow;uniform vec2 viewport;uniform vec4 weatherRect;varying float fade,spin,depth;varying vec2 screen;
float hash(float n){return fract(sin(n*127.13)*43758.5453);}void main(){float n=particle;depth=hash(n+13.);float cycles=depth>.67?2.:1.;vec2 p=vec2(hash(n+4.)+sin(phase+n)*(.025+depth*.04),fract(hash(n+31.)+phase/6.28318530718*cycles));p=mix(weatherRect.xy,weatherRect.zw,p);screen=p;fade=smoothstep(weatherRect.y,weatherRect.y+.035,p.y)*(1.-smoothstep(weatherRect.w-.035,weatherRect.w,p.y))*(.3+.55*depth);spin=phase*2.+n;gl_Position=vec4(p.x*2.-1.,1.-p.y*2.,0.,1.);gl_PointSize=viewport.y*(snow>.5?.006:.020)*(.5+depth*1.4);}`,
  fragmentShader:`uniform sampler2D leaf;uniform float snow;uniform vec4 weatherRect,panes[3];varying float fade,spin,depth;varying vec2 screen;float inside(vec2 p,vec4 r){return step(r.x,p.x)*step(p.x,r.z)*step(r.y,p.y)*step(p.y,r.w);}void main(){float glass=max(inside(screen,panes[0]),max(inside(screen,panes[1]),inside(screen,panes[2])));if(glass<.5)discard;vec2 d=(gl_PointCoord-.5)*2.;float a;vec3 color;if(snow>.5){a=exp(-dot(d,d)*(3.+depth*2.));color=vec3(.88,.94,1.);}else{d=mat2(cos(spin),-sin(spin),sin(spin),cos(spin))*d;d.x/=max(.24,abs(cos(spin*.5)));vec4 painted=texture2D(leaf,d*.5+.5);if(abs(d.x)>1.||abs(d.y)>1.)discard;a=painted.a;color=painted.rgb*(.72+.28*abs(cos(spin*.5)));}a*=fade;if(a<.003)discard;gl_FragColor=vec4(color,a);}`});
 particles=new T.Points(geometry,material);particles.frustumCulled=false;particles.renderOrder=1;scene.add(particles);
}
let frames=0,time=0,paused=false;
function resize(){renderer.setSize(innerWidth,innerHeight,false);viewport.value.set(innerWidth,innerHeight);}
addEventListener('resize',resize);resize();
function renderAt(seconds){time=((seconds%12)+12)%12;phase.value=time/12*Math.PI*2;renderer.render(scene,camera);frames++;}
window.living={renderAt,metrics:()=>({frames,time,resolution:renderer.getDrawingBufferSize(new T.Vector2()).toArray(),weather:entry.weather,particles:particles?.geometry.attributes.particle.count||0,character:false}),pause:value=>paused=value};
window.chrome?.webview?.addEventListener('message',async e=>{if(e.data.type==='pause')paused=!!e.data.value;if(e.data.type==='render'){renderAt(e.data.time);if(manual)renderer.getContext().finish();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));send({type:'frame',id:e.data.id});}});
renderAt(0);send({type:'ready'});
const start=performance.now();function animate(now){if(!paused)renderAt((now-start)/1000);requestAnimationFrame(animate);}if(!manual)requestAnimationFrame(animate);

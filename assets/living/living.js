import * as T from 'three';

// Fixed illustration, animated weather and reflections; no added people or transport sprites.
const params=new URLSearchParams(location.search),id=Number(params.get('scene')||1001),manual=params.get('manual')==='1';
const index=Math.max(0,Math.min(2,id-1001)),names=['rain','metro','sakura'],send=o=>window.chrome?.webview?.postMessage(o);
const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true,preserveDrawingBuffer:true});
renderer.setPixelRatio(1);renderer.outputColorSpace=T.SRGBColorSpace;
const scene=new T.Scene(),camera=new T.OrthographicCamera(-1,1,1,-1,.01,10);camera.position.z=2;
const loader=new T.TextureLoader();
const background=await loader.loadAsync(names[index]+'-background.png');
background.colorSpace=T.SRGBColorSpace;background.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());
let time=0,frames=0,paused=false;
const material=new T.ShaderMaterial({uniforms:{image:{value:background},phase:{value:0},kind:{value:index}},vertexShader:`varying vec2 pos;void main(){pos=uv;gl_Position=vec4(position.xy,0.,1.);}`,fragmentShader:`
uniform sampler2D image;uniform float phase;uniform int kind;varying vec2 pos;
float band(float a,float b,float v){return smoothstep(a,a+.018,v)*(1.-smoothstep(b-.018,b,v));}
void main(){vec2 p=vec2(pos.x,1.-pos.y),q=p;float water=0.,wet=0.,hair=0.,sky=0.;
if(kind==0){water=band(.52,.99,p.y)*band(max(0.,.42-.62*p.y),.86-.43*p.y,p.x);water*=smoothstep(.8,1.2,length((p-vec2(.04,.59))/vec2(.12,.085)));hair=band(.775,.91,p.x)*band(.245,.57,p.y);sky=band(.16,.45,p.x)*(1.-smoothstep(.11,.17,p.y));}
if(kind==1){water=band(.405,.59,p.y)*(1.-smoothstep(.38,.58,p.x));wet=band(.58,.99,p.y)*band(.16,.69,p.x);hair=band(.8,.925,p.x)*band(.245,.51,p.y);sky=(1.-smoothstep(.23-.18*p.x,.26-.18*p.x,p.y));}
if(kind==2){wet=band(.63,1.,p.y)*band(.41-.1*(p.y-.6),.66+.19*(p.y-.6),p.x);}
float waves=sin(p.y*310.+phase*4.)+sin(p.x*170.+p.y*220.-phase*7.)*.45;
q.x+=water*waves*.00045;q.y+=water*sin(p.x*185.+phase*3.)*.0003;
q.x+=wet*sin(p.y*370.+p.x*140.+phase*6.)*.00035;
float strands=sin(p.y*21.+phase*2.)*.5+sin(p.y*43.-phase*3.)*.22;
q.x+=hair*strands*.00115;q.y+=hair*sin(phase*2.)*.00045;
vec2 moon=kind==0?vec2(.302,.09):vec2(.37,.09);sky*=smoothstep(.025,.045,length((p-moon)*vec2(1.,.5625)));q.x+=sky*sin(phase)*.0017;
vec3 color=texture2D(image,vec2(q.x,1.-q.y)).rgb;
float ripple=sin(p.y*520.-phase*9.)*sin(p.x*195.+phase*7.);
color+=vec3(.014,.021,.026)*water*ripple*.23;
float neon=sin(phase*3.)*.018+sin(phase*7.+1.)*.007;
color*=1.+neon*smoothstep(.19,.5,dot(color,vec3(.2126,.7152,.0722)));
gl_FragColor=vec4(color,1.);
#include <colorspace_fragment>
}`});const plate=new T.Mesh(new T.PlaneGeometry(2,2),material);scene.add(plate);

// A screen-space overlay keeps the rain thin and translucent over the original painted art.
const overlayMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,uniforms:{phase:{value:0},kind:{value:index}},vertexShader:material.vertexShader,fragmentShader:`uniform float phase;uniform int kind;varying vec2 pos;float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}void main(){vec2 p=vec2(pos.x,1.-pos.y);vec3 c=vec3(.65,.77,.85);float alpha=0.;if(kind!=1){vec2 grid=vec2(p.x*120.+p.y*8.,p.y*26.+phase/6.2831853*52.);vec2 cell=floor(grid),f=fract(grid);float rain=step(.79,hash(vec2(cell.x,0.)))*exp(-pow((f.x-.5)*115.,2.))*smoothstep(.1,.2,f.y)*(1.-smoothstep(.62,.79,f.y));alpha=rain*.17;}if(kind!=0){for(int i=0;i<18;i++){float fi=float(i);vec2 center=fract(vec2(hash(vec2(fi,4.))+phase/6.2831853,hash(vec2(fi,9.))+phase/6.2831853));center.x+=sin(phase*2.+fi)*.013;vec2 q=(p-center)*vec2(1.,.5625);float angle=phase*2.+fi;mat2 turn=mat2(cos(angle),-sin(angle),sin(angle),cos(angle));q=turn*q;float petal=exp(-dot(q*vec2(1.,2.5),q*vec2(1.,2.5))*180000.);alpha=max(alpha,petal*.52*smoothstep(0.,.04,p.y)*(1.-smoothstep(.96,1.,p.y)));if(petal>.1)c=vec3(.95,.62,.78);}}gl_FragColor=vec4(c,alpha);}`});
const overlay=new T.Mesh(new T.PlaneGeometry(2,2),overlayMaterial);overlay.renderOrder=5;scene.add(overlay);

function resize(){renderer.setSize(innerWidth,innerHeight,false);}addEventListener('resize',resize);resize();
function renderAt(seconds){time=((seconds%12)+12)%12;material.uniforms.phase.value=time/12*Math.PI*2;overlayMaterial.uniforms.phase.value=material.uniforms.phase.value;renderer.render(scene,camera);frames++;return frames;}
window.living={renderAt,metrics:()=>({frames,time,resolution:renderer.getDrawingBufferSize(new T.Vector2()).toArray(),kind:names[index],spriteOverlays:scene.children.filter(o=>o.isMesh&&o!==plate&&o!==overlay).length}),pause:value=>paused=value};
window.chrome?.webview?.addEventListener('message',async e=>{if(e.data.type==='pause')paused=!!e.data.value;if(e.data.type==='render'){renderAt(e.data.time);if(manual)renderer.getContext().finish();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));send({type:'frame',id:e.data.id});}});
renderAt(0);send({type:'ready'});
let start=performance.now();function animate(now){if(!paused)renderAt((now-start)/1000);requestAnimationFrame(animate);}if(!manual)requestAnimationFrame(animate);

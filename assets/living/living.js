import * as T from 'three';

// The camera stays fixed. Only the scene's own water, hair, rain, petals and transport move.
const params=new URLSearchParams(location.search),id=Number(params.get('scene')||1001),manual=params.get('manual')==='1';
const index=Math.max(0,Math.min(2,id-1001)),names=['rain','metro','sakura'],send=o=>window.chrome?.webview?.postMessage(o);
const renderer=new T.WebGLRenderer({canvas:document.querySelector('canvas'),antialias:true,preserveDrawingBuffer:true});
renderer.setPixelRatio(1);renderer.outputColorSpace=T.SRGBColorSpace;
const scene=new T.Scene(),camera=new T.OrthographicCamera(-1,1,1,-1,.01,10);camera.position.z=2;
const loader=new T.TextureLoader();
const [background,trainImage,coupleImage]=await Promise.all([loader.loadAsync(names[index]+'-background.png'),loader.loadAsync('commuter.png'),loader.loadAsync('couple.png')]);
for(const texture of [background,trainImage,coupleImage]){texture.colorSpace=T.SRGBColorSpace;texture.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());}
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
}`});scene.add(new T.Mesh(new T.PlaneGeometry(2,2),material));

// A strip is bent onto each scene's actual rail path. Each carriage follows the curve.
const trainGeometry=new T.BufferGeometry(),trainSegments=80;
const trainVertices=new Float32Array((trainSegments+1)*2*3),trainUV=new Float32Array((trainSegments+1)*2*2),indices=[];
for(let i=0;i<=trainSegments;i++){const u=.0138+i/trainSegments*.973;trainUV.set([u,.2665,u,.738],i*4);if(i<trainSegments){const k=i*2;indices.push(k,k+1,k+2,k+1,k+3,k+2);}}
trainGeometry.setAttribute('position',new T.BufferAttribute(trainVertices,3));trainGeometry.setAttribute('uv',new T.BufferAttribute(trainUV,2));trainGeometry.setIndex(indices);
const trainMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,side:T.DoubleSide,uniforms:{image:{value:trainImage},kind:{value:index}},vertexShader:`varying vec2 spriteUV;varying vec2 screenPoint;void main(){spriteUV=uv;screenPoint=vec2(position.x*.5+.5,.5-position.y*.5);gl_Position=vec4(position,1.);}`,fragmentShader:`uniform sampler2D image;uniform int kind;varying vec2 spriteUV;varying vec2 screenPoint;void main(){vec4 c=texture2D(image,spriteUV);if(c.a<.05)discard;float mask=1.;if(kind==0)mask=smoothstep(.19,.205,screenPoint.x)*(1.-smoothstep(.70,.715,screenPoint.x));if(kind==1)mask=1.-smoothstep(.6,.64,screenPoint.x);if(kind==2)mask=smoothstep(.385,.405,screenPoint.x)*(1.-smoothstep(.555,.575,screenPoint.x));gl_FragColor=vec4(c.rgb,c.a*mask);
#include <colorspace_fragment>
}`});
const train=new T.Mesh(trainGeometry,trainMaterial);train.frustumCulled=false;train.renderOrder=2;scene.add(train);
const peopleMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,side:T.DoubleSide,uniforms:{image:{value:coupleImage},phase:{value:0}},vertexShader:`varying vec2 spriteUV;varying vec2 screenPoint;void main(){spriteUV=uv;screenPoint=vec2((modelMatrix*vec4(position,1.)).x*.5+.5,.5-(modelMatrix*vec4(position,1.)).y*.5);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,fragmentShader:`uniform sampler2D image;uniform float phase;varying vec2 spriteUV;varying vec2 screenPoint;void main(){vec2 q=spriteUV;float legs=(1.-smoothstep(.27,.46,q.y));q.x+=sin(phase*12.+step(.52,q.x)*3.14159)*legs*.009;q.y+=sin(phase*12.)*.0018;vec4 c=texture2D(image,vec2(.143+q.x*.733,.055+q.y*.867));if(c.a<.06)discard;float mask=smoothstep(.513,.527,screenPoint.x)*(1.-smoothstep(.603,.614,screenPoint.x));gl_FragColor=vec4(c.rgb,c.a*mask);
#include <colorspace_fragment>
}`});
const people=new T.Mesh(new T.PlaneGeometry(1,1),peopleMaterial);people.renderOrder=3;people.visible=index===2;scene.add(people);
function track(x){
if(index===0)return{y:.329-.028*x,h:.052};
if(index===1){const y=.69-.26*x-.5*Math.pow(Math.max(0,x-.35),2);return{y,h:.095*(1.-Math.min(.9,Math.max(0,x))*.93)};}
return{y:.173+.255*x,h:.052};
}
function transport(t){
const progress=(t%12)/12,width=index===0?.76:index===1?.56:.58,start=-width-.06+progress*(1+width+.12);
for(let i=0;i<=trainSegments;i++){const x=start+i/trainSegments*width,{y,h}=track(x);trainVertices.set([x*2-1,1-y*2,.3,x*2-1,1-(y-h)*2,.3],i*6);}
trainGeometry.attributes.position.needsUpdate=true;
// Buildings, foreground wiring and the edge of each bridge occlude the passing train.
train.visible= index!==2||start<.66;
}

// A screen-space overlay keeps the rain thin and translucent over the original painted art.
const overlayMaterial=new T.ShaderMaterial({transparent:true,depthTest:false,depthWrite:false,uniforms:{phase:{value:0},kind:{value:index}},vertexShader:material.vertexShader,fragmentShader:`uniform float phase;uniform int kind;varying vec2 pos;float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}void main(){vec2 p=vec2(pos.x,1.-pos.y);vec3 c=vec3(.65,.77,.85);float alpha=0.;if(kind!=1){vec2 grid=vec2(p.x*120.+p.y*8.,p.y*26.+phase/6.2831853*52.);vec2 cell=floor(grid),f=fract(grid);float rain=step(.79,hash(vec2(cell.x,0.)))*exp(-pow((f.x-.5)*115.,2.))*smoothstep(.1,.2,f.y)*(1.-smoothstep(.62,.79,f.y));alpha=rain*.17;}if(kind!=0){for(int i=0;i<18;i++){float fi=float(i);vec2 center=fract(vec2(hash(vec2(fi,4.))+phase/6.2831853,hash(vec2(fi,9.))+phase/6.2831853));center.x+=sin(phase*2.+fi)*.013;vec2 q=(p-center)*vec2(1.,.5625);float angle=phase*2.+fi;mat2 turn=mat2(cos(angle),-sin(angle),sin(angle),cos(angle));q=turn*q;float petal=exp(-dot(q*vec2(1.,2.5),q*vec2(1.,2.5))*180000.);alpha=max(alpha,petal*.52*smoothstep(0.,.04,p.y)*(1.-smoothstep(.96,1.,p.y)));if(petal>.1)c=vec3(.95,.62,.78);}}gl_FragColor=vec4(c,alpha);}`});
const overlay=new T.Mesh(new T.PlaneGeometry(2,2),overlayMaterial);overlay.renderOrder=5;scene.add(overlay);

function resize(){renderer.setSize(innerWidth,innerHeight,false);}addEventListener('resize',resize);resize();
function renderAt(seconds){time=((seconds%12)+12)%12;material.uniforms.phase.value=time/12*Math.PI*2;overlayMaterial.uniforms.phase.value=material.uniforms.phase.value;transport(time);if(index===2){const p=time/12,h=.18-.025*p,w=h*.563/(innerWidth/innerHeight);people.scale.set(w*2,h*2,1);people.position.set((.478+p*.18)*2-1,1-(.735-.035*p-h*.5)*2,.4);peopleMaterial.uniforms.phase.value=material.uniforms.phase.value;}renderer.render(scene,camera);frames++;return frames;}
window.living={renderAt,metrics:()=>({frames,time,resolution:renderer.getDrawingBufferSize(new T.Vector2()).toArray(),kind:names[index]}),pause:value=>paused=value};
window.chrome?.webview?.addEventListener('message',async e=>{if(e.data.type==='pause')paused=!!e.data.value;if(e.data.type==='render'){renderAt(e.data.time);await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));send({type:'frame',id:e.data.id});}});
renderAt(0);send({type:'ready'});
let start=performance.now();function animate(now){if(!paused)renderAt((now-start)/1000);requestAnimationFrame(animate);}if(!manual)requestAnimationFrame(animate);

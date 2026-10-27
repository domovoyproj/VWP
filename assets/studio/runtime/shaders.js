export const layerVertex=`#version 300 es
precision highp float;
layout(location=0)in vec2 aUv;
uniform sampler2D uMask;
uniform vec4 uRect;
uniform vec4 uFrame;
uniform vec2 uCursor;
uniform vec2 uResolution;
uniform float uTime,uDepth,uParallax,uWind,uStrength,uMotion;
uniform int uKind,uAnchor;
out vec2 vUv;
void main(){
  vUv=aUv;vec2 position=uRect.xy+aUv*uRect.zw;
  float mask=texture(uMask,aUv).b;
  float anchor=uAnchor==0?aUv.y:uAnchor==1?1.-aUv.y:sin(aUv.y*3.14159265);
  float bend=anchor*anchor;
  if(uKind==1||uKind==2){
    float gust=sin(uTime*3.+aUv.y*2.1)+.35*sin(uTime*7.+aUv.x*4.);
    float weight=uKind==1?mask*bend:mask;
    position.x+=gust*.010*uWind*uStrength*weight;
    position.y+=sin(uTime*4.+aUv.x*5.)*.0018*uWind*uStrength*weight;
  }
  if(uKind==3){
    position.x+=sin(uTime*5.+aUv.y*4.)*.006*uWind*uStrength*mask;
    position.y-=sin(uTime*6.)*.003*uStrength*uMotion*texture(uMask,aUv).a;
  }
  position+=uCursor*uDepth*uParallax*vec2(.025,.018);
  position=uFrame.xy+position*uFrame.zw;
  gl_Position=vec4(position.x*2.-1.,1.-position.y*2.,0.,1.);
}`;

export const layerFragment=`#version 300 es
precision highp float;
precision highp int;
in vec2 vUv;
uniform sampler2D uImage,uMask;
uniform vec2 uResolution,uSun;
uniform float uTime,uOpacity,uMotion,uStrength,uWaterline,uAudio;
uniform int uMaskView,uKind;
out vec4 frag;
const float PI=3.14159265359;
vec3 linear(vec3 c){return pow(max(c,vec3(0.)),vec3(2.2));}
void main(){
  vec4 mask=texture(uMask,vUv);
  vec2 uv=vUv;
  float rainLight=0.;
  if(uKind==4){
    float column=floor(vUv.x*72.);
    float seed=fract(sin(column*127.1+311.7)*43758.5453);
    float speed=1.+floor(seed*3.);
    vec2 drop=vec2(fract(vUv.x*72.)-.5,fract(vUv.y*12.-uTime/(2.*PI)*speed+seed)-.5);
    drop.x+=sin(column*4.)*.2;
    float shape=exp(-dot(drop*vec2(5.,14.),drop*vec2(5.,14.)));
    uv+=drop*vec2(.018,.006)*shape*mask.r*uStrength*uMotion;
    rainLight=shape*mask.r*uStrength*uMotion*step(.35,seed);
  }
  float depth=pow(clamp((vUv.y-uWaterline)/max(.01,1.-uWaterline),0.,1.),1.35);
  float a=sin(vUv.x*147.+vUv.y*123.+uTime*18.);
  float b=sin(vUv.x*81.-vUv.y*218.-uTime*13.);
  float c=cos(vUv.x*226.+vUv.y*57.+uTime*11.);
  vec2 ripple=vec2(a*.0012+b*.0007,c*.00055+b*.0003)*depth*mask.r*uStrength*uMotion;
  uv+=ripple;
  vec4 base=texture(uImage,clamp(uv,vec2(.001),vec2(.999)));
  vec2 cloudShift=vec2(sin(uTime)*.009,(cos(uTime)-1.)*.0015)*uStrength*uMotion;
  if(mask.g>.001)base=mix(base,texture(uImage,clamp(vUv-cloudShift,vec2(.001),vec2(.999))),mask.g);
  vec3 color=linear(base.rgb/max(base.a,.001));
  color+=vec3(.06,.08,.10)*rainLight;
  float shimmer=pow(max(0.,a*.35+b*.25+c*.4),9.);
  float sunPath=exp(-pow((vUv.x-uSun.x)/(.008+depth*.12),2.));
  color+=vec3(1.,.73,.38)*shimmer*sunPath*depth*mask.r*.095*uMotion;
  float light=uKind==3?0.:mask.a;
  float glow=1.+light*(.08*sin(uTime*3.)+.04*sin(uTime*7.+2.)) *uMotion;
  color*=glow;
  color+=light*vec3(.12,.04,.01)*(1.+uAudio*.35);
  if(uMaskView>=0){float value=uMaskView==0?mask.r:uMaskView==1?mask.g:uMaskView==2?mask.b:mask.a;color=mix(color,vec3(.1,.5,.9),value*.55);}
  frag=vec4(color,base.a*uOpacity);
}`;

export const bloomFragment=`#version 300 es
precision highp float;in vec2 vUv;uniform sampler2D uSource;uniform vec2 uStep;uniform int uExtract;out vec4 frag;
void main(){vec3 c=texture(uSource,vUv).rgb*.227027;
c+=texture(uSource,vUv+uStep*1.384615).rgb*.316216;c+=texture(uSource,vUv-uStep*1.384615).rgb*.316216;
c+=texture(uSource,vUv+uStep*3.230769).rgb*.070270;c+=texture(uSource,vUv-uStep*3.230769).rgb*.070270;
if(uExtract==1)c=max(c-vec3(.55),vec3(0.));frag=vec4(c,1.);}`;

export const finalFragment=`#version 300 es
precision highp float;in vec2 vUv;uniform sampler2D uScene,uBloom;uniform float uExposure,uBloomAmount;out vec4 frag;
void main(){vec3 color=texture(uScene,vUv).rgb*exp2(uExposure)+texture(uBloom,vUv).rgb*uBloomAmount;
color=color/(1.+max(color-1.,vec3(0.))*.4);color=pow(max(color,vec3(0.)),vec3(1./2.2));
float vignette=1.-.07*pow(length((vUv-.5)*vec2(1.,.8)),2.);color*=vignette;
float dither=(fract(sin(dot(gl_FragCoord.xy,vec2(12.9898,78.233)))*43758.5453)-.5)/255.;frag=vec4(clamp(color+dither,0.,1.),1.);}`;

export const particleVertex=`#version 300 es
precision highp float;layout(location=0)in vec4 aSeed;
uniform float uTime,uHeight,uKind,uWind,uDensity;uniform vec4 uRegion,uFrame;uniform vec2 uCursor;
out float vAlpha;out float vBlur;
void main(){float depth=.15+aSeed.z*.85;float y=fract(aSeed.y+uTime/(6.28318530718)*(1.+floor(aSeed.w*3.)));
float x=fract(aSeed.x+sin(uTime*2.+aSeed.w*6.28)*.015+uWind*y*.06);
if(uKind>1.5){x=aSeed.x+sin(uTime+aSeed.w*6.28)*.015;y=aSeed.y+cos(uTime*2.+aSeed.w*6.28)*.015;}
vec2 position=uRegion.xy+vec2(x,y)*uRegion.zw+uCursor*depth*.015;
position=uFrame.xy+position*uFrame.zw;
gl_Position=vec4(position.x*2.-1.,1.-position.y*2.,0.,1.);
gl_PointSize=uHeight*(uKind>2.5?.001+depth*.0015:uKind>1.5?.003:.0013+pow(depth,3.)*.006);
vAlpha=(.2+depth*.45)*smoothstep(0.,.05,y)*(1.-smoothstep(.95,1.,y));
if(uKind>2.5)vAlpha*=.3;else if(uKind>1.5)vAlpha*=.3+.7*pow(.5+.5*sin(uTime*3.+aSeed.w*6.28),3.);
vBlur=depth>.8?.8:.3;}`;

export const particleFragment=`#version 300 es
precision highp float;in float vAlpha,vBlur;uniform vec3 uColor;out vec4 frag;
void main(){float r=length(gl_PointCoord-.5)*2.;float a=(1.-smoothstep(.15,1.,r))*vAlpha;frag=vec4(uColor,a);}`;

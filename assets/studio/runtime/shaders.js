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
uniform vec3 uTint;
uniform float uSurfaceStrength,uLightStrength,uSeed;
uniform int uSurface,uLight;
uniform float uTime,uOpacity,uMotion,uStrength,uWaterline,uAudio,uWind;
uniform int uMaskView,uKind,uClip;
out vec4 frag;
const float PI=3.14159265359;
vec3 linear(vec3 c){return pow(max(c,vec3(0.)),vec3(2.2));}
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
float cloud(vec2 p){return noise(p)*.57+noise(p*2.03)*.29+noise(p*4.07)*.14;}
void main(){
  vec4 mask=texture(uMask,vUv);
  vec2 uv=vUv;
  // Per-pixel fields keep nearby faces and architecture completely stationary.
  if(uKind==3||uKind==5){uv.x+=sin(uTime*5.+vUv.y*4.)*.004*uWind*uStrength*mask.b;uv.y+=sin(uTime*3.+vUv.x*6.)*.0008*uWind*uStrength*mask.b;if(uKind==3)uv.y+=sin(uTime*6.)*.002*uStrength*uMotion*mask.a;}
  float rainLight=0.;
  if(uKind==4||uSurface==2){
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
  float amount=uStrength*uMotion*uSurfaceStrength;
  if(uSurface==0||uSurface==1)uv+=ripple*(uSurface==1?1.8:1.)*uSurfaceStrength;
  if(uSurface==8){uv.y+=sin(vUv.y*185.-uTime*16.+sin(vUv.x*75.)*2.)*.003*mask.r*amount;uv.x+=sin(vUv.y*98.-uTime*11.)*.001*mask.r*amount;}
  vec2 orbit=vec2(sin(uTime),cos(uTime));
  float veil=0.;if(uSurface>=3&&uSurface<=7&&mask.r>.001)veil=cloud(vUv*vec2(6.,9.)+orbit*.32+uSeed);
  if(uSurface==4){vec3 sourceColor=texture(uImage,vUv).rgb;float chroma=clamp((max(sourceColor.g,sourceColor.b)-sourceColor.r)*3.,0.,1.);uv+=vec2(sin(vUv.x*18.+uTime*2.)*.004,sin(vUv.x*25.+uTime*3.)*.008)*mask.r*amount*chroma;}
  if(uSurface==5||uSurface==6||uSurface==7)uv+=vec2(sin(vUv.y*36.+uTime*2.),cos(vUv.x*28.-uTime*3.))*.0018*mask.r*amount;
  vec4 base=texture(uImage,clamp(uv,vec2(.001),vec2(.999)));
  vec2 cloudShift=vec2(sin(uTime)*.009,(cos(uTime)-1.)*.0015)*uStrength*uMotion;
  if(mask.g>.001)base=mix(base,texture(uImage,clamp(vUv-cloudShift,vec2(.001),vec2(.999))),mask.g);
  vec3 color=linear(base.rgb/max(base.a,.001));
  color+=vec3(.06,.08,.10)*rainLight;
  float shimmer=pow(max(0.,a*.35+b*.25+c*.4),9.);
  float sunPath=exp(-pow((vUv.x-uSun.x)/(.008+depth*.12),2.));
  if(uSurface==0||uSurface==1)color+=vec3(1.,.73,.38)*shimmer*sunPath*depth*mask.r*.095*uMotion*uSurfaceStrength;
  if(uSurface==3)color=mix(color,vec3(.36,.43,.48),veil*mask.r*.105*amount);
  if(uSurface==4)color+=uTint*mask.r*pow(veil,2.)*.075*amount;
  if(uSurface==5)color+=uTint*mask.r*pow(veil,4.)*.045*amount;
  if(uSurface==6||uSurface==9)color+=uTint*mask.r*pow(max(0.,sin(vUv.x*33.+vUv.y*21.+uTime*3.)),16.)*.025*amount;
  if(uSurface==7)color+=uTint*mask.r*pow(max(0.,sin(vUv.y*45.-uTime*7.+veil*8.)),12.)*.16*amount;
  if(uSurface==8)color+=vec3(.22,.27,.29)*mask.r*pow(max(0.,sin(vUv.y*185.-uTime*16.+vUv.x*31.)),8.)*.12*amount;
  float light=uKind==3?0.:mask.a;
  float glow=1.+light*(.035*sin(uTime*3.+uSeed)+.015*sin(uTime*7.+2.))*uMotion*uLightStrength;
  if(uLight<2||uLight==4){color*=glow;color+=light*(uLight==1?uTint*.065:vec3(.08,.026,.006))*(1.+uAudio*.35)*uLightStrength;}
  if(uLight==4)color+=uTint*light*pow(max(0.,sin(uTime*23.+vUv.y*20.)),5.)*.09*uMotion*uLightStrength;
  if(uLight==2){vec2 cells=vUv*vec2(430.,240.),cell=floor(cells);float point=exp(-dot(fract(cells)-.5,fract(cells)-.5)*90.);float seed=hash(cell+uSeed);color+=uTint*light*point*step(.996,seed)*(.15+.85*pow(.5+.5*sin(uTime*3.+seed*30.),3.))*.5*uMotion*uLightStrength;}
  if(uLight==3&&light>.001){
    float bolts=0.;
    for(int i=0;i<4;i++){float s=float(i)*1.731+uSeed;float center=.12+fract(sin(s*3.7)*437.3)*.76;float path=center+sin(vUv.y*19.+s+sin(uTime*4.))*.027+sin(vUv.y*57.+s)*.009;float distance=abs(vUv.x-path);float pulse=pow(max(0.,sin(uTime*8.+s*2.)),22.);bolts+=(exp(-distance*1500.)*.8+exp(-distance*130.)*.14)*pulse;float branch=abs(vUv.x-(path+(vUv.y-.48)*.16));bolts+=exp(-branch*1100.)*.28*pulse*smoothstep(.12,.3,vUv.y)*(1.-smoothstep(.62,.76,vUv.y));}
    color+=uTint*light*bolts*uMotion*uLightStrength;
  }
  if(uMaskView>=0){float value=uMaskView==0?mask.r:uMaskView==1?mask.g:uMaskView==2?mask.b:mask.a;color=mix(color,vec3(.1,.5,.9),value*.55);}
  frag=vec4(color,base.a*uOpacity*(uClip==1?mask.r:1.));
}`;

export const bloomFragment=`#version 300 es
precision highp float;in vec2 vUv;uniform sampler2D uSource;uniform vec2 uStep;uniform int uExtract;out vec4 frag;
void main(){vec3 c=texture(uSource,vUv).rgb*.227027;
c+=texture(uSource,vUv+uStep*1.384615).rgb*.316216;c+=texture(uSource,vUv-uStep*1.384615).rgb*.316216;
c+=texture(uSource,vUv+uStep*3.230769).rgb*.070270;c+=texture(uSource,vUv-uStep*3.230769).rgb*.070270;
if(uExtract==1)c=max(c-vec3(.55),vec3(0.));frag=vec4(c,1.);}`;

export const finalFragment=`#version 300 es
precision highp float;in vec2 vUv;uniform sampler2D uScene,uBloom,uWeatherMask;uniform float uExposure,uBloomAmount;uniform int uWeatherView;uniform vec4 uFrame;out vec4 frag;
void main(){vec3 color=texture(uScene,vUv).rgb*exp2(uExposure)+texture(uBloom,vUv).rgb*uBloomAmount;
if(uWeatherView==1){vec2 uv=(vec2(vUv.x,1.-vUv.y)-uFrame.xy)/uFrame.zw;float inside=step(0.,uv.x)*step(0.,uv.y)*step(uv.x,1.)*step(uv.y,1.);color=mix(color,vec3(.1,.5,.9),texture(uWeatherMask,uv).r*.55*inside);}
color=color/(1.+max(color-1.,vec3(0.))*.4);color=pow(max(color,vec3(0.)),vec3(1./2.2));
float vignette=1.-.07*pow(length((vUv-.5)*vec2(1.,.8)),2.);color*=vignette;
float dither=(fract(sin(dot(gl_FragCoord.xy,vec2(12.9898,78.233)))*43758.5453)-.5)/255.;frag=vec4(clamp(color+dither,0.,1.),1.);}`;

export const particleVertex=`#version 300 es
precision highp float;layout(location=0)in vec4 aSeed;
uniform float uTime,uHeight,uKind,uWind,uDensity;uniform vec4 uRegion,uFrame;uniform vec2 uCursor;
out float vAlpha;out float vAngle;out vec2 vSceneUv;out vec2 vExtent;
void main(){float depth=.15+aSeed.z*.85;float y=fract(aSeed.y+uTime/(6.28318530718)*(1.+floor(aSeed.w*3.)));
float x=fract(aSeed.x+sin(uTime*2.+aSeed.w*6.28)*.015+uWind*y*.06);
if(uKind>1.5&&uKind<3.5){x=aSeed.x+sin(uTime+aSeed.w*6.28)*.015;y=aSeed.y+cos(uTime*2.+aSeed.w*6.28)*.015;}
if((uKind>6.5&&uKind<7.5)||uKind>8.5)y=1.-y;
if(uKind>7.5&&uKind<8.5){y=fract(aSeed.y+uTime/6.28318530718*3.);x=fract(aSeed.x+y*.65);}
vec2 position=uRegion.xy+vec2(x,y)*uRegion.zw+uCursor*depth*.015;
vSceneUv=position;
position=uFrame.xy+position*uFrame.zw;
gl_Position=vec4(position.x*2.-1.,1.-position.y*2.,0.,1.);
float size=uKind<1.5?.0013+pow(depth,3.)*.006:uKind<2.5?.003:uKind<3.5?.001+depth*.0015:uKind<4.5?.012+depth*.02:uKind<6.5?.003+depth*.006:uKind<7.5?.0015+depth*.002:uKind<8.5?.025+depth*.025:.002+depth*.004;
gl_PointSize=uHeight*size;vExtent=vec2(.5625,1.)*size;
vAlpha=(.2+depth*.45)*smoothstep(0.,.05,y)*(1.-smoothstep(.95,1.,y));
if(uKind>2.5&&uKind<3.5)vAlpha*=.3;else if(uKind>1.5&&uKind<2.5)vAlpha*=.3+.7*pow(.5+.5*sin(uTime*3.+aSeed.w*6.28),3.);
if(uKind>3.5&&uKind<4.5)vAlpha*=.28;
if(uKind>7.5&&uKind<8.5)vAlpha*=pow(max(0.,sin(uTime*2.+aSeed.w*6.28)),10.);
vAngle=aSeed.w*6.28+sin(uTime*2.+aSeed.x*6.)*1.5;}`;

export const particleFragment=`#version 300 es
precision highp float;in float vAlpha,vAngle;in vec2 vSceneUv,vExtent;uniform vec3 uColor;uniform float uKind;uniform sampler2D uWeatherMask;out vec4 frag;
void main(){vec2 q=gl_PointCoord-.5;float r=length(q)*2.;float shape=1.-smoothstep(.15,1.,r);vec3 color=uColor;
if(uKind>3.5&&uKind<4.5)shape=exp(-q.x*q.x*2600.)*(1.-smoothstep(.15,.5,abs(q.y)));
if(uKind>4.5&&uKind<6.5){float c=cos(vAngle),s=sin(vAngle);q=mat2(c,-s,s,c)*q;float leaf=length(q*vec2(2.9,1.8));shape=(1.-smoothstep(.55,.8,leaf))*(.6+.4*cos(q.x*6.));color*=.7+.3*cos(q.y*8.);}
if(uKind>7.5&&uKind<8.5){q=mat2(.8,-.6,.6,.8)*q;shape=exp(-q.x*q.x*3000.)*smoothstep(-.5,.3,q.y)*(1.-smoothstep(.3,.5,q.y));}
if(uKind>8.5)shape=exp(-pow((r-.65)*14.,2.))*.35;
vec2 uv=vSceneUv+(gl_PointCoord-.5)*vExtent;float inside=step(0.,uv.x)*step(0.,uv.y)*step(uv.x,1.)*step(uv.y,1.);float mask=texture(uWeatherMask,uv).r*inside;
frag=vec4(color,shape*vAlpha*mask);}`;

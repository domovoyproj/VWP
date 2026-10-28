import * as T from 'three';
import { Reflector } from './vendor/Reflector.js';

export function createRoad(scene, high, eco, fogColor) {
    const origin = { value: new T.Vector2() }, time = { value: 0 }, rainStrength={value:1};
    const material = new T.MeshStandardMaterial({ color: 0x24333d, roughness: .38, metalness: .2 });
    material.onBeforeCompile = shader => {
        shader.uniforms.worldOrigin=origin;shader.uniforms.time=time;shader.uniforms.rainStrength=rainStrength;
        shader.vertexShader=shader.vertexShader.replace('#include <common>','#include <common>\nuniform vec2 worldOrigin;varying vec3 roadWorld;');
        shader.vertexShader=shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\nroadWorld=(modelMatrix*vec4(position,1.)).xyz+vec3(worldOrigin.x,0.,worldOrigin.y);');
        shader.fragmentShader=shader.fragmentShader.replace('#include <common>',`#include <common>
            varying vec3 roadWorld;uniform float time;uniform float rainStrength;
            float roadHash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
            float roadNoise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);return mix(mix(roadHash(i),roadHash(i+vec2(1.,0.)),f.x),mix(roadHash(i+vec2(0.,1.)),roadHash(i+1.),f.x),f.y);}`);
        shader.fragmentShader=shader.fragmentShader.replace('#include <map_fragment>',`#include <map_fragment>
            vec2 street=mod(roadWorld.xz+20.,40.)-20.;
            float river=abs(mod(roadWorld.x+80.,160.)-80.);
            if(river<3.8&&abs(street.y)>4.12)discard;
            float asphalt=max(step(abs(street.x),4.),step(abs(street.y),4.));
            float grain=roadHash(floor(roadWorld.xz*180.));
            float erosion=roadNoise(roadWorld.xz*.6);
            diffuseColor.rgb=mix(vec3(.041,.057,.069),vec3(.012,.019,.027),asphalt)*( .86+grain*.12+erosion*.13);
            float line=step(abs(abs(street.x)-3.55),.025)*step(abs(street.y),16.);
            float dash=step(abs(street.x),.03)*step(mod(roadWorld.z,8.),3.);
            float crossing=step(abs(street.x),3.6)*step(abs(abs(street.y)-7.),1.1)*step(mod(roadWorld.x+10000.,1.15),.45);
            if(river<4.){line=0.;dash=0.;crossing=0.;}
            diffuseColor.rgb+=vec3(.24,.235,.20)*min(1.,line+dash+crossing)*(.65+erosion*.35);`);
        shader.fragmentShader=shader.fragmentShader.replace('#include <roughnessmap_fragment>',`#include <roughnessmap_fragment>
            float puddles=smoothstep(.48,.66,roadNoise(roadWorld.xz*.43)+roadNoise(roadWorld.xz*.13)*.12);
            roughnessFactor=mix(.67,.17,puddles)*(.97+roadHash(floor(roadWorld.xz*200.))*.03);`);
        // World-space rain rings perturb specular reflections instead of painting
        // bright circles over the pavement. The pattern survives origin rebasing.
        shader.fragmentShader=shader.fragmentShader.replace('#include <normal_fragment_maps>',`#include <normal_fragment_maps>
            vec2 surface=roadWorld.xz;
            vec2 slope=vec2(roadNoise(surface*38.),roadNoise(surface.yx*41.+7.))*.055-vec2(.0275);
            vec2 cell=floor(surface*1.3),local=fract(surface*1.3);
            for(int ring=0;ring<2;ring++){
                float seed=roadHash(cell+float(ring)*17.);
                vec2 center=vec2(roadHash(cell+3.+float(ring)),roadHash(cell+11.+float(ring)));
                vec2 delta=local-center;float radius=length(delta);
                float age=fract(time*.65+seed);float front=radius-age*.62;
                float envelope=exp(-front*front*280.)*sin(front*95.)*(1.-age);
                slope+=delta/max(radius,.025)*envelope*.026*rainStrength;
            }
            float wet=smoothstep(.48,.66,roadNoise(surface*.43)+roadNoise(surface*.13)*.12);
            normal=normalize(normal+mat3(viewMatrix)*vec3(-slope.x,0.,-slope.y)*mix(.35,1.,wet));`);
    };
    const ground=new T.Mesh(new T.PlaneGeometry(900,900),material);ground.rotation.x=-Math.PI/2;ground.position.y=.025;ground.receiveShadow=true;scene.add(ground);
    const waterUniforms={...Reflector.ReflectorShader.uniforms,textureMatrix:{value:new T.Matrix4()},worldOrigin:origin,time,fog:{value:new T.Color(fogColor)}};
    const vertexShader=`uniform mat4 textureMatrix;uniform vec2 worldOrigin;varying vec4 vUv;varying vec3 waterWorld;varying vec3 waterLocal;
        void main(){vUv=textureMatrix*vec4(position,1.);vec4 w=modelMatrix*vec4(position,1.);waterLocal=w.xyz;waterWorld=w.xyz+vec3(worldOrigin.x,0.,worldOrigin.y);gl_Position=projectionMatrix*viewMatrix*w;}`;
    const fragmentShader=`uniform sampler2D tDiffuse;uniform float time;uniform vec3 fog;varying vec4 vUv;varying vec3 waterWorld;varying vec3 waterLocal;
        void main(){float river=abs(mod(waterWorld.x+80.,160.)-80.);if(river>3.8)discard;
        vec2 uv=vUv.xy/vUv.w;
        vec2 wave=vec2(sin(waterWorld.z*2.7+time*1.2)+sin(waterWorld.z*5.1-waterWorld.x*3.+time*1.8)*.33,cos(waterWorld.x*4.-time*.8+waterWorld.z*.5))*.00075;
        vec3 reflected=texture2D(tDiffuse,uv+wave).rgb*.55;
        for(int i=1;i<4;i++){vec2 spread=vec2(float(i)*.00045,.0002*float(i));reflected+=(texture2D(tDiffuse,uv+wave+spread).rgb+texture2D(tDiffuse,uv+wave-spread).rgb)*.075;}
        float fresnel=.24+.76*pow(1.-abs(normalize(cameraPosition-waterLocal).y),2.);
        vec3 c=mix(vec3(.009,.025,.039),reflected,fresnel);
        c+=vec3(.011,.023,.029)*sin(waterWorld.z*4.+time)*sin(waterWorld.x*3.4-time);
        float haze=1.-exp(-length(cameraPosition-waterLocal)*.009);gl_FragColor=vec4(mix(c,fog,haze),1.);}`;
    let water;
    if(eco) {
        const fallback=new T.ShaderMaterial({uniforms:waterUniforms,vertexShader,fragmentShader:fragmentShader.replace('vec3 reflected=texture2D(tDiffuse,uv+wave).rgb*.55;','vec3 reflected=vec3(.025,.06,.078);').replace(/for\(int i=1;i<4;i\+\+\)\{[^}]+\}/,'')});
        water=new T.Mesh(new T.PlaneGeometry(900,900),fallback);
    }else water=new Reflector(new T.PlaneGeometry(900,900),{textureWidth:high?1024:512,textureHeight:high?1024:512,multisample:0,shader:{uniforms:waterUniforms,vertexShader,fragmentShader}});
    water.rotation.x=-Math.PI/2;water.position.y=-3.05;scene.add(water);
    return { ground, reflection:eco?null:water,
        update(seconds,camera,floatingOrigin,fog,raining=true){
            origin.value.set(floatingOrigin.x,floatingOrigin.z);time.value=seconds;
            rainStrength.value=raining?1:0;
            ground.position.x=water.position.x=camera.position.x;ground.position.z=water.position.z=camera.position.z;
            water.material.uniforms.worldOrigin.value.copy(origin.value);water.material.uniforms.time.value=seconds;water.material.uniforms.fog.value.setHex(fog);
        }
    };
}

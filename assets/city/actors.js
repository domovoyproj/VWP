import * as T from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { clone, retargetClip } from './vendor/SkeletonUtils.js';
import { mergeGeometries } from './vendor/BufferGeometryUtils.js';

const loader = new GLTFLoader();

function bodyOf(root) {
    let body;
    root.traverse(o => { if (o.isSkinnedMesh && /SuperHero|Superhero/.test(o.name)) body = o; });
    if (!body) throw new Error('Character is missing its deforming body mesh');
    return body;
}

// Both free packs use the same humanoid hierarchy, with UE/Godot bone names.
// Retarget world rotations, keeping the character's own proportions and bind pose.
function mappedBoneName(bone) {
    const common = { Head: 'DEF-head', neck_01: 'DEF-neck', pelvis: 'DEF-hips', root: 'root', spine_01: 'DEF-spine.001', spine_02: 'DEF-spine.002', spine_03: 'DEF-spine.003' };
    if (common[bone.name]) return common[bone.name];
    const match = bone.name.match(/^(clavicle|upperarm|lowerarm|hand|thigh|calf|foot|ball)_([lr])$/);
    if (match) return `DEF-${({ clavicle: 'shoulder', upperarm: 'upper_arm', lowerarm: 'forearm', hand: 'hand', thigh: 'thigh', calf: 'shin', foot: 'foot', ball: 'toe' })[match[1]]}.${match[2].toUpperCase()}`;
    const finger = bone.name.match(/^(index|middle|pinky|ring|thumb)_0([123])_([lr])$/);
    if (finger) return `DEF-${finger[1] === 'thumb' ? 'thumb' : 'f_' + finger[1]}.0${finger[2]}.${finger[3].toUpperCase()}`;
    return '';
}
function boneName(bone) { return T.PropertyBinding.sanitizeNodeName(mappedBoneName(bone)); }

function dress(body, shirtColor, trousersColor) {
    // Vertex positions are in the undeformed bind pose: the clothing regions
    // therefore stay attached to the skin through the full walking cycle.
    const g = body.geometry.clone(), p = g.attributes.position, data = new Float32Array(p.count);
    const maxY = Math.max(...Array.from({ length: p.count }, (_, i) => p.getY(i)));
    const maxZ = Math.max(...Array.from({ length: p.count }, (_, i) => p.getZ(i)));
    const up = maxY > maxZ ? 'getY' : 'getZ', height = Math.max(maxY, maxZ);
    for (let i = 0; i < p.count; i++) {
        const h = p[up](i) / height, x = Math.abs(p.getX(i));
        // Shirt sleeves stop before the hands; trousers and shoes cover the legs.
        data[i] = h < .07 ? 3 : h < .54 ? 2 : h < .82 && x < .58 ? 1 : 0;
    }
    g.setAttribute('garment', new T.BufferAttribute(data, 1)); body.geometry = g;
    const material = body.material.clone(); material.roughness = .82; material.metalness = 0;
    material.onBeforeCompile = shader => {
        shader.uniforms.shirt = { value: new T.Color(shirtColor) };
        shader.uniforms.trousers = { value: new T.Color(trousersColor) };
        shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nattribute float garment; varying float clothing;').replace('#include <begin_vertex>', '#include <begin_vertex>\nclothing=garment;');
        shader.fragmentShader = shader.fragmentShader.replace('#include <common>', '#include <common>\nuniform vec3 shirt;uniform vec3 trousers;varying float clothing;').replace('#include <map_fragment>', `#include <map_fragment>
            if(clothing>.2){vec3 textile=clothing>2.5?vec3(.025,.031,.043):clothing>1.5?trousers:shirt;
                float weave=.97+.03*sin(vMapUv.x*900.)*sin(vMapUv.y*900.);diffuseColor.rgb=textile*weave;}`);
    };
    material.customProgramCacheKey = () => 'afterlight-street-clothes-v1'; body.material = material;
    // A separate skinned cloth shell has a looser silhouette and its own normals,
    // rather than applying a shirt colour directly to anatomical skin shading.
    const cloth = g.clone(), indices = g.index.array, selected = [], neighbours = Array.from({ length: p.count }, () => new Set());
    for (let i = 0; i < indices.length; i += 3) {
        const a = indices[i], b = indices[i + 1], c = indices[i + 2];
        if (data[a] !== 1 || data[b] !== 1 || data[c] !== 1) continue;
        selected.push(a, b, c); neighbours[a].add(b).add(c); neighbours[b].add(a).add(c); neighbours[c].add(a).add(b);
    }
    cloth.setIndex(selected); const positions = cloth.attributes.position;
    for (let pass = 0; pass < 5; pass++) {
        const next = positions.array.slice();
        for (let i = 0; i < p.count; i++) if (neighbours[i].size) {
            const mean = new T.Vector3(); for (const j of neighbours[i]) mean.add(new T.Vector3().fromBufferAttribute(positions, j)); mean.divideScalar(neighbours[i].size);
            next[i * 3] = positions.getX(i) * .6 + mean.x * .4;
            next[i * 3 + 1] = positions.getY(i) * .6 + mean.y * .4;
            next[i * 3 + 2] = positions.getZ(i) * .6 + mean.z * .4;
        }
        positions.array.set(next);
    }
    cloth.computeVertexNormals(); const normals = cloth.attributes.normal;
    for (let i = 0; i < p.count; i++) if (data[i] === 1) {
        positions.setXYZ(i, positions.getX(i) + normals.getX(i) * .024, positions.getY(i) + normals.getY(i) * .024, positions.getZ(i) + normals.getZ(i) * .024);
    }
    positions.needsUpdate = true;
    const jacket = new T.SkinnedMesh(cloth, new T.MeshStandardMaterial({ color: shirtColor, roughness: .92, side: T.DoubleSide }));
    jacket.skeleton = body.skeleton; jacket.bindMatrix.copy(body.bindMatrix); jacket.bindMatrixInverse.copy(body.bindMatrixInverse);
    jacket.castShadow = jacket.receiveShadow = true; jacket.frustumCulled = false; body.parent.add(jacket);
}

function mergeCar(root) {
    root.updateMatrixWorld(true); const inverse = root.matrixWorld.clone().invert(), groups = new Map();
    root.traverse(o => {
        if (!o.isMesh) return;
        // Logos belong to their owners; keep them off this fictional city's cars.
        if (/Emblem|License Plate|^Interior|^Engine/.test(o.name)) return;
        let material = o.material;
        if (material.transmission) { material = material.clone(); material.transmission = 0; material.color.setHex(0x233d4b); material.metalness = .72; material.roughness = .15; }
        if (/Headlight/.test(o.name)) { material = material.clone(); material.emissive.setHex(0xffdcb0); material.emissiveIntensity = 3; }
        if (/Taillight/.test(o.name)) { material = material.clone(); material.emissive.setHex(0xe13047); material.emissiveIntensity = 2.6; }
        const geometry = o.geometry.clone().applyMatrix4(new T.Matrix4().multiplyMatrices(inverse, o.matrixWorld));
        if (!groups.has(material)) groups.set(material, []); groups.get(material).push(geometry);
    });
    const result = new T.Group();
    for (const [material, geometries] of groups) {
        const geometry = mergeGeometries(geometries); geometries.forEach(g => g.dispose());
        if (!geometry) throw new Error('Car material batch has incompatible geometry');
        const mesh = new T.Mesh(geometry, material); mesh.castShadow = false; mesh.receiveShadow = true; result.add(mesh);
    }
    const bounds = new T.Box3().setFromObject(result), size = bounds.getSize(new T.Vector3());
    // glTF is Y-up; retain the author's orientation and align the street direction.
    if (size.x > size.z) result.rotation.y = Math.PI / 2;
    const scale = 4.7 / Math.max(size.x, size.z); result.scale.setScalar(scale);
    result.position.y = -bounds.min.y * scale;
    return result;
}

export async function createActors(scene, eco, bake=false) {
    const [carFile, male, female, motion, buzzed, long] = await Promise.all([
        'car-road.glb', 'person-male.glb', 'person-female.glb', 'motion.glb', 'Hair_Buzzed.glb', 'Hair_Long.glb'
    ].map(name => !bake&&name==='motion.glb'?Promise.resolve(null):loader.loadAsync('./models/' + name)));
    const carTemplate = mergeCar(carFile.scene), vehicles = [];
    for (let i = 0; i < (eco ? 3 : 7); i++) {
        const car = carTemplate.clone(true), group = new T.Group(); group.add(car); scene.add(group);
        car.traverse(o => {
            if (!o.isMesh || !/Paint/.test(o.material.name)) return;
            o.material = o.material.clone(); o.material.color.setHex([0x485967, 0xbcbdb9, 0x672734, 0x225c65][i % 4]);
            o.material.clearcoat = 1; o.material.clearcoatRoughness = .13;
        });
        vehicles.push(group);
    }
    let source; motion?.scene.traverse(o => { if (o.isSkinnedMesh) source = o; });
    if (bake&&!source) throw new Error('Walking animation is missing its skeleton');
    // The export tool bakes retargeting once. Normal startup just parses these
    // compact tracks; it does not resample eight skeleton clips on the CPU.
    const baked=bake?{}:await fetch('./models/street-motion.json').then(r=>{if(!r.ok)throw new Error('Street animation library is missing');return r.json();});
    const pedestrians = [];
    const shadowCanvas = document.createElement('canvas'); shadowCanvas.width = shadowCanvas.height = 128;
    const shadowContext = shadowCanvas.getContext('2d'), gradient = shadowContext.createRadialGradient(64, 64, 0, 64, 64, 64);
    gradient.addColorStop(0, 'rgba(0,0,0,.65)'); gradient.addColorStop(.5, 'rgba(0,0,0,.22)'); gradient.addColorStop(1, 'rgba(0,0,0,0)');
    shadowContext.fillStyle = gradient; shadowContext.fillRect(0, 0, 128, 128);
    const shadowMaterial = new T.MeshBasicMaterial({ map: new T.CanvasTexture(shadowCanvas), transparent: true, depthWrite: false, toneMapped: false });
    // Moving actors use contact shadows; the cached building shadow map stays static.
    for (const car of vehicles) {
        const shadow = new T.Mesh(new T.PlaneGeometry(2.5, 5), shadowMaterial);
        shadow.rotation.x = -Math.PI / 2; shadow.position.y = .025; car.add(shadow);
    }
    for (const [gender, file] of [[0, male], [1, female]]) {
        const template = clone(file.scene), target = bodyOf(template);
        if(bake){source.skeleton.pose(); template.updateMatrixWorld(true); source.updateMatrixWorld(true);}
        const clips = bake?['Walk_Loop','Walk_Formal_Loop','Idle_Talking_Loop','Idle_Loop'].map(name => {
            source.skeleton.pose();target.skeleton.pose();template.updateMatrixWorld(true);source.updateMatrixWorld(true);
            return retargetClip(target, source, motion.animations.find(c => c.name === name), { getBoneName: boneName, hip: 'DEF-hips', fps: 30, hipInfluence: new T.Vector3(0, 1, 0) });
        }):baked[gender].map(c=>T.AnimationClip.parse(c));
        // Bind to named bones from the whole cloned hierarchy, including eye meshes.
        for (const clip of clips)for(const track of clip.tracks) track.name = track.name.replace(/^\.bones\[([^\]]+)\]/, '$1');
        if(bake)baked[gender]=clips.map(c=>T.AnimationClip.toJSON(c));
        for (let i = 0; i < (eco ? 3 : 7); i++) {
            const root = clone(file.scene), body = bodyOf(root), group = new T.Group(); group.add(root); scene.add(group);
            dress(body, [0x293541, 0x443243, 0x465550, 0x665458][(i + gender) % 4], [0x1d2934, 0x343843, 0x2e3340][i % 3]);
            root.traverse(o => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = true; } });
            // Hairstyles use bind-pose world coordinates; cancel the head transform
            // once before parenting them to the animated head bone.
            const head = root.getObjectByName('Head'), hair = (gender ? long : buzzed).scene.clone(true);
            root.updateMatrixWorld(true); const inverseHead = head.getWorldQuaternion(new T.Quaternion()).invert();
            hair.quaternion.copy(inverseHead); hair.position.copy(head.getWorldPosition(new T.Vector3()).negate().applyQuaternion(inverseHead)); head.add(hair);
            hair.traverse(o => { if (o.isMesh) { o.material = o.material.clone(); o.material.color.setHex(gender ? 0x30272c : 0x18212a); o.castShadow = false; } });
            root.scale.set(0.84, .93 + (i % 3) * .035, .92);
            root.updateMatrixWorld(true); root.traverse(o => {
                if (!o.isSkinnedMesh) return; o.skeleton.update(); o.computeBoundingSphere(); o.boundingSphere.radius += .4; o.frustumCulled = true;
            });
            const behaviour=i>=5?(i===5?'talk':'wait'):'walk';
            const mixer = new T.AnimationMixer(root), action = mixer.clipAction(clips[behaviour==='talk'?2:behaviour==='wait'?3:i%2]);action.play();
            const index = pedestrians.length;
            const shadow = new T.Mesh(new T.PlaneGeometry(1.2, 1.6), shadowMaterial); shadow.rotation.x = -Math.PI / 2; scene.add(shadow);
            pedestrians.push({ group, mixer, phase: i * .17, index, shadow, behaviour, gender, knee: root.getObjectByName('calf_l') });
        }
    }
    return {
        exportMotions(){return baked;},
        count: { vehicles: vehicles.length, pedestrians: pedestrians.length, walking:pedestrians.filter(a=>a.behaviour==='walk').length, talking:pedestrians.filter(a=>a.behaviour==='talk').length, waiting:pedestrians.filter(a=>a.behaviour==='wait').length },
        inspect() { return pedestrians[0].knee.quaternion.toArray(); },
        update(time, world, origin) {
            const nearestStreet=Math.round(world.x/40)*40;
            const isCanal=Math.abs(nearestStreet-Math.round(nearestStreet/160)*160)<1;
            const street = nearestStreet - origin.x;
            const trafficStreet=street+(isCanal?40:0);
            for (let i = 0; i < vehicles.length; i++) {
                const direction = i % 2 ? 1 : -1, car = vehicles[i];
                const z = Math.floor((world.z + 120) / 240) * 240 + ((time * (8 + i % 3) * direction + i * 37 + 100000) % 240) - 120 - origin.z;
                car.position.set(trafficStreet + direction * 2.3, .04, z); car.rotation.y = direction > 0 ? Math.PI : 0;
            }
            for (let i = 0; i < pedestrians.length; i++) {
                const actor = pedestrians[i], direction = i % 2 ? 1 : -1;
                const z = Math.floor((world.z + 80) / 160) * 160 + ((time * direction * 1.18 + i * 11.5 + 100000) % 160) - 80 - origin.z;
                const surfaceY = Math.abs(((z + origin.z + 20) % 40 + 40) % 40 - 20) < 4 ? .04 : .2;
                if(actor.behaviour==='walk') {
                    actor.group.position.set(street + direction * (5.35 + i % 3 * .25), surfaceY, z); actor.group.rotation.y = direction > 0 ? 0 : Math.PI;
                }else {
                    const blockZ=Math.round(world.z/40)*40;
                    // Two people face one another, while another pair wait by shops.
                    actor.group.position.set(street+5.8,.2,blockZ+11+(actor.behaviour==='talk'?actor.gender*1.35:5+actor.gender*2.2));
                    actor.group.rotation.y=actor.behaviour==='talk'?(actor.gender?Math.PI:0):-Math.PI/2;
                }
                actor.mixer.setTime(time * .8 + actor.phase);
                actor.shadow.position.copy(actor.group.position); actor.shadow.position.y += .015;
            }
        }
    };
}

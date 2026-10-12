import * as T from 'three';
import { mergeGeometries } from './vendor/BufferGeometryUtils.js';

// Geometry is authored in metres. Window recesses, floors, shop interiors and
// rooftop silhouettes remain real geometry when the viewer walks around them.
export async function createArchitecture(scene, renderer, seed, accent, treeMaterial, artwork) {
    const cube = new T.BoxGeometry(1, 1, 1);
    const cylinder = new T.CylinderGeometry(1, 1, 1, 12);
    const ball = new T.SphereGeometry(1, 16, 10);
    const plane = new T.PlaneGeometry(1, 1);
    const metal = new T.MeshStandardMaterial({ color: 0x53616d, metalness: .7, roughness: .36 });
    const dark = new T.MeshStandardMaterial({ color: 0x141d26, metalness: .28, roughness: .6 });
    const wood = new T.MeshStandardMaterial({ color: 0x29343b, roughness: .81 });
    const wall = new T.MeshStandardMaterial({ vertexColors: true, roughness: .88 });
    const textureLoader = new T.TextureLoader();
    const [concreteMap, concreteNormal, brickMap, brickNormal, storefrontAtlas, detailAtlas] = await Promise.all([
        'concrete_wall_007-color.jpg', 'concrete_wall_007-normal.jpg', 'brick_wall_003-color.jpg', 'brick_wall_003-normal.jpg', 'storefronts-v2.png', 'details-v3.png'
    ].map(name => textureLoader.loadAsync('./textures/' + name)));
    for (const texture of [concreteMap, concreteNormal, brickMap, brickNormal]) {
        texture.wrapS = texture.wrapT = T.RepeatWrapping;
        texture.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
    }
    concreteMap.colorSpace = brickMap.colorSpace = T.SRGBColorSpace;
    wall.map = concreteMap; wall.normalMap = concreteNormal; wall.normalScale.set(.45, .45);
    const brick = new T.MeshStandardMaterial({ vertexColors: true, map: brickMap, normalMap: brickNormal, normalScale: new T.Vector2(.65, .65), roughness: .86 });
    const glass = new T.MeshStandardMaterial({ color: 0x153846, metalness: .65, roughness: .17 });
    const room = new T.MeshStandardMaterial({ vertexColors: true, emissive: 0xffffff, emissiveIntensity: .55, roughness: .82 });
    const glow = new T.MeshStandardMaterial({ vertexColors: true, color: 0xffffff, emissive: 0xffffff, emissiveIntensity: 2.2 });
    for (const material of [room, glow]) material.onBeforeCompile = shader => {
        shader.fragmentShader = shader.fragmentShader.replace('#include <emissivemap_fragment>', '#include <emissivemap_fragment>\n#ifdef USE_COLOR\ntotalEmissiveRadiance *= vColor;\n#endif');
    };
    const windowArt = new T.MeshStandardMaterial({ map: artwork.apartment, emissiveMap: artwork.apartment, emissive: 0xffffff, emissiveIntensity: .68, roughness: .46 });
    const townhouseArt=new T.MeshStandardMaterial({map:artwork.townhouse,emissiveMap:artwork.townhouse,emissive:0xffffff,emissiveIntensity:.42,roughness:.79});
    const distantWall = new T.MeshStandardMaterial({ map: artwork.apartment, emissiveMap: artwork.apartment, emissive: 0xffffff, emissiveIntensity: .35, color: 0x97aabd, roughness: .85 });
    storefrontAtlas.colorSpace = T.SRGBColorSpace; storefrontAtlas.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
    const storefrontArt = new T.MeshStandardMaterial({ map: storefrontAtlas, emissiveMap: storefrontAtlas, emissive: 0xffffff, emissiveIntensity: .9, roughness: .52 });
    detailAtlas.colorSpace=T.SRGBColorSpace;detailAtlas.anisotropy=Math.min(8,renderer.capabilities.getMaxAnisotropy());
    const detailArt = new T.MeshStandardMaterial({ map:detailAtlas, emissiveMap:detailAtlas, emissive:0xffffff, emissiveIntensity:.45, roughness:.66 });
    // A shared atlas prevents each sign or building from adding a draw call.
    const atlas = document.createElement('canvas'); atlas.width = atlas.height = 2048;
    const ctx = atlas.getContext('2d');
    const labels = ['AFTERLIGHT', '珈琲 / COFFEE', '夜市', 'RAMEN • 24H', '星の街', '雨 / AME', 'ARCADE', '未来', 'NEON HOTEL', '喫茶', 'VWP ORIGINAL', '電気', 'TOKYO NIGHTS', '居酒屋', '旅館', 'OPEN / 24'];
    const signColors = ['#d3a57c', '#d7e5df', '#e67a88', '#d6bf93', '#6aa9b8', '#cd8ca4'];
    for (let i = 0; i < 16; i++) {
        const x = (i % 4) * 512, y = Math.floor(i / 4) * 512;
        ctx.fillStyle = '#101820'; ctx.fillRect(x, y, 512, 512);
        ctx.strokeStyle = signColors[i % signColors.length]; ctx.lineWidth = 5;
        ctx.strokeRect(x + 14, y + 110, 484, 292);
        ctx.fillStyle = ctx.strokeStyle; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.font = `${labels[i].length > 9 ? 42 : 64}px 'Yu Gothic',Arial`;
        ctx.fillText(labels[i], x + 256, y + 249);
        ctx.font = '17px Arial'; ctx.fillStyle = '#90a4ad';
        ctx.fillText(`${String(i + 1).padStart(2, '0')}  /  AFTERLIGHT DISTRICT  /  EST. 2026`, x + 256, y + 340);
    }
    const map = new T.CanvasTexture(atlas); map.colorSpace = T.SRGBColorSpace;
    map.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
    const signage = new T.MeshStandardMaterial({ map, emissiveMap: map, emissive: 0xffffff, emissiveIntensity: 1.6, roughness: .4 });
    const blades = document.createElement('canvas'); blades.width = blades.height = 1024;
    const bladeContext = blades.getContext('2d'), bladeLabels = ['夜市', '珈琲', '星街', '未来', '喫茶', 'ゲーム', '旅館', '本屋'];
    for (let i = 0; i < 8; i++) {
        const x = i % 4 * 256, y = Math.floor(i / 4) * 512;
        bladeContext.fillStyle = i % 3 === 0 ? '#28121d' : '#0e1b29'; bladeContext.fillRect(x, y, 256, 512);
        bladeContext.strokeStyle = ['#ff688c', '#83d4de', '#ddb9ee', '#e8b681'][i % 4]; bladeContext.lineWidth = 7;
        bladeContext.strokeRect(x + 12, y + 12, 232, 488); bladeContext.fillStyle = bladeContext.strokeStyle;
        bladeContext.font = "86px 'Yu Gothic',Arial"; bladeContext.textAlign = 'center'; bladeContext.textBaseline = 'middle';
        const letters = [...bladeLabels[i]]; letters.forEach((ch, j) => bladeContext.fillText(ch, x + 128, y + 256 + (j - (letters.length - 1) / 2) * 116));
    }
    const bladeMap = new T.CanvasTexture(blades); bladeMap.colorSpace = T.SRGBColorSpace;
    const bladeMaterial = new T.MeshStandardMaterial({ map: bladeMap, emissiveMap: bladeMap, emissive: 0xffffff, emissiveIntensity: 2.5, roughness: .45 });
    const sidewalkCanvas = document.createElement('canvas'); sidewalkCanvas.width = sidewalkCanvas.height = 512;
    const paving = sidewalkCanvas.getContext('2d'), rnd = seed(71, 23);
    paving.fillStyle = '#41494f'; paving.fillRect(0, 0, 512, 512);
    for (let y = 0; y < 512; y += 32) for (let x = -32; x < 512; x += 64) {
        const c = 58 + Math.floor(rnd() * 19); paving.fillStyle = `rgb(${c},${c + 6},${c + 9})`;
        paving.fillRect(x + (y % 64 ? 32 : 0) + 1, y + 1, 62, 30);
    }
    const pavementMap = new T.CanvasTexture(sidewalkCanvas); pavementMap.colorSpace = T.SRGBColorSpace;
    pavementMap.wrapS = pavementMap.wrapT = T.RepeatWrapping; pavementMap.repeat.set(8, 8);
    const pavement = new T.MeshStandardMaterial({ map: pavementMap, roughness: .76, metalness: .12 });
    const colors = [0x889499, 0x927d72, 0x666e79, 0xa9a18d, 0x576d75, 0x8c8586];
    const tmpColor = new T.Color();

    function add(parent, geometry, material, x, y, z, sx = 1, sy = 1, sz = 1, color) {
        let g = geometry;
        if (material.vertexColors) {
            g = geometry.clone(); const count = g.attributes.position.count;
            const data = new Float32Array(count * 3); tmpColor.setHex(color ?? 0xffffff);
            for (let i = 0; i < count; i++) tmpColor.toArray(data, i * 3);
            g.setAttribute('color', new T.BufferAttribute(data, 3));
        }
        if (material === wall || material === brick) {
            const p = g.attributes.position, n = g.attributes.normal, uv = g.attributes.uv;
            for (let i = 0; i < p.count; i++) {
                const onX = Math.abs(n.getX(i)) > .5, onY = Math.abs(n.getY(i)) > .5;
                uv.setXY(i, (onX ? p.getZ(i) * sz : p.getX(i) * sx) / 3, (onY ? p.getZ(i) * sz : p.getY(i) * sy) / 3);
            }
        }
        const m = new T.Mesh(g, material); m.position.set(x, y, z); m.scale.set(sx, sy, sz);
        m.castShadow = m.receiveShadow = material !== glow && material !== treeMaterial;
        parent.add(m); return m;
    }
    function panel(parent, width, height, x, y, z, index) {
        const g = plane.clone(), uv = g.attributes.uv;
        for (let i = 0; i < uv.count; i++) uv.setXY(i, ((index % 4) + .025 + uv.getX(i) * .95) / 4, (3 - Math.floor(index / 4) + .21 + uv.getY(i) * .59) / 4);
        return add(parent, g, signage, x, y, z, width, height);
    }
    function bladePanel(parent, z, index) {
        const g = plane.clone(), uv = g.attributes.uv;
        for (let i = 0; i < uv.count; i++) uv.setXY(i, (index % 4 + .02 + uv.getX(i) * .96) / 4, (1 - Math.floor(index / 4) + .02 + uv.getY(i) * .96) / 2);
        return add(parent, g, bladeMaterial, 0, 0, z, 2.25, 4.3);
    }
    function detailPanel(parent, width, height, x, y, z, index) {
        const g=plane.clone(),uv=g.attributes.uv;
        for(let i=0;i<uv.count;i++)uv.setXY(i,(index%3+.008+uv.getX(i)*.984)/3,(1-Math.floor(index/3)+.008+uv.getY(i)*.984)/2);
        return add(parent,g,detailArt,x,y,z,width,height);
    }
    function batch(group) {
        group.updateMatrixWorld(true); const inverse = group.matrixWorld.clone().invert(), batches = new Map();
        group.traverse(o => {
            if (!o.isMesh) return;
            const geometry = o.geometry.clone().applyMatrix4(new T.Matrix4().multiplyMatrices(inverse, o.matrixWorld));
            if (!batches.has(o.material)) batches.set(o.material, []);
            batches.get(o.material).push(geometry);
            if (![cube, cylinder, ball, plane].includes(o.geometry)) o.geometry.dispose();
        });
        group.clear();
        for (const [material, geometries] of batches) {
            // Extruded arches and hulls are non-indexed; boxes are indexed.
            // Normalise only mixed batches before joining their shared material.
            const mixed=geometries.some(g=>g.index)&&geometries.some(g=>!g.index);
            const inputs=mixed?geometries.map(g=>g.index?g.toNonIndexed():g):geometries;
            const geometry = mergeGeometries(inputs);
            if(mixed)for(let i=0;i<inputs.length;i++)if(inputs[i]!==geometries[i])inputs[i].dispose();
            for (const g of geometries) g.dispose();
            if (!geometry) throw new Error('Architecture batch has incompatible attributes');
            const joined = new T.Mesh(geometry, material);
            joined.castShadow = joined.receiveShadow = material !== glow && material !== treeMaterial;
            group.add(joined);
        }
    }
    function facade(building, width, height, depth, style, r) {
        const facade = new T.Group(); building.add(facade); facade.position.z = depth / 2 + .025;
        const shopIndex = Math.floor(r() * 8);
        const floors = style===0?0:Math.floor((height - 4.4) / 3.1), bays = Math.max(2, Math.floor(width / 2.6));
        const pitch = width / bays;
        if(style===0){
            const face=plane.clone(),uv=face.attributes.uv;
            for(let i=0;i<uv.count;i++)uv.setXY(i,uv.getX(i),.385+uv.getY(i)*.585);
            add(facade,face,townhouseArt,0,4+(height-4)/2,.045,width-.2,height-4);
            // Real eaves and balconies project in front of the traditional art.
            for(let y=4.6;y<height;y+=3.1){
                add(facade,cube,wood,0,y,.22,width+.3,.1,.65);
                add(facade,cube,metal,0,y+.62,.62,width-.5,.035,.035);
                for(let x=-width/2+.4;x<width/2;x+=.32)add(facade,cube,dark,x,y+.32,.62,.028,.62,.028);
            }
        }
        for (let floor = 0; floor < floors; floor++) {
            const y = 5.9 + floor * 3.1;
            // Recessed dark frame -> lit interior -> mullions and projecting sill.
            for (let bay = 0; bay < bays; bay++) {
                const x = (bay + .5) * pitch - width / 2, w = Math.min(1.7, pitch - .55);
                add(facade, cube, dark, x, y, .05, w + .2, 1.95, .2);
                const interior = plane.clone(), uv = interior.attributes.uv;
                const column = Math.floor(r() * 5), row = Math.floor(r() * 7);
                for (let i = 0; i < uv.count; i++) uv.setXY(i, (column + .15 + uv.getX(i) * .69) / 5, (row + .28 + uv.getY(i) * .6) / 7);
                add(facade, interior, windowArt, x, y, .165, w, 1.76);
                add(facade, cube, dark, x - w * .23, y, .185, .025, 1.8, .04);
                add(facade, cube, metal, x, y - .99, .22, w + .26, .12, .4);
                if (style === 1 && floor % 2 === 0) {
                    add(facade, cube, wall, x, y - 1.12, .5, pitch - .2, .15, .85, 0x65757e);
                    add(facade, cube, metal, x, y - .65, .87, pitch - .2, .07, .065);
                    for (let k = -2; k <= 2; k++) add(facade, cube, metal, x + k * (pitch - .3) / 4, y - .9, .87, .035, .55, .035);
                }
            }
            if (style === 0) add(facade, cube, wood, 0, y - 1.4, .13, width, .2, .25);
            else add(facade, cube, metal, 0, y + 1.42, .02, width, .095, .11);
        }
        if (style === 0) for (let i = 0; i <= bays; i++) add(facade, cube, wood, i * pitch - width / 2, height / 2 + 2, .12, .18, height - 4, .23);
        // Glass shop doors with visible counters, shelving and warm ceilings.
        // The ground floor is hollow. The rear artwork sits inside a real alcove,
        // behind counter furniture, lintels and a doorway one metre in front.
        add(facade, cube, dark, 0, 2.1, -.96, width - .45, 3.8, .1);
        const shop = plane.clone(), shopUV = shop.attributes.uv;
        for (let i = 0; i < shopUV.count; i++) shopUV.setXY(i, (shopIndex % 2 + .012 + shopUV.getX(i) * .976) / 2, (3 - Math.floor(shopIndex / 2) + .012 + shopUV.getY(i) * .976) / 4);
        add(facade, shop, storefrontArt, 0, 2.1, -.89, width - .65, 3.55);
        add(facade,cube,wood,0,.68,-.28,width*.75,.12,.65);
        add(facade,cube,dark,0,.4,-.32,width*.75,.5,.35);
        add(facade,cube,room,0,3.68,-.3,width-.8,.055,1.1,0xd3af7d);
        for(const side of [-1,1])add(facade,cube,style===0?wood:wall,side*(width/2-.18),2.1,-.45,.32,3.8,1.15,0x52616a);
        const doorX=width*.25;
        add(facade,cube,dark,doorX,1.85,.18,1.1,3.3,.08);
        detailPanel(facade,.98,1.6,doorX,2.5,.23,shopIndex%2?3:4);
        add(facade,cube,metal,doorX+.38,1.48,.26,.035,.42,.055);
        detailPanel(facade,1.1,1.45,-width*.28,1.3,.38,0);
        if(shopIndex%3===0)for(let i=0;i<3;i++){
            add(facade,cylinder,wood,-width*.15+i*.85,.62,.67,.28,.09,.28);
            add(facade,cylinder,metal,-width*.15+i*.85,.35,.67,.035,.55,.035);
        }
        for (let i = 0; i < bays; i++) {
            const x = (i + .5) * pitch - width / 2;
            add(facade, cube, dark, x, 2.15, .3, .09, 3.7, .14);
            add(facade, cube, metal, x + pitch * .43, 1.9, .32, .025, .35, .04);
        }
        add(facade, cube, wood, 0, 4.15, .3, width + .2, .5, .65);
        panel(facade, width * .78, 1.0, 0, 4.2, .66, Math.floor(r() * labels.length));
        const awning = add(facade, cube, dark, 0, 3.88, 1.05, width + .55, .12, 1.6); awning.rotation.x = -.08;
        add(facade, cube, glow, 0, 3.72, 1.75, width - .5, .035, .035, r() > .5 ? 0x7ebcc4 : 0xc6809b);
        // Projecting double-sided blade signs and support brackets.
        if (r() > .25) {
            const blade = new T.Group(); facade.add(blade); blade.position.set(width / 2 - .4, 8.2, 1.3); blade.rotation.y = Math.PI / 2;
            add(blade, cube, dark, 0, 0, 0, 2.4, 4.5, .16);
            const index = Math.floor(r() * 8);
            bladePanel(blade, .09, index);
            bladePanel(blade, -.09, index).rotation.y = Math.PI;
            add(facade, cube, metal, width / 2 - .4, 10.5, .7, .12, .12, 1.5);
        }
        if (style === 0) for (const x of [-width * .35, width * .35]) {
            add(facade, cylinder, glow, x, 3.1, 1.1, .2, .65, .2, 0xd65e57);
            add(facade, cylinder, dark, x, 3.44, 1.1, .21, .045, .21);
            add(facade, cylinder, dark, x, 2.75, 1.1, .16, .045, .16);
            add(facade, cube, metal, x, 3.7, 1.1, .025, .4, .025);
        }
        return facade;
    }

    function building(parent, x, z, width, depth, height, style, r, detailed) {
        const b = new T.Group(); b.position.set(x, .22, z); parent.add(b);
        const color = colors[Math.floor(r() * colors.length)];
        const shell = add(b, cube, detailed ? style === 0 ? brick : wall : distantWall, 0, detailed?(height+4)/2:height/2, 0, width, detailed?height-4:height, depth, color);
        if(detailed){
            add(b,cube,dark,0,3.9,0,width,.18,depth);
            for(const px of [-width/2+.18,width/2-.18])for(const pz of [-depth/2+.18,depth/2-.18])add(b,cube,style===0?brick:wall,px,2,pz,.36,4,.36,color);
        }
        if (!detailed) {
            shell.geometry = cube.clone(); const uv = shell.geometry.attributes.uv;
            for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * width / 12, uv.getY(i) * height / 22);
        }
        add(b, cube, dark, 0, .22, 0, width + .18, .4, depth + .18);
        for (let side = 0; side < 4; side++) {
            const faceRandom = seed(Math.floor(width * 100) + side * 71, Math.floor(depth * 100 + height * 100));
            let f;
            if (detailed) f = facade(b, side % 2 ? depth : width, height, side % 2 ? width : depth, style, faceRandom);
            else {
                f = new T.Group(); b.add(f); const g = plane.clone(), uv = g.attributes.uv, index = Math.floor(faceRandom() * 8);
                for (let i = 0; i < uv.count; i++) uv.setXY(i, (index % 2 + .012 + uv.getX(i) * .976) / 2, (3 - Math.floor(index / 2) + .012 + uv.getY(i) * .976) / 4);
                add(f, g, storefrontArt, 0, 2.1, .23, (side % 2 ? depth : width) - .65, 3.55);
            }
            f.rotation.y = side * Math.PI / 2;
            f.position.set(side === 1 ? width / 2 : side === 3 ? -width / 2 : 0, 0, side === 0 ? depth / 2 : side === 2 ? -depth / 2 : 0);
        }
        add(b, cube, dark, 0, height + .08, 0, width + .55, .2, depth + .55);
        // A flat accessible roof for midrises; tiled eaves and a ridge on small shops.
        if (style === 0) {
            for (const side of [-1, 1]) {
                const roof = add(b, cube, metal, 0, height + 1.25, side * depth / 4, width + 1.2, .16, depth * .54); roof.rotation.x = side * .18;
                for (let t = -width / 2; detailed && t < width / 2; t += .5) {
                    const tile = add(b, cylinder, dark, t, height + 1.3, side * depth / 4, .07, depth * .54, .07);
                    tile.rotation.x = Math.PI / 2 + side * .18;
                }
            }
            add(b, cube, metal, 0, height + 1.96, 0, width + 1.2, .18, .3);
        } else {
            for (const side of [-1, 1]) {
                add(b, cube, wall, side * width / 2, height + .5, 0, .2, .9, depth, color);
                add(b, cube, wall, 0, height + .5, side * depth / 2, width, .9, .2, color);
            }
            for (let i = 0; i < 3; i++) {
                add(b, cube, metal, -width * .25 + i * 2, height + .7, 0, 1.4, 1.1, 2);
                add(b, cylinder, dark, -width * .25 + i * 2, height + 1.28, 0, .42, .04, .42);
            }
            add(b, cube, wall, width * .15, height + 1.5, depth * .2, 3.6, 3, 3.8, 0x68757e);
            add(b, cylinder, metal, -width * .3, height + 3, depth * .22, .045, 6, .045);
            add(b, ball, glow, -width * .3, height + 6, depth * .22, .055, .055, .055, 0xda6968);
        }
        // Air conditioning units and individual drain pipes on the side wall.
        add(b, cylinder, metal, width / 2 + .12, height / 2, -depth * .32, .045, height, .045);
        for (let y = 6.5; detailed && y < Math.min(30, height); y += 6.2) {
            add(b, cube, metal, width / 2 + .34, y, depth * .22, .6, .8, 1.25);
            add(b, cylinder, dark, width / 2 + .67, y, depth * .22, .3, .025, .3).rotation.z = Math.PI / 2;
        }
        return b;
    }

    function buildTemplate(ix, iz, detailed) {
        const r = seed(ix, iz), group = new T.Group(); group.userData = { ix, iz };
        add(group, cube, pavement, 0, .1, 0, 32, .2, 32);
        // Four independently proportioned buildings with narrow internal alleys.
        let house = 0;
        for (const x of [-7.65, 7.65]) for (const z of [-7.65, 7.65]) {
            const houseRandom = seed(ix * 7 + house, iz * 11 + house * 2); house++;
            const n = houseRandom(), style = n < .46 ? 0 : n < .86 ? 1 : 2;
            const width = 9.8 + houseRandom() * 3.8, depth = 10.2 + houseRandom() * 3.4;
            const height = style === 0 ? 7.8 + Math.floor(houseRandom() * 3) * 3.1 : style === 1 ? 17 + Math.floor(houseRandom() * 8) * 3.1 : 43 + Math.floor(houseRandom() * 13) * 3.1;
            building(group, x, z, width, depth, height, style, houseRandom, detailed);
        }
        for (const x of [-15.1, 15.1]) for (const z of [-13.5, 13.5]) {
            add(group, cylinder, metal, x, 3.4, z, .08, 6.8, .08);
            add(group, cube, metal, x - Math.sign(x) * .55, 6.75, z, 1.3, .13, .3);
            add(group, cube, glow, x - Math.sign(x) * .55, 6.66, z, 1.1, .045, .22, 0xf6d6a8);
        }
        for (const x of [-15.3, 15.3]) {
            add(group, cube, metal, x, .55, 3.5, 1.25, .7, 2);
            // Keep the existing original cherry-tree artwork as vegetation only.
            add(group, new T.PlaneGeometry(5, 7), treeMaterial, x, 3.8, 3.5);
            // Textured vending cabinet with separate button panel and retrieval slot.
            add(group, cube, dark, x, 1.2, -4, .7, 2, 1.35);
            const machine = new T.Group(); group.add(machine); machine.position.set(x + Math.sign(x) * .36, 1.2, -4); machine.rotation.y = Math.sign(x) * Math.PI / 2;
            detailPanel(machine,1.25,1.95,0,0,.015,1);
            add(machine,cube,metal,.48,.07,.05,.06,.45,.055);
            add(machine,cube,dark,0,-.72,.045,.77,.15,.04);
            if(detailed){
                // Street furniture gives the near district a human scale.
                add(group,cube,wood,x,.63,8, .65,.12,2.2);
                add(group,cube,wood,x+Math.sign(x)*.26,.98,8,.08,.65,2.2);
                for(const z of [7.2,8.8])add(group,cube,metal,x,.35,z,.07,.55,.08);
                for(let z=10;z<13;z+=.65)add(group,cylinder,metal,x,.9,z,.025,1.4,.025);
                add(group,cube,dark,x+Math.sign(x)*.42,1.4,11.4,.75,.95,.12);
            }
        }
        batch(group); return group;
    }
    // Keep a bounded library of GPU meshes. Recycling blocks reuses buffers
    // instead of rebuilding thousands of window meshes while the camera moves.
    const templates = new Map();
    function block(ix, iz, detailed) {
        const variant = ((Math.imul(ix, 83) ^ Math.imul(iz, 131)) >>> 0) % 24, key = variant + (detailed ? ':near' : ':far');
        if (!templates.has(key)) templates.set(key, buildTemplate(variant, variant * 13 + 17, detailed));
        const group = templates.get(key).clone(true); group.userData = { ix, iz, template: key, detailed };
        return group;
    }
    function release(group) { group.clear(); }

    // Distant silhouettes continue well beyond the detailed, bounded chunks.
    // They move in the same floating-origin coordinate frame as the near city.
    const skyline = new T.Group(); scene.add(skyline);
    const skylineMaterial = new T.ShaderMaterial({
        uniforms: { fog: { value: new T.Color(0x101c2b) }, image:{value:artwork.apartment} },
        vertexShader: `attribute vec3 color; varying vec3 c; varying vec3 w; varying vec3 n;varying vec2 facadeUV;void main(){c=color;n=normal;facadeUV=uv;vec4 p=modelMatrix*vec4(position,1.);w=p.xyz;gl_Position=projectionMatrix*viewMatrix*p;}`,
        fragmentShader: `uniform vec3 fog;uniform sampler2D image;varying vec3 c;varying vec3 w;varying vec3 n;varying vec2 facadeUV;void main(){vec3 painted=pow(texture2D(image,facadeUV).rgb,vec3(2.2));vec3 v=mix(painted*.7,c*.022,step(.5,abs(n.y)));float mist=1.-exp(-length(cameraPosition-w)*.007);gl_FragColor=vec4(mix(v,fog,mist),1.);}`
    });
    function updateSkyline(cx, cz, origin) {
        if (skyline.userData.cx === cx && skyline.userData.cz === cz && skyline.userData.ox === origin.x && skyline.userData.oz === origin.z) return;
        skyline.traverse(o => { if (o.geometry) o.geometry.dispose(); }); skyline.clear();
        const geometry = [];
        for (let dz = -10; dz <= 10; dz++) for (let dx = -10; dx <= 10; dx++) {
            if (Math.abs(dx) <= 3 && Math.abs(dz) <= 3) continue;
            const r = seed(cx + dx, cz + dz);
            for (let n = 0; n < 2; n++) {
                const g = cube.clone(); const height = 25 + Math.pow(r(), 2) * 145;
                const width=10+r()*8,depth=12+r()*7;
                const uv=g.attributes.uv;for(let i=0;i<uv.count;i++)uv.setXY(i,uv.getX(i)*width/12,uv.getY(i)*height/22);
                const matrix = new T.Matrix4().compose(new T.Vector3((cx + dx + .3 + n * .4) * 40 - origin.x, height / 2, (cz + dz + .4) * 40 - origin.z), new T.Quaternion(), new T.Vector3(width, height, depth));
                g.applyMatrix4(matrix); const c = new T.Color(colors[Math.floor(r() * colors.length)]), data = new Float32Array(g.attributes.position.count * 3);
                for (let i = 0; i < g.attributes.position.count; i++) c.toArray(data, i * 3);
                g.setAttribute('color', new T.BufferAttribute(data, 3)); geometry.push(g);
            }
        }
        skyline.add(new T.Mesh(mergeGeometries(geometry), skylineMaterial)); geometry.forEach(g => g.dispose());
        skyline.userData = { cx, cz, ox: origin.x, oz: origin.z };
    }
    return { block, release, updateSkyline, batch, pavement, metal, dark, glass, room, glow, cube, cylinder, add, detailPanel,
        metrics() { return { templates: templates.size, maxTemplates: 48 }; },
        recolor(fog) { skylineMaterial.uniforms.fog.value.setHex(fog); } };
}

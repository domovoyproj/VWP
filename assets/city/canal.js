import * as T from 'three';

// A continuous district uses one batched template, rebased with the city.
// Water is rendered by the shared reflection surface in city.js.
export function createCanal(scene, architecture) {
    const { add, cube, cylinder, metal, dark, glow, pavement, batch } = architecture;
    const stone = new T.MeshStandardMaterial({ color: 0x52606b, map: pavement.map, roughness: .86 });
    const warm = new T.MeshStandardMaterial({ color: 0xf5c896, emissive: 0xf5c896, emissiveIntensity: 1.2, roughness: .5 });
    const wood = new T.MeshStandardMaterial({ color: 0x45322d, roughness: .83 });
    const template = new T.Group();
    for (const side of [-1, 1]) {
        add(template, cube, stone, side * 4.12, -1.45, 0, .65, 3.4, 400);
        add(template, cube, stone, side * 4.12, .26, 0, .88, .16, 400);
        // Railings are interrupted at road bridges and pedestrian crossings.
        for (let z = -196; z <= 196; z += 2) {
            if (Math.abs(((z + 20) % 40 + 40) % 40 - 20) < 4.6) continue;
            add(template, cylinder, metal, side * 4.18, .84, z, .025, 1.05, .025);
            add(template, cube, metal, side * 4.18, 1.37, z, .07, .07, 2.03);
            add(template, cube, metal, side * 4.18, .63, z, .03, .035, 2.03);
        }
        for (let z = -190; z <= 190; z += 20) {
            add(template, cylinder, dark, side * 4.75, 1.9, z, .055, 3.45, .055);
            add(template, cube, dark, side * 4.75, 3.62, z, .38, .62, .38);
            for (const face of [-1, 1]) {
                add(template, cube, warm, side * 4.75 + face * .196, 3.63, z, .012, .46, .27);
                add(template, cube, warm, side * 4.75, 3.63, z + face * .196, .27, .46, .012);
            }
            add(template, cube, dark, side * 4.75, 3.97, z, .5, .1, .5);
            // Warm light cast on the stone is authored in shared vertex colours.
            add(template, cube, glow, side * 4.13, -.15, z, .02, .09, .28, 0xb78a60);
        }
    }
    const bridgeShape = new T.Shape();
    bridgeShape.moveTo(-4.2, -.68);bridgeShape.lineTo(-4.2, .1);bridgeShape.lineTo(4.2, .1);bridgeShape.lineTo(4.2, -.68);
    bridgeShape.lineTo(3.4, -.68);bridgeShape.quadraticCurveTo(0, .02, -3.4, -.68);bridgeShape.lineTo(-4.2, -.68);
    const bridgeArch = new T.ExtrudeGeometry(bridgeShape, { depth: .35, bevelEnabled: true, bevelSegments: 2, bevelSize: .04, bevelThickness: .04, curveSegments: 20 });
    for (let z = -200; z <= 200; z += 40) {
        add(template, cube, stone, 0, -.25, z, 8.5, .45, 8.2);
        for (const side of [-1, 1]) {
            add(template, bridgeArch, stone, 0, -.06, z + side * 4.2);
            add(template, cube, stone, 0, .35, z + side * 4.2, 8.7, .35, .38);
            add(template, cube, metal, 0, 1.34, z + side * 4.2, 8.7, .07, .06);
            for (let x = -4; x <= 4; x += .5) add(template, cube, metal, x, .94, z + side * 4.2, .035, .8, .035);
            for (const x of [-4.4, 4.4]) {
                add(template, cube, stone, x, .73, z + side * 4.2, .52, 1.35, .52);
                add(template, cube, warm, x, 1.5, z + side * 4.2, .23, .18, .23);
            }
        }
    }
    batch(template);
    const channels = [-160, 0, 160].map(offset => { const group = template.clone(true);group.userData.offset = offset;scene.add(group);return group; });

    // Two small river boats, with hulls, cabin frames, navigation lamps and wake.
    const boats = [];
    const hullShape = new T.Shape();hullShape.moveTo(-.95, -3);hullShape.lineTo(.95, -3);hullShape.lineTo(.95, 2.3);hullShape.quadraticCurveTo(0, 3.6, -.95, 2.3);hullShape.lineTo(-.95, -3);
    const hullGeometry = new T.ExtrudeGeometry(hullShape, { depth: .45, bevelEnabled: true, bevelSize: .15, bevelThickness: .1, bevelSegments: 3 });hullGeometry.rotateX(Math.PI/2);
    for (let i = 0; i < 2; i++) {
        const boat = new T.Group();
        add(boat, hullGeometry, dark, 0, .25, 0);
        add(boat, cube, wood, 0, .2, -.3, 1.6, .1, 4.8);
        add(boat, cube, dark, 0, .85, -.4, 1.6, 1.4, 2.4);
        for (const side of [-1, 1]) for (let z = -1.1; z < .6; z += .65) {
            add(boat, cube, warm, side * .81, .94, z, .015, .65, .54);
            add(boat, cube, metal, side * .83, .94, z - .28, .02, .85, .025);
        }
        add(boat, cube, metal, 0, 1.61, -.4, 1.85, .14, 2.7);
        add(boat, cylinder, metal, 0, 1.85, -1.35, .025, .4, .025);
        add(boat, cube, glow, -.68, .5, 1.6, .09, .07, .09, 0xc9404c);
        add(boat, cube, glow, .68, .5, 1.6, .09, .07, .09, 0x76b6a1);
        batch(boat);boat.traverse(o => { if(o.isMesh)o.castShadow=false; });scene.add(boat);boats.push(boat);
    }
    return {
        update(time, world, origin) {
            const centerX = Math.round(world.x/160)*160, centerZ = Math.round(world.z/40)*40;
            for (const channel of channels) channel.position.set(centerX + channel.userData.offset-origin.x, 0, centerZ-origin.z);
            for (let i=0;i<boats.length;i++) {
                const direction=i?-1:1,z=Math.floor((world.z+120)/240)*240+((time*3.4*direction+i*117+100000)%240)-120;
                boats[i].position.set(centerX+(i?1.7:-1.7)-origin.x, -2.9+Math.sin(time*1.2+i)*.025, z-origin.z);
                boats[i].rotation.set(Math.sin(time*.8+i)*.009, i?Math.PI:0, Math.sin(time*.65+i)*.008);
            }
        },
        metrics() { return { channels: channels.length, boats: boats.length }; }
    };
}

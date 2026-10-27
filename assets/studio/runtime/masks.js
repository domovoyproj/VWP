import {image} from './gl.js';
export const channels=['water','clouds','wind','light'];
export class Masks{
  constructor(gl,width=1024,height=576){
    this.gl=gl;this.width=width;this.height=height;this.undo=[];this.redo=[];
    this.canvases=channels.map(()=>{const canvas=document.createElement('canvas');canvas.width=width;canvas.height=height;const context=canvas.getContext('2d',{willReadFrequently:true});context.fillStyle='black';context.fillRect(0,0,width,height);return canvas});
    this.texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,this.texture);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
  }
  async load(layer,base){
    for(let channel=0;channel<4;channel++){
      const canvas=this.canvases[channel],ctx=canvas.getContext('2d');
      if(layer.maskFiles?.[channels[channel]]){const source=await image(new URL(layer.maskFiles[channels[channel]],base));ctx.drawImage(source,0,0,this.width,this.height);continue}
      for(const shape of layer.masks||[]){if(shape.channel!==channel)continue;ctx.save();ctx.filter=`blur(${shape.feather*this.height}px)`;ctx.fillStyle='white';ctx.beginPath();shape.points.forEach(([x,y],i)=>i?ctx.lineTo(x*this.width,y*this.height):ctx.moveTo(x*this.width,y*this.height));ctx.closePath();ctx.fill();ctx.restore()}
    }
    this.upload();
  }
  upload(selected=-1){
    const count=this.width*this.height,bytes=this.bytes||(this.bytes=new Uint8Array(count*4));
    for(let channel=0;channel<4;channel++){if(selected>=0&&selected!==channel)continue;const data=this.canvases[channel].getContext('2d').getImageData(0,0,this.width,this.height).data;for(let i=0;i<count;i++)bytes[i*4+channel]=data[i*4];}
    const gl=this.gl;gl.bindTexture(gl.TEXTURE_2D,this.texture);gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,false);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA8,this.width,this.height,0,gl.RGBA,gl.UNSIGNED_BYTE,bytes);
  }
  begin(channel){this.undo.push({channel,data:this.canvases[channel].getContext('2d').getImageData(0,0,this.width,this.height)});if(this.undo.length>12)this.undo.shift();this.redo=[]}
  paint(channel,x,y,radius,erase=false,aspect=1){
    const ctx=this.canvases[channel].getContext('2d'),r=Math.max(1,radius*this.height),px=x*this.width,py=y*this.height;
    ctx.save();ctx.translate(px,py);ctx.scale(aspect,1);const gradient=ctx.createRadialGradient(0,0,r*.12,0,0,r);gradient.addColorStop(0,erase?'rgba(0,0,0,.32)':'rgba(255,255,255,.32)');gradient.addColorStop(1,erase?'rgba(0,0,0,0)':'rgba(255,255,255,0)');ctx.fillStyle=gradient;ctx.fillRect(-r,-r,r*2,r*2);ctx.restore();
  }
  history(back=true){const from=back?this.undo:this.redo,to=back?this.redo:this.undo,record=from.pop();if(!record)return;const ctx=this.canvases[record.channel].getContext('2d');to.push({channel:record.channel,data:ctx.getImageData(0,0,this.width,this.height)});ctx.putImageData(record.data,0,0);this.upload()}
  export(){return Object.fromEntries(this.canvases.map((canvas,i)=>[channels[i],canvas.toDataURL('image/png').split(',')[1]]))}
  dispose(){this.gl.deleteTexture(this.texture);this.canvases.length=0;this.undo=[];this.redo=[]}
}

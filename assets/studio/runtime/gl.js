export function program(gl,vertex,fragment){
  const compile=(kind,source)=>{const shader=gl.createShader(kind);gl.shaderSource(shader,source);gl.compileShader(shader);if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS)){const error=gl.getShaderInfoLog(shader);gl.deleteShader(shader);throw Error(error)}return shader};
  const vs=compile(gl.VERTEX_SHADER,vertex),fs=compile(gl.FRAGMENT_SHADER,fragment),handle=gl.createProgram();gl.attachShader(handle,vs);gl.attachShader(handle,fs);gl.linkProgram(handle);gl.deleteShader(vs);gl.deleteShader(fs);if(!gl.getProgramParameter(handle,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(handle));
  const uniforms=new Map();return {handle,use(){gl.useProgram(handle)},location(name){if(!uniforms.has(name))uniforms.set(name,gl.getUniformLocation(handle,name));return uniforms.get(name)},f(name,value){gl.uniform1f(this.location(name),value)},i(name,value){gl.uniform1i(this.location(name),value)},v2(name,value){gl.uniform2fv(this.location(name),value)},v3(name,value){gl.uniform3fv(this.location(name),value)},v4(name,value){gl.uniform4fv(this.location(name),value)},dispose(){gl.deleteProgram(handle)}};
}
export function grid(gl,columns=48,rows=40){
  const vertices=[],indices=[];for(let y=0;y<=rows;y++)for(let x=0;x<=columns;x++)vertices.push(x/columns,y/rows);
  for(let y=0;y<rows;y++)for(let x=0;x<columns;x++){const a=y*(columns+1)+x,b=a+columns+1;indices.push(a,b,a+1,a+1,b,b+1)}
  const vao=gl.createVertexArray(),vbo=gl.createBuffer(),ibo=gl.createBuffer();gl.bindVertexArray(vao);gl.bindBuffer(gl.ARRAY_BUFFER,vbo);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(vertices),gl.STATIC_DRAW);gl.enableVertexAttribArray(0);gl.vertexAttribPointer(0,2,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ibo);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(indices),gl.STATIC_DRAW);gl.bindVertexArray(null);
  return {vao,count:indices.length,draw(){gl.bindVertexArray(vao);gl.drawElements(gl.TRIANGLES,indices.length,gl.UNSIGNED_SHORT,0)},dispose(){gl.deleteVertexArray(vao);gl.deleteBuffer(vbo);gl.deleteBuffer(ibo)}};
}
export function texture(gl,source,{repeat=false,mipmap=false,premult=false}={}){
  const handle=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,handle);gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,false);gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,premult);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,source);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,repeat?gl.REPEAT:gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,repeat?gl.REPEAT:gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,mipmap?gl.LINEAR_MIPMAP_LINEAR:gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);if(mipmap)gl.generateMipmap(gl.TEXTURE_2D);return handle;
}
export function bindTexture(gl,unit,handle){gl.activeTexture(gl.TEXTURE0+unit);gl.bindTexture(gl.TEXTURE_2D,handle)}
export function target(gl,width,height,half=false){
  const handle=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,handle);gl.texImage2D(gl.TEXTURE_2D,0,half?gl.RGBA16F:gl.RGBA8,width,height,0,gl.RGBA,half?gl.HALF_FLOAT:gl.UNSIGNED_BYTE,null);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
  const framebuffer=gl.createFramebuffer();gl.bindFramebuffer(gl.FRAMEBUFFER,framebuffer);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,handle,0);if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)throw Error('Framebuffer is unavailable');
  return {handle,framebuffer,width,height,bytes:width*height*(half?8:4),dispose(){gl.deleteFramebuffer(framebuffer);gl.deleteTexture(handle)}};
}
export async function image(url){const result=new Image();result.crossOrigin='anonymous';const ready=new Promise((resolve,reject)=>{result.onload=()=>resolve(result);result.onerror=()=>reject(Error('Не удалось загрузить изображение: '+new URL(url).pathname))});result.src=url;return ready}
export const quadVertex=`#version 300 es
precision highp float;layout(location=0)in vec2 aUv;out vec2 vUv;void main(){vUv=aUv;gl_Position=vec4(aUv*2.-1.,0.,1.);}`;

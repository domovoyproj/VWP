"""Compile the WPF shader using the Windows inbox Direct3D compiler."""
import ctypes as c
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def blob_bytes(blob):
    table=c.cast(blob,c.POINTER(c.POINTER(c.c_void_p))).contents
    pointer=c.WINFUNCTYPE(c.c_void_p,c.c_void_p)(table[3])(blob)
    length=c.WINFUNCTYPE(c.c_size_t,c.c_void_p)(table[4])(blob)
    return c.string_at(pointer,length)
def release(blob):
    if blob:
        table=c.cast(blob,c.POINTER(c.POINTER(c.c_void_p))).contents
        c.WINFUNCTYPE(c.c_ulong,c.c_void_p)(table[2])(blob)
def main():
    source=(ROOT/'assets/live/Planets.hlsl').read_bytes()
    compiler=c.WinDLL('d3dcompiler_47.dll').D3DCompile
    compiler.argtypes=[c.c_void_p,c.c_size_t,c.c_char_p,c.c_void_p,c.c_void_p,c.c_char_p,c.c_char_p,c.c_uint,c.c_uint,c.POINTER(c.c_void_p),c.POINTER(c.c_void_p)]
    compiler.restype=c.c_long
    code=c.c_void_p();errors=c.c_void_p()
    try:
        status=compiler(source,len(source),b'SceneMotion.hlsl',None,None,b'main',b'ps_3_0',1<<15,0,c.byref(code),c.byref(errors))
        if status<0:raise RuntimeError(blob_bytes(errors).decode(errors='replace'))
        data=blob_bytes(code)
        (ROOT/'assets/live/Planets.ps').write_bytes(data)
        print(f'Compiled scene shader: {len(data)} bytes')
    finally:release(code);release(errors)
if __name__=='__main__':main()

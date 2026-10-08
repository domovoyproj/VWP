sampler2D image : register(s0);
sampler2D mask : register(s1);
sampler2D surface : register(s2);
float time : register(c0);
float4 planetA : register(c1);
float4 planetB : register(c2);
float4 frame : register(c3);

float3 globe(float2 uv,float4 sphere,float period,float3 baseColor)
{
    float2 p=(uv-sphere.xy)/max(sphere.zw,.0001);
    float z=sqrt(saturate(1-dot(p,p)));
    float longitude=atan2(p.x,z)+time*6.2831853/period;
    float latitude=asin(clamp(p.y,-1,1));
    float material=tex2D(surface,float2(frac(longitude/6.2831853+.5),latitude/3.14159265+.5)).r;
    float light=.65+.35*saturate(dot(normalize(float3(-.7,-.25,1)),float3(p,z)));
    // Surface detail rotates on a sphere; illumination and the silhouette stay fixed.
    return baseColor*(.65+material*.85)*light*1.1;
}
float4 main(float2 uv:TEXCOORD):COLOR
{
    float2 source=(uv-frame.xy)/frame.zw;
    float4 color=tex2D(image,uv);
    float2 m=tex2D(mask,saturate(source)).rg;
    m*=step(0,source.x)*step(source.x,1)*step(0,source.y)*step(source.y,1);
    if(m.r>.001)color.rgb=lerp(color.rgb,globe(source,planetA,90,color.rgb),m.r);
    if(m.g>.001)color.rgb=lerp(color.rgb,globe(source,planetB,-120,color.rgb),m.g);
    return color;
}

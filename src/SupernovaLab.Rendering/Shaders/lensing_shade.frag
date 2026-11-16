#version 410 core
// Ruling R-P3-10 (Task 4b): colours the cached ray map (lensing.frag with u_output = 1) from the surroundings as they are now.
// One fragment per ray-map texel: a = 0 captured (black), a = 1 escaped or step-limited (the environment cube map along the
// stored exit direction, ruling R-P3-7), a = 2 a neutron star's surface (nsurface.frag's shading of the hit point stored in
// rgb, plan Task 6). The same colours as lensing.frag's own u_output = 0 branch, so the cache changes the cost, not the picture.

uniform sampler2D u_rays;      // the ray map, RGBA32F, the same size as this pass's viewport
uniform samplerCube u_env;     // the surroundings seen from the remnant (EnvironmentCapture)

out vec4 o_color;

vec3 nsSurface(vec3 hit);      // nsurface.frag

void main()
{
    vec4 ray = texelFetch(u_rays, ivec2(gl_FragCoord.xy), 0);
    vec3 color = vec3(0.0);
    if (ray.a > 1.5) color = nsSurface(ray.xyz);
    else if (ray.a > 0.5) color = texture(u_env, ray.xyz).rgb;
    o_color = vec4(color, 1.0);
}

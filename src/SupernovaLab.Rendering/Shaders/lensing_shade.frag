#version 410 core
// Ruling R-P3-10 (Task 4b): colours the cached ray map (lensing.frag with u_output = 1) from the surroundings as they are now.
// One fragment per ray-map texel: a = 0 captured (black), a = 1 escaped or step-limited (the environment cube map along the
// stored exit direction, ruling R-P3-7), a = 2 a neutron star's surface (nsurface.frag's shading of the hit point stored in
// rgb, plan Task 6), a = 3 + coverage an escaped ray across a grid line (plan Task 10, ruling P3-8: the environment mixed with the
// grid's colour by that coverage), a = 5 the accretion disk (plan Task 11: rgb = (g, r), shaded by disk.frag's diskShade, whose
// brightness uniform carries the fade, so the fade never retraces). The same colours as lensing.frag's own u_output = 0 branch, so
// the cache changes the cost, not the picture.

uniform sampler2D u_rays;      // the ray map, RGBA32F, the same size as this pass's viewport
uniform samplerCube u_env;     // the surroundings seen from the remnant (EnvironmentCapture)
uniform vec3 u_gridColor;      // the grid lines' colour (plan Task 10)
uniform vec4 u_veil;           // Task 20 (#71): the opaque debris's glow (rgb) mixed over the surroundings by a (0 clear, 1 opaque)

out vec4 o_color;

vec3 nsSurface(vec3 hit);      // nsurface.frag
vec3 diskShade(float g, float r);   // disk.frag (plan Task 11)

// The surroundings along an escaped ray's exit direction, seen through the debris's veil.
vec3 surroundings(vec3 dir) { return mix(texture(u_env, dir).rgb, u_veil.rgb, u_veil.a); }

void main()
{
    vec4 ray = texelFetch(u_rays, ivec2(gl_FragCoord.xy), 0);
    vec3 color = vec3(0.0);
    if (ray.a > 4.5) color = diskShade(ray.x, ray.y);
    else if (ray.a > 2.5) color = mix(surroundings(ray.xyz), u_gridColor, clamp(ray.a - 3.0, 0.0, 1.0));
    else if (ray.a > 1.5) color = nsSurface(ray.xyz);
    else if (ray.a > 0.5) color = surroundings(ray.xyz);
    o_color = vec4(color, 1.0);
}

#version 410 core
// u_kind 0: a bright point with a dark outline, so it stands out against bright debris (neutron star, white dwarf);
// 1: a black disk with a thin bright ring (black hole, schematic).

uniform int u_kind;
uniform vec3 u_color;
uniform float u_glow;       // a giant flare's drawn brightness, 0 to 1 (plan Task 12): the point swells and a white halo spreads
uniform float u_sizeScale;  // how much larger the point sprite is drawn for the glow (1 + gain * glow), so the core keeps its pixel size

out vec4 o_color;

void main()
{
    float r = length(gl_PointCoord * 2.0 - 1.0);
    if (u_kind == 1)
    {
        float disk = 1.0 - smoothstep(0.60, 0.68, r);
        float ring = smoothstep(0.60, 0.68, r) * (1.0 - smoothstep(0.80, 0.92, r));
        float a = max(disk, ring);
        if (a <= 0.0) discard;
        o_color = vec4(u_color * ring, a);
        return;
    }
    float rs = r * u_sizeScale;                       // r in units of the unswollen sprite
    float core = 1.0 - smoothstep(0.38, 0.50, rs);
    float outline = 0.85 * (1.0 - smoothstep(0.80, 0.95, rs));
    float halo = u_glow * pow(max(1.0 - r, 0.0), 2.0);
    float a = max(max(core, outline), halo);
    if (a <= 0.0) discard;
    vec3 c = mix(u_color, vec3(1.0), u_glow);
    o_color = vec4(c * max(core, halo * 0.8), a);   // the halo stays below white: the flash is small and bounded
}
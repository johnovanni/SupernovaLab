#version 410 core
// u_kind 0: a bright point with a dark outline, so it stands out against bright debris (neutron star, white dwarf);
// 1: a black disk with a thin bright ring (black hole, schematic).

uniform int u_kind;
uniform vec3 u_color;

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
    float core = 1.0 - smoothstep(0.38, 0.50, r);
    float outline = 0.85 * (1.0 - smoothstep(0.80, 0.95, r));
    float a = max(core, outline);
    if (a <= 0.0) discard;
    o_color = vec4(u_color * core, a);
}
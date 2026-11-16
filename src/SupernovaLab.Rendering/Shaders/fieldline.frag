#version 410 core
// One flat colour, with the parts the neutron star hides dropped: the GLSL mirror of StarOcclusion.Hidden (C#). A point is hidden
// when the straight line from the eye enters the star's sphere before it (with a margin of 0.01 radii, so the near surface's own
// points show), or when it lies beyond the star's centre within the bent disc's apparent radius.

in vec3 v_rel;

uniform vec4 u_color;
uniform vec3 u_occCentre;   // the star's centre relative to the eye
uniform vec2 u_occRadii;    // its radius and its apparent (bent) radius; 0: nothing hides the lines

out vec4 o_color;

bool hiddenByStar(vec3 p)
{
    float s = length(p);
    float r = u_occRadii.x;
    if (!(r > 0.0) || !(s > 0.0)) return false;
    vec3 d = p / s;
    float tc = dot(u_occCentre, d);
    vec3 perp = u_occCentre - tc * d;
    float b2 = dot(perp, perp);
    if (b2 < r * r && s > tc - sqrt(r * r - b2) + 0.01 * r) return true;
    return b2 < u_occRadii.y * u_occRadii.y && s > tc;
}

void main()
{
    if (hiddenByStar(v_rel)) discard;
    o_color = u_color;
}

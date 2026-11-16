#version 410 core
// Translucent, fading from the apex to the rim. Plan Task 6: in the Close-up the neutron star hides the parts of a beam inside or
// behind it (the GLSL mirror of StarOcclusion.Hidden, as fieldline.frag); u_occRadii = 0 (the main view, the jets) hides nothing.

in float v_along;
in vec3 v_rel;

uniform vec3 u_color;
uniform float u_alpha;
uniform vec3 u_occCentre;   // the star's centre relative to the eye
uniform vec2 u_occRadii;    // its radius and its apparent (bent) radius; 0: nothing hidden

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
    o_color = vec4(u_color, u_alpha * (1.0 - v_along));
}

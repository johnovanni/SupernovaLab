#version 410 core
// A blackbody-coloured disk with schematic linear limb darkening, I(mu) / I(1) = 1 - 0.6 (1 - mu).

in vec2 v_corner;

uniform vec3 u_color;

out vec4 o_color;

void main()
{
    float r = length(v_corner);
    float edge = max(fwidth(r), 1e-4);
    float a = 1.0 - smoothstep(1.0 - edge, 1.0, r);
    if (a <= 0.0) discard;
    float mu = sqrt(max(0.0, 1.0 - r * r));
    float limb = 1.0 - 0.6 * (1.0 - mu);
    o_color = vec4(u_color * limb, a);
}
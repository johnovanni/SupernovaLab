#version 410 core
// A glowing thin tube: brightest where the surface faces the eye, fading to the sides, so the ring reads as a soft line of gas.

uniform vec3 u_color;
uniform float u_intensity;

in vec3 v_normal;
in vec3 v_rel;

out vec4 o_color;

void main()
{
    float facing = abs(dot(normalize(v_normal), normalize(v_rel)));
    o_color = vec4(u_color * (u_intensity * (0.15 + 0.85 * facing * facing)), 0.0);
}

#version 410 core
// A hotspot: a round glowing dot, bright at the centre and soft at the rim.

uniform vec3 u_color;
uniform float u_intensity;

in float v_strength;

out vec4 o_color;

void main()
{
    float r = length(gl_PointCoord * 2.0 - 1.0);
    float glow = 1.0 - smoothstep(0.0, 1.0, r);
    if (glow <= 0.0) discard;
    o_color = vec4(u_color * (u_intensity * v_strength * glow * glow), 0.0);
}

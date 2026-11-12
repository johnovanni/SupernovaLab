#version 410 core
// Translucent, fading from the apex to the rim.

in float v_along;

uniform vec3 u_color;
uniform float u_alpha;

out vec4 o_color;

void main()
{
    o_color = vec4(u_color, u_alpha * (1.0 - v_along));
}
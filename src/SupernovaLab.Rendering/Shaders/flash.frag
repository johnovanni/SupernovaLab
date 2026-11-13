#version 410 core
// The breakout flash: light added over the whole view. A glow centred on the view (u_core, falling off over about a third of
// the view's height) on top of an even veil (u_veil); colour * (core * glow + veil), added to what is drawn.

in vec2 v_uv;

uniform vec3 u_color;
uniform float u_core;
uniform float u_veil;
uniform float u_aspect;     // view width / height

out vec4 o_color;

void main()
{
    vec2 d = (v_uv - vec2(0.5)) * vec2(u_aspect, 1.0);
    float glow = exp(-dot(d, d) * 12.0);
    o_color = vec4(u_color * (u_core * glow + u_veil), 0.0);
}

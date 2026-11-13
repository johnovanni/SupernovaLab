#version 410 core
// A distance ring's line: grey #888780 at the ring's opacity.

uniform float u_opacity;

out vec4 o_color;

void main()
{
    o_color = vec4(0.5333, 0.5294, 0.5020, u_opacity);
}

#version 410 core
// Dual-filter upsample (Bjorge): eight taps, the four axis taps at weight 1 and the four diagonals at weight 2, over 12.
// Blended additively into the larger level.

in vec2 v_uv;

uniform sampler2D u_src;
uniform vec2 u_halfPixel;   // half a texel of the source (the smaller level)

out vec4 o_color;

void main()
{
    vec2 h = u_halfPixel;
    vec4 sum = texture(u_src, v_uv + vec2(-h.x * 2.0, 0.0));
    sum += texture(u_src, v_uv + vec2(-h.x, h.y)) * 2.0;
    sum += texture(u_src, v_uv + vec2(0.0, h.y * 2.0));
    sum += texture(u_src, v_uv + vec2(h.x, h.y)) * 2.0;
    sum += texture(u_src, v_uv + vec2(h.x * 2.0, 0.0));
    sum += texture(u_src, v_uv + vec2(h.x, -h.y)) * 2.0;
    sum += texture(u_src, v_uv + vec2(0.0, -h.y * 2.0));
    sum += texture(u_src, v_uv + vec2(-h.x, -h.y)) * 2.0;
    o_color = sum / 12.0;
}

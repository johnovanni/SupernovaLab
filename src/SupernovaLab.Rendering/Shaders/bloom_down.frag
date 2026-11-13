#version 410 core
// Dual-filter downsample (Bjorge): the centre at weight 4 and four diagonal half-texel taps, over 8.

in vec2 v_uv;

uniform sampler2D u_src;
uniform vec2 u_halfPixel;   // half a texel of the destination, in source uv

out vec4 o_color;

void main()
{
    vec4 sum = texture(u_src, v_uv) * 4.0;
    sum += texture(u_src, v_uv - u_halfPixel);
    sum += texture(u_src, v_uv + u_halfPixel);
    sum += texture(u_src, v_uv + vec2(u_halfPixel.x, -u_halfPixel.y));
    sum += texture(u_src, v_uv - vec2(u_halfPixel.x, -u_halfPixel.y));
    o_color = sum / 8.0;
}

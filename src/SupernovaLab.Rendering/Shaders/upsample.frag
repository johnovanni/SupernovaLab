#version 410 core
// The lensing pass's bilinear upsample (plan ruling P3-9): the reduced-resolution image (LensingPass) stretched over the view
// with the texture's linear filter. The caller masks alpha off, so the target keeps its own (the HDR target's face mask is 0
// here; the GL control's own framebuffer keeps its opaque clear).

in vec2 v_uv;

uniform sampler2D u_src;

out vec4 o_color;

void main()
{
    o_color = vec4(texture(u_src, v_uv).rgb, 1.0);
}

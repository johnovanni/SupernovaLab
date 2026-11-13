#version 410 core
// Scene + bloom -> Narkowicz's ACES fit (HdrPipeline.Tonemap mirrors it) -> gamma 2.2 to the default framebuffer.

in vec2 v_uv;

uniform sampler2D u_scene;
uniform sampler2D u_bloom;
uniform float u_exposure;
uniform float u_bloomStrength;

out vec4 o_color;

vec3 aces(vec3 x)
{
    return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

void main()
{
    vec3 hdr = texture(u_scene, v_uv).rgb;
    if (u_bloomStrength > 0.0) hdr += texture(u_bloom, v_uv).rgb * u_bloomStrength;
    vec3 mapped = aces(hdr * u_exposure);
    o_color = vec4(pow(mapped, vec3(1.0 / 2.2)), 1.0);
}

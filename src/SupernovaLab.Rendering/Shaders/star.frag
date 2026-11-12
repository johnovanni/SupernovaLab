#version 410 core
// u_mode 0, the star: a blackbody-coloured disk with schematic linear limb darkening, I(mu) / I(1) = 1 - 0.6 (1 - mu).
// u_mode 1, the gas around the star: a translucent haze whose opacity follows the path length through a uniform ball,
// sqrt(1 - r^2), plus a faint rim so its edge reads as a shell (schematic).

in vec2 v_corner;

uniform vec3 u_color;
uniform int u_mode;
uniform float u_alpha;

out vec4 o_color;

void main()
{
    float r = length(v_corner);
    float edge = max(fwidth(r), 1e-4);
    float a = 1.0 - smoothstep(1.0 - edge, 1.0, r);
    if (a <= 0.0) discard;
    float mu = sqrt(max(0.0, 1.0 - r * r));
    if (u_mode == 1)
    {
        float rim = smoothstep(0.85, 0.98, r);
        o_color = vec4(u_color, a * u_alpha * (0.6 * mu + 0.6 * rim));
        return;
    }
    float limb = 1.0 - 0.6 * (1.0 - mu);
    o_color = vec4(u_color * limb, a);
}
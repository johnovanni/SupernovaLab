#version 410 core
// One soft Gaussian dot, added to what is behind it. A particle the reverse shock has passed (v_shocked = its weight) takes the
// hot-gas tint and u_shockBoost (1.5) times the brightness (structure section S4; schematic).

in float v_shocked;

uniform vec3 u_color;
uniform vec3 u_hotColor;      // the hot-gas tint at the same brightness as u_color
uniform float u_shockBoost;    // ParticleUniforms.ShockedBoost (1.5)
uniform float u_alpha;

out vec4 o_color;

void main()
{
    vec2 p = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(p, p);
    if (r2 > 1.0) discard;
    vec3 c = mix(u_color, u_hotColor, v_shocked) * (1.0 + (u_shockBoost - 1.0) * v_shocked);
    o_color = vec4(c, u_alpha * exp(-3.0 * r2));
}

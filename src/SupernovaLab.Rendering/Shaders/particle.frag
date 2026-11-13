#version 410 core
// One soft Gaussian dot, added to what is behind it. In the cutaway's face pass (u_cut = 2) a dot is an opaque, soft-edged
// disc in its element colour, blended over what is behind it.

uniform vec3 u_color;
uniform float u_alpha;
uniform int u_cut;

in vec3 v_color;
out vec4 o_color;

void main()
{
    vec2 p = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(p, p);
    if (r2 > 1.0) discard;
    if (u_cut == 2)
    {
        o_color = vec4(v_color, 1.0 - smoothstep(0.55, 1.0, sqrt(r2)));
        return;
    }
    o_color = vec4(u_color, u_alpha * exp(-3.0 * r2));
}

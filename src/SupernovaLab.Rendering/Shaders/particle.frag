#version 410 core
// One soft Gaussian dot, added to what is behind it.

uniform vec3 u_color;
uniform float u_alpha;

out vec4 o_color;

void main()
{
    vec2 p = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(p, p);
    if (r2 > 1.0) discard;
    o_color = vec4(u_color, u_alpha * exp(-3.0 * r2));
}

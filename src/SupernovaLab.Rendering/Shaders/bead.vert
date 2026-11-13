#version 410 core
// A hotspot bead on SN 1987A's equatorial ring: a point sprite at angle a_bead.x on the ring, with its own strength a_bead.y.

layout(location = 0) in vec2 a_bead;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform vec3 u_planeU;
uniform vec3 u_planeV;
uniform float u_radius;     // scene units
uniform float u_pointPx;

out float v_strength;

void main()
{
    vec3 world = u_radius * (cos(a_bead.x) * u_planeU + sin(a_bead.x) * u_planeV);
    precise vec3 rel = world - u_eyeHigh - u_eyeLow;
    v_strength = a_bead.y;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    gl_PointSize = u_pointPx;
}

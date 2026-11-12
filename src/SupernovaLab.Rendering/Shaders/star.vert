#version 410 core
// The star before the explosion: a disk facing the camera at the origin, u_radius scene units across its radius.

layout(location = 0) in vec2 a_corner;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform vec3 u_right;
uniform vec3 u_up;
uniform float u_radius;

out vec2 v_corner;

void main()
{
    vec3 world = (a_corner.x * u_right + a_corner.y * u_up) * u_radius;
    precise vec3 rel = world - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    v_corner = a_corner;
}
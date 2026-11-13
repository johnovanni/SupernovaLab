#version 410 core
// A distance ring: a circle in the plane facing the camera, centred on the origin, u_radius scene units across its radius.

layout(location = 0) in vec2 a_unit;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform vec3 u_right;
uniform vec3 u_up;
uniform float u_radius;

void main()
{
    vec3 world = (a_unit.x * u_right + a_unit.y * u_up) * u_radius;
    precise vec3 rel = world - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
}

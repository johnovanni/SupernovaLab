#version 410 core
// The ejecta: fixed points of a unit shell (|a_unit| from 0.7 to 1), scaled to the current radius.

layout(location = 0) in vec3 a_unit;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_radius;
uniform float u_pointPx;

void main()
{
    precise vec3 rel = a_unit * u_radius - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    gl_PointSize = u_pointPx;
}
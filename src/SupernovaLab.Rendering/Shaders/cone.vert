#version 410 core
// A cone from the origin: a_local = (cos phi, sin phi, s), s = 0 at the apex and 1 at the rim. u_basis's columns are two
// directions across the cone and its axis.

layout(location = 0) in vec3 a_local;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform mat3 u_basis;
uniform float u_length;
uniform float u_tanHalf;

out float v_along;

void main()
{
    vec3 local = vec3(a_local.xy * u_tanHalf * a_local.z, a_local.z) * u_length;
    precise vec3 rel = u_basis * local - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    v_along = a_local.z;
}
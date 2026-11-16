#version 410 core
// Plan Task 9 (spec 3.2): matter falling onto the shock, schematic straight-in paths. Task 20 (the owner saw the dots move outward as
// the shock grew): each dot's position (km) and opacity come from InfallSwarm on the CPU, where a dot only ever falls, at the free-fall
// profile |v| ~ r^(-1/2), fades out when the shock reaches it and comes back farther out, fading in (one fade per 0.5 s at most: flash
// safe).

layout(location = 0) in vec3 a_pos;
layout(location = 1) in float a_alpha;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_pointPx;
uniform int u_side;           // fix round 1: 0 = every particle, 1 = only those beyond the centre's plane (seen from the eye), 2 = only the near ones

out float v_alpha;

void main()
{
    v_alpha = a_alpha;
    vec3 position = a_pos;
    vec3 eye = u_eyeHigh + u_eyeLow;
    bool near = dot(position, eye) >= 0.0;
    bool drop = a_alpha <= 0.0 || (u_side == 1 && near) || (u_side == 2 && !near);
    precise vec3 rel = position - u_eyeHigh - u_eyeLow;
    gl_Position = drop ? vec4(2.0, 2.0, 2.0, 1.0) : u_viewProj * vec4(rel, 1.0);
    gl_PointSize = drop ? 0.0 : u_pointPx;
}

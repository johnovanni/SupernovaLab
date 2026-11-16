#version 410 core
// Plan Task 9 (spec 3.2): matter falling onto the shock, schematic straight-in paths. Each particle has a fixed direction a_dir and
// a seed a_seed in [0, 1); its fall runs from u_rOut to the shock u_rIn once per cycle (u_cycles counts cycles, accumulated on the
// CPU from CoreAnimation.InfallCycleS, at least 2 s of wall time), with the free-fall profile |v| ~ r^(-1/2):
//   r^1.5 = u_rOut^1.5 - tau (u_rOut^1.5 - u_rIn^1.5),  tau = fract(u_cycles + a_seed).
// A particle fades in at the start of its fall and out as it reaches the shock: one smooth rise and fall per cycle (flash safe).

layout(location = 0) in vec3 a_dir;
layout(location = 1) in float a_seed;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_rIn;          // the shock's mean radius, scene units (km)
uniform float u_rOut;         // where the particles start
uniform float u_cycles;       // falls completed so far (fractional)
uniform float u_pointPx;

out float v_alpha;

void main()
{
    float tau = fract(u_cycles + a_seed);
    float outer = pow(u_rOut, 1.5), inner = pow(u_rIn, 1.5);
    float r = pow(outer - tau * (outer - inner), 2.0 / 3.0);
    v_alpha = smoothstep(0.0, 0.15, tau) * (1.0 - smoothstep(0.8, 1.0, tau));
    vec3 position = a_dir * r;
    precise vec3 rel = position - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    gl_PointSize = u_pointPx;
}

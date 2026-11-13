#version 410 core
// The debris particles. Layout: location 0 = velocity in units of v_max (|v| <= 1), 1 = layer index (0-5, ElementLayer
// order), 2 = seed in [0, 1). Later tasks add the layered colours and the shock confinement.

layout(location = 0) in vec3 a_velocity;
layout(location = 1) in float a_layer;
layout(location = 2) in float a_seed;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_starRadius;   // R*, scene units (cm / unitCm, divided on the CPU so floats never hold 1e17 cm)
uniform float u_expansion;    // v_max * (t - t_bo), scene units
uniform int u_shocks;         // 0 = none (Task 8 adds the confinement branch)
uniform float u_pointPx;

void main()
{
    // Mirrors ParticleKinematics.RadiusCm (C#, plan ruling P4), with s = v / v_max and a_velocity = s * direction:
    //   r = R* * s + v * (t - t_bo) = s * (R* + v_max * (t - t_bo)),  position = r * direction = a_velocity * (R* + expansion).
    // With u_shocks == 0 this is the free rule (the C# function with shocks == null).
    vec3 position = a_velocity * (u_starRadius + u_expansion);
    precise vec3 rel = position - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    gl_PointSize = u_pointPx;
}

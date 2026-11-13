#version 410 core
// The debris particles. Layout: location 0 = velocity in units of v_max (|v| <= 1), 1 = layer index (0-5, ElementLayer
// order), 2 = seed in [0, 1). Later tasks add the shock confinement.
//
// The wedge cutaway (ruling R-P2-21): with u_cut = 1, particles in the removed quarter (x > 0 and z > 0 in scene
// coordinates; it turns with the scene) are not drawn. The cut faces themselves are drawn analytically (cutface.frag), and
// the depth test hides the particles behind them.

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
uniform int u_cut;

void main()
{
    // Mirrors ParticleKinematics.RadiusCm (C#, plan ruling P4), with s = v / v_max and a_velocity = s * direction:
    //   r = R* * s + v * (t - t_bo) = s * (R* + v_max * (t - t_bo)),  position = r * direction = a_velocity * (R* + expansion).
    // With u_shocks == 0 this is the free rule (the C# function with shocks == null).
    vec3 position = a_velocity * (u_starRadius + u_expansion);
    bool drop = u_cut != 0 && position.x > 0.0 && position.z > 0.0;
    precise vec3 rel = position - u_eyeHigh - u_eyeLow;
    gl_Position = drop ? vec4(2.0, 2.0, 2.0, 1.0) : u_viewProj * vec4(rel, 1.0);
    gl_PointSize = drop ? 0.0 : u_pointPx;
}

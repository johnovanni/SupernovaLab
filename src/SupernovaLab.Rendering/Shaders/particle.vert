#version 410 core
// The debris particles. Layout: location 0 = velocity in units of v_max (|v| <= 1), 1 = layer index (0-5, ElementLayer
// order), 2 = seed in [0, 1).
//
// The wedge cutaway (ruling R-P2-21): with u_cut = 1, particles in the removed quarter (w.x > 0 and w.z > 0 in the wedge frame,
// the scene's x and z turned about y by the wedge angle; it turns with the scene) are not drawn. The cut faces themselves are
// drawn analytically (cutface.frag) after the bloom, translucent where the cloud is thin, over the particles behind them (there
// is no depth test).

layout(location = 0) in vec3 a_velocity;
layout(location = 1) in float a_layer;
layout(location = 2) in float a_seed;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_starRadius;   // R*, scene units (cm / unitCm, divided on the CPU so floats never hold 1e17 cm)
uniform float u_expansion;    // v_max * (t - t_bo), scene units
uniform int u_shocks;         // 0 = no shocks (free expansion); 1 = confine the particles the reverse shock has passed
uniform float u_reverse;      // R_r, scene units
uniform float u_contact;      // R_c, scene units
uniform float u_shockWeight;  // ruling R-P2-4's weight w, 0 to 1
uniform float u_shockRatio;   // s_m = v_m / v_max (ParticleKinematics.ShockedSpeedRatio): faster particles are shocked; >= 1: none
uniform float u_pointPx;
uniform int u_cut;
uniform vec3 u_wedgeX;        // the wedge frame (WedgeOrientation): w.x = dot(p, u_wedgeX), w.z = dot(p, u_wedgeZ)
uniform vec3 u_wedgeZ;

out float v_shocked;          // ParticleKinematics.ShockedWeight: the hot-gas tint's weight

void main()
{
    // Mirrors ParticleKinematics.RadiusCm (C#, plan ruling P4), with s = v / v_max and a_velocity = s * direction:
    //   free: r = R* * s + v * (t - t_bo) = s * K, K = R* + v_max * (t - t_bo).
    //   shocked (s > s_m, the reverse shock's running-minimum Lagrangian speed over v_max, ruling R-P2-24):
    //   r_conf = R_r + (R_c - R_r)(s - s_m)/(1 - s_m), weighted in: r = r_free + w (r_conf - r_free) (ruling R-P2-4),
    //   then clamped to the free radius, r = min(r, r_free) (ruling R-P2-33: confinement never pushes debris ahead).
    float s = length(a_velocity);
    float k = u_starRadius + u_expansion;
    float r = s * k;
    v_shocked = 0.0;
    if (u_shocks != 0 && u_shockWeight > 0.0)
    {
        float sm = u_shockRatio;
        if (sm < 1.0 && s > sm)
        {
            float confined = u_reverse + (u_contact - u_reverse) * min(1.0, (s - sm) / (1.0 - sm));
            r = min(r + u_shockWeight * (confined - r), r);
            v_shocked = u_shockWeight;
        }
    }
    vec3 position = s > 0.0 ? a_velocity * (r / s) : vec3(0.0);
    bool drop = u_cut != 0 && dot(position, u_wedgeX) > 0.0 && dot(position, u_wedgeZ) > 0.0;
    precise vec3 rel = position - u_eyeHigh - u_eyeLow;
    gl_Position = drop ? vec4(2.0, 2.0, 2.0, 1.0) : u_viewProj * vec4(rel, 1.0);
    gl_PointSize = drop ? 0.0 : u_pointPx;
}

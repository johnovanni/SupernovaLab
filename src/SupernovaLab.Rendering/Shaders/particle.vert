#version 410 core
// The debris particles. Layout: location 0 = velocity in units of v_max (|v| <= 1), 1 = layer index (0-5, ElementLayer
// order), 2 = seed in [0, 1). Later tasks add the shock confinement.
//
// The wedge cutaway (plan ruling P7). The removed quarter is x > 0 and z > 0 in scene coordinates (it turns with the scene).
//   u_cut = 0: no cutaway.
//   u_cut = 1: the thermal pass. Particles in the removed quarter are dropped, and so are those on a cut face (the face pass
//              draws them in their element colour).
//   u_cut = 2: the face pass. Only particles of layer u_faceLayer within u_cutHalf of a cut face are drawn, in that layer's colour.
// A cut face is a retained particle within u_cutHalf (3% of the framing radius) of a cut plane on the plane's exposed side:
// -u_cutHalf < x <= 0 with z > 0, or -u_cutHalf < z <= 0 with x > 0.

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
uniform float u_cutHalf;
uniform int u_faceLayer;
uniform vec3 u_layerColor[6]; // the face pass's HDR colours, by layer

out vec3 v_color;

void main()
{
    // Mirrors ParticleKinematics.RadiusCm (C#, plan ruling P4), with s = v / v_max and a_velocity = s * direction:
    //   r = R* * s + v * (t - t_bo) = s * (R* + v_max * (t - t_bo)),  position = r * direction = a_velocity * (R* + expansion).
    // With u_shocks == 0 this is the free rule (the C# function with shocks == null).
    vec3 position = a_velocity * (u_starRadius + u_expansion);
    v_color = vec3(0.0);
    bool drop = false;
    if (u_cut != 0)
    {
        bool removed = position.x > 0.0 && position.z > 0.0;
        bool face = !removed
            && ((position.z > 0.0 && position.x > -u_cutHalf) || (position.x > 0.0 && position.z > -u_cutHalf));
        if (u_cut == 1) drop = removed || face;
        else
        {
            int layer = int(a_layer + 0.5);
            drop = !face || layer != u_faceLayer;
            v_color = u_layerColor[clamp(layer, 0, 5)];
        }
    }
    precise vec3 rel = position - u_eyeHigh - u_eyeLow;
    gl_Position = drop ? vec4(2.0, 2.0, 2.0, 1.0) : u_viewProj * vec4(rel, 1.0);
    gl_PointSize = drop ? 0.0 : u_pointPx;
}

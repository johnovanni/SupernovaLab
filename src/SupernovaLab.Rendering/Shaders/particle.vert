#version 410 core
// The debris particles. Layout: location 0 = velocity in units of v_max (|v| <= 1), 1 = layer index (0-5, ElementLayer
// order), 2 = seed in [0, 1). For now the position is the velocity scaled to the outer radius (homologous expansion);
// later tasks add the layered colours and the interaction rule.

layout(location = 0) in vec3 a_velocity;
layout(location = 1) in float a_layer;
layout(location = 2) in float a_seed;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_radius;     // scene units reached by |v| = 1
uniform float u_pointPx;

void main()
{
    precise vec3 rel = a_velocity * u_radius - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    gl_PointSize = u_pointPx;
}

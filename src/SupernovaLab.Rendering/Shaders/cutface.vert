#version 410 core
// The debris cutaway's two cut faces (ruling R-P2-21): the quarter planes bounding the removed quarter (x > 0 and z > 0 in
// scene coordinates). a_pos is the corner of a unit quad in (a, b); u_plane 0 is the plane x = 0 (a = y, b = z), 1 the
// plane z = 0 (a = y, b = x). u_extent scales it (scene units).

layout(location = 0) in vec2 a_pos;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform float u_extent;
uniform int u_plane;

out vec3 v_world;

void main()
{
    // a in [-1, 1], b in [0, 1]
    vec3 world = u_plane == 0 ? vec3(0.0, a_pos.x, a_pos.y) : vec3(a_pos.y, a_pos.x, 0.0);
    world *= u_extent;
    precise vec3 rel = world - u_eyeHigh - u_eyeLow;
    gl_Position = u_viewProj * vec4(rel, 1.0);
    v_world = world;
}

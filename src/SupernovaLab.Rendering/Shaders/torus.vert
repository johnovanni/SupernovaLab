#version 410 core
// One thin torus (a ring of SN 1987A, structure S8): a circle of radius u_radius in the plane spanned by u_planeU and u_planeV,
// centred u_center along the symmetry axis, swept by a tube of radius u_tube. a_angles = (theta around the ring, phi around the tube).
// All lengths are scene units (cm over the view's unit), so floats never hold 1e18 cm.

layout(location = 0) in vec2 a_angles;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform vec3 u_center;
uniform vec3 u_planeU;
uniform vec3 u_planeV;
uniform vec3 u_axis;
uniform float u_radius;
uniform float u_tube;

out vec3 v_normal;
out vec3 v_rel;

void main()
{
    vec3 around = cos(a_angles.x) * u_planeU + sin(a_angles.x) * u_planeV;
    vec3 normal = cos(a_angles.y) * around + sin(a_angles.y) * u_axis;
    vec3 world = u_center + u_radius * around + u_tube * normal;
    precise vec3 rel = world - u_eyeHigh - u_eyeLow;
    v_normal = normal;
    v_rel = rel;
    gl_Position = u_viewProj * vec4(rel, 1.0);
}

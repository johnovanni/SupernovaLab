#version 410 core
// The Close-up's lines (plan Task 6): a neutron star's field lines, its light cylinder and the Earth arrow, as GL_LINES pairs in
// km in the set's own frame (the magnetic frame for the field lines), turned into the scene by u_frame and drawn relative to the eye.

layout(location = 0) in vec3 a_pos;

uniform vec3 u_eyeHigh;
uniform vec3 u_eyeLow;
uniform mat4 u_viewProj;
uniform mat3 u_frame;

out vec3 v_rel;

void main()
{
    vec3 world = u_frame * a_pos;
    precise vec3 rel = world - u_eyeHigh - u_eyeLow;
    v_rel = rel;
    gl_Position = u_viewProj * vec4(rel, 1.0);
}

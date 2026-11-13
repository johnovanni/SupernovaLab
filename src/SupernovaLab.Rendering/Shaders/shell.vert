#version 410 core
// A shell impostor (plan ruling P8): one full-screen triangle pair; shell.frag rebuilds each pixel's line of sight from the
// camera and integrates the shell's analytic column density along it. Full screen, so it also works with the eye inside a shell.

layout(location = 0) in vec2 a_corner;

out vec2 v_ndc;

void main()
{
    v_ndc = a_corner;
    gl_Position = vec4(a_corner, 0.0, 1.0);
}

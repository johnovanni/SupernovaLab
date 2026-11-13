#version 410 core
// A translucent shell (plan ruling P8): the brightness of each pixel is the shell's column density along its line of sight
// (optically thin emission), divided by the largest column (u_peak), so the limb is brightest. Mirrors ColumnDensity (C#):
// change one, change the other. u_kind 0 = uniform shell between u_rIn and u_rOut, 1 = uniform ball of radius u_rOut,
// 2 = rho ~ 1/r^2 between u_rIn and u_rOut. Radii in scene units, centred on the origin.
//
// The line of sight: from the eye u_eye through the pixel, direction d. s is the distance along it from the point of closest
// approach C (distance b from the centre); the eye is at s = -t0, so only s > -t0 counts. With the cutaway (u_cut = 1) the part
// of the line inside the removed quarter (w.x > 0 and w.z > 0 in the wedge frame, as the particles) is subtracted.

in vec2 v_ndc;

uniform vec3 u_forward;       // the camera's view direction and screen axes (OrbitCamera.ScreenAxes), unit vectors
uniform vec3 u_right;
uniform vec3 u_up;
uniform vec2 u_tanHalf;       // tan of half the field of view, horizontally and vertically
uniform vec3 u_eye;           // the eye, scene units, relative to the centre
uniform int u_kind;
uniform float u_rIn;
uniform float u_rOut;
uniform float u_peak;         // the largest column (ColumnDensity.Peak), so the brightest line of sight gives u_intensity
uniform vec3 u_color;
uniform float u_intensity;
uniform float u_cover;        // 0 = purely additive glow; above 0 the shell also hides what is behind it by u_cover * column / u_peak (blend One, OneMinusSrcAlpha)
uniform int u_cut;
uniform vec3 u_wedgeX;        // the wedge frame (WedgeOrientation)
uniform vec3 u_wedgeZ;

out vec4 o_color;

const float BIG = 1e30;

// The integral over [a, c] inside the material: its length, or the integral of ds / (b^2 + s^2) for the wind.
float piece(float b, float a, float c)
{
    if (c <= a) return 0.0;
    if (u_kind != 2) return c - a;
    if (b > 0.0) return atan(b * (c - a), b * b + a * c) / b;
    return 1.0 / a - 1.0 / c;
}

// The column over s in [s1, s2] (ColumnDensity.Segment).
float segment(float b, float s1, float s2)
{
    if (b >= u_rOut || s2 <= s1) return 0.0;
    float zOut = sqrt(u_rOut * u_rOut - b * b);
    float zIn = u_kind == 1 ? 0.0 : sqrt(max(u_rIn * u_rIn - b * b, 0.0));
    return piece(b, max(s1, -zOut), min(s2, -zIn)) + piece(b, max(s1, zIn), min(s2, zOut));
}

// Narrows [lo, hi] to the part of the line C + s d with dot(p, n) > 0.
void halfSpace(vec3 c, vec3 d, vec3 n, inout float lo, inout float hi)
{
    float cn = dot(c, n), dn = dot(d, n);
    if (abs(dn) < 1e-9) { if (cn <= 0.0) hi = lo - 1.0; return; }
    float s = -cn / dn;
    if (dn > 0.0) lo = max(lo, s); else hi = min(hi, s);
}

void main()
{
    vec3 d = normalize(u_forward + v_ndc.x * u_tanHalf.x * u_right + v_ndc.y * u_tanHalf.y * u_up);
    float t0 = -dot(u_eye, d);
    vec3 c = u_eye + t0 * d;
    float b = length(c);
    if (b >= u_rOut || u_peak <= 0.0) discard;
    float sEye = -t0;
    float column = segment(b, sEye, BIG);
    if (u_cut != 0)
    {
        float lo = sEye, hi = BIG;
        halfSpace(c, d, u_wedgeX, lo, hi);
        halfSpace(c, d, u_wedgeZ, lo, hi);
        column -= segment(b, lo, hi);
    }
    if (column <= 0.0) discard;
    o_color = vec4(u_color * (u_intensity * column / u_peak), min(u_cover * column / u_peak, 1.0));
}

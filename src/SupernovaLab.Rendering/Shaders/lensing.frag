#version 410 core
// The Close-up's light bending (spec section 6.6; plan rulings P3-7 and P3-9; rulings R-P3-6 to R-P3-8). Each pixel's ray is
// traced back from the camera through the Kerr metric, and coloured by what it meets: black when the hole captures it, the
// surroundings (the environment cube map, EnvironmentCapture) along its exit direction when it escapes or runs out of steps,
// the surface colour when it hits a neutron star.
//
// This is the GLSL mirror of BlackHoleOptics.TraceRay (SupernovaLab.Core/Compact/BlackHoleOptics.cs), whose class notes are
// the specification: change one, change the other. In float rather than double; otherwise step for step the same:
//   1. Cartesian Kerr-Schild, M = 1: f = 2 r^3 / (r^4 + a^2 z^2), l = ((r x + a y)/(r^2 + a^2), (r y - a x)/(r^2 + a^2), z/r),
//      r^2 = (w + sqrt(w^2 + 4 a^2 z^2)) / 2 with w = x^2 + y^2 + z^2 - a^2 (the cancellation-free form for w < 0).
//   2. Time reversal: the metric's a is -spin and the ray runs forward from the camera along +d. The chart is outgoing
//      Kerr-Schild of the hole's own spin +a (ruling R-P3-8): the scene's axes, spin along +z.
//   3. Hamiltonian, p_t = -1, L = 1 + l.p: dx/dl = p - f L l, dp/dl = L^2/2 grad f + f L grad L (closed-form gradients).
//   4. Far cameras (ruling R-P3-6): beyond |x| = 200 the ray runs straight to |x| = 199.99 and R_esc becomes 200; a line
//      missing that sphere escapes at once, bent toward the hole by (2/b)(1 - cos g).
//   5. Start: s = l.d, T = (1 + f s^2)/(sqrt(1 - f (1 - s^2)) - f s), E = T - f (T + s), p = (d + f (T + s) l) / E;
//      no root or E <= 0: captured.
//   6. Fates, at the camera and after every step, in this order: surface, captured (r < r+ + 0.01), escaped (r > R_esc),
//      step limit (400 steps).
//   7. Classical RK4, h = 0.05 max(r - r+, 0.5)(1 + r/20), capped at 2.0, r at the start of the step; k1 is reused.
//   8. Exit direction = normalize(dx/dl) at the stop.
// Step-limited rays orbit near the photon sphere, so they take the surroundings along their final momentum and read as the
// photon ring (ruling R-P3-7). All lengths in GM/c^2.

in vec2 v_uv;

uniform float u_spin;          // a = J c / (G M^2), 0 to 1, about +z
uniform vec3 u_camPos;         // the camera, GM/c^2
uniform vec3 u_camForward;     // the camera's view direction and screen axes (OrbitCamera), unit vectors
uniform vec3 u_camRight;
uniform vec3 u_camUp;
uniform vec2 u_tanHalf;        // tan of half the field of view, horizontally and vertically
uniform float u_escapeRadius;  // R_esc = max(60, 1.2 x the camera distance) (BlackHoleOptics.EscapeRadiusFor)
uniform float u_surfaceRadius; // a neutron star's surface (spin 0 only), GM/c^2; 0 for none
uniform vec3 u_surfaceColor;   // the surface's colour (HDR)
uniform int u_grid;            // the grid sphere r = 50 GM/c^2 (plan ruling P3-8): 1 = on; drawn from Task 10
uniform samplerCube u_env;     // the surroundings seen from the remnant (EnvironmentCapture)

out vec4 o_color;

const int MAX_STEPS = 400;
const float CAPTURE_MARGIN = 0.01;
const float MAX_STEP = 2.0;
const float FAR_CAMERA_RADIUS = 200.0;
const float FAR_START_RADIUS = 199.99;

const int FATE_NONE = -1;
const int FATE_CAPTURED = 0;
const int FATE_ESCAPED = 1;
const int FATE_HIT_SURFACE = 2;
const int FATE_STEP_LIMIT = 3;

// The Kerr-Schild r (BlackHoleOptics.KerrSchildR).
float ksR(float a2, vec3 x)
{
    float z2 = x.z * x.z;
    float w = dot(x, x) - a2;
    float sq = sqrt(w * w + 4.0 * a2 * z2);
    float r2 = w >= 0.0 ? 0.5 * (w + sq) : 2.0 * a2 * z2 / (sq - w);
    return sqrt(r2);
}

// Hamilton's equations (BlackHoleOptics.Derivatives); also returns r at x.
void derivs(float a, vec3 x, vec3 p, out vec3 dx, out vec3 dp, out float r)
{
    float a2 = a * a;
    float z2 = x.z * x.z;
    r = ksR(a2, x);
    float r2 = r * r;
    float q = r2 * r2 + a2 * z2;
    float s = r2 + a2;
    float f = 2.0 * r * r2 / q;
    vec3 l = vec3((r * x.x + a * x.y) / s, (r * x.y - a * x.x) / s, x.z / r);
    float bigL = 1.0 + dot(l, p);
    float fl = f * bigL;

    dx = p - fl * l;

    float r3q = r * r2 / q;
    vec3 dr = vec3(x.x * r3q, x.y * r3q, x.z * r * s / q);
    float cf = 2.0 * r2 / (q * q);
    float kf = 3.0 * a2 * z2 - r2 * r2;
    vec3 df = cf * vec3(kf * dr.x, kf * dr.y, kf * dr.z - 2.0 * a2 * r * x.z);
    float aa = (x.x * p.x + x.y * p.y - 2.0 * r * (l.x * p.x + l.y * p.y)) / s - x.z * p.z / r2;
    vec3 dl = aa * dr + vec3((r * p.x - a * p.y) / s, (a * p.x + r * p.y) / s, p.z / r);

    dp = 0.5 * bigL * bigL * df + fl * dl;
}

// One classical RK4 step of size h on (x, p), given k1 (BlackHoleOptics.Rk4Step).
void rk4(float a, float h, inout vec3 x, inout vec3 p, vec3 k1x, vec3 k1p)
{
    float hh = 0.5 * h;
    float rUnused;
    vec3 k2x;
    vec3 k2p;
    vec3 k3x;
    vec3 k3p;
    vec3 k4x;
    vec3 k4p;
    derivs(a, x + hh * k1x, p + hh * k1p, k2x, k2p, rUnused);
    derivs(a, x + hh * k2x, p + hh * k2p, k3x, k3p, rUnused);
    derivs(a, x + h * k3x, p + h * k3p, k4x, k4p, rUnused);
    float h6 = h / 6.0;
    x += h6 * (k1x + 2.0 * k2x + 2.0 * k3x + k4x);
    p += h6 * (k1p + 2.0 * k2p + 2.0 * k3p + k4p);
}

// The future-directed null momentum leaving x along unit d, p_t = -1 (BlackHoleOptics.InitialMomentum); false if none exists.
bool initialMomentum(float a, vec3 x, vec3 d, out vec3 p)
{
    float a2 = a * a;
    float r = ksR(a2, x);
    float r2 = r * r;
    float s = r2 + a2;
    float f = 2.0 * r * r2 / (r2 * r2 + a2 * x.z * x.z);
    vec3 l = vec3((r * x.x + a * x.y) / s, (r * x.y - a * x.x) / s, x.z / r);
    float sl = dot(l, d);
    float disc = 1.0 - f * (1.0 - sl * sl);
    float den = disc >= 0.0 ? sqrt(disc) - f * sl : -1.0;
    float t = den > 0.0 ? (1.0 + f * sl * sl) / den : 0.0;
    float e = t - f * (t + sl);
    p = vec3(0.0);
    if (!(den > 0.0) || !(e > 0.0)) return false;
    p = (d + f * (t + sl) * l) / e;
    return true;
}

// Ruling R-P3-6 (BlackHoleOptics.FarStart): true with the start on the sphere |x| = 199.99, or false with the weak-field-bent
// direction when the forward half of the line misses it.
bool farStart(vec3 c, vec3 d, out vec3 onSphere, out vec3 bent)
{
    float cd = dot(c, d);
    float c2 = dot(c, c);
    vec3 perp = c - cd * d;
    float b = length(perp);
    onSphere = c;
    bent = d;
    if (cd < 0.0 && b < FAR_START_RADIUS)
    {
        onSphere = c + (-cd - sqrt(FAR_START_RADIUS * FAR_START_RADIUS - b * b)) * d;
        return true;
    }
    float cosG = cd / sqrt(c2);
    float delta = cosG > 0.0 ? 2.0 * b / (c2 * (1.0 + cosG)) : 2.0 / b * (1.0 - cosG);
    if (b > 0.0) bent = normalize(cos(delta) * d - (sin(delta) / b) * perp);
    return false;
}

void main()
{
    vec2 ndc = v_uv * 2.0 - 1.0;
    vec3 d = normalize(u_camForward + ndc.x * u_tanHalf.x * u_camRight + ndc.y * u_tanHalf.y * u_camUp);

    float a = -u_spin;   // time reversal (item 2)
    float rPlus = 1.0 + sqrt(max(0.0, 1.0 - u_spin * u_spin));
    float rCapture = rPlus + CAPTURE_MARGIN;
    float escapeRadius = u_escapeRadius;
    vec3 x = u_camPos;
    vec3 dir = d;
    int fate = FATE_NONE;

    if (length(x) > FAR_CAMERA_RADIUS)
    {
        vec3 onSphere;
        vec3 bent;
        if (farStart(x, d, onSphere, bent))
        {
            x = onSphere;
            escapeRadius = FAR_CAMERA_RADIUS;
        }
        else
        {
            fate = FATE_ESCAPED;
            dir = bent;
        }
    }

    vec3 p = vec3(0.0);
    if (fate == FATE_NONE)
    {
        // Step 0 at the camera, before the momentum is set up (it does not exist inside the horizon).
        float r0 = ksR(a * a, x);
        if (u_surfaceRadius > 0.0 && r0 < u_surfaceRadius) fate = FATE_HIT_SURFACE;
        else if (r0 < rCapture) fate = FATE_CAPTURED;
        else if (r0 > escapeRadius) fate = FATE_ESCAPED;
        else if (!initialMomentum(a, x, d, p)) fate = FATE_CAPTURED;
    }

    if (fate == FATE_NONE)
    {
        for (int steps = 0; steps <= MAX_STEPS; steps++)
        {
            vec3 k1x;
            vec3 k1p;
            float r;
            derivs(a, x, p, k1x, k1p, r);
            dir = k1x;
            if (u_surfaceRadius > 0.0 && r < u_surfaceRadius) { fate = FATE_HIT_SURFACE; break; }
            if (r < rCapture) { fate = FATE_CAPTURED; break; }
            if (r > escapeRadius) { fate = FATE_ESCAPED; break; }
            if (steps >= MAX_STEPS) { fate = FATE_STEP_LIMIT; break; }
            float h = min(0.05 * max(r - rPlus, 0.5) * (1.0 + r / 20.0), MAX_STEP);
            rk4(a, h, x, p, k1x, k1p);
        }
        dir = normalize(dir);
    }

    vec3 color = vec3(0.0);
    if (fate == FATE_HIT_SURFACE) color = u_surfaceColor;
    else if (fate == FATE_ESCAPED || fate == FATE_STEP_LIMIT) color = texture(u_env, dir).rgb;
    o_color = vec4(color, 1.0);
}

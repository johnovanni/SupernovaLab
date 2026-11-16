#version 410 core
// Plan Task 9 (spec 3.2): the core's first second, ray-cast per pixel over one full-screen pair of triangles (shell.vert), drawn
// without the bending of light (a note says so). Front to back along each line of sight:
//   - the shock: a translucent surface at the mean radius u_shockR, distorted by a schematic low-order shape of relative size
//     u_amp (CoreCollapseHistory.DistortionFraction, an rms size): a sloshing l = 1 mode, a slowly turning spiral l = 1 mode, a fixed l = 2
//     bulge and a few convection bumps, all swinging with u_phase (one swing in CoreAnimation.SloshPeriodS = 4 s of wall time,
//     a shape change far below the 3 Hz flash limit). Found by marching the line through the shell between the radii
//     u_shockR (1 -+ SHAPE_SCALE u_amp), then bisecting each crossing;
//   - the gain radius: a faint undistorted shell at u_gainR (0: none);
//   - the proto-neutron star (or the core before collapse): an opaque sphere of radius u_sphereR, limb darkened.
// Translucent layers are brighter seen edge-on, as a thin glowing sheet is. Output is premultiplied (blend One, OneMinusSrcAlpha).
// Radii in scene units (km), centred on the origin.

in vec2 v_ndc;

uniform vec3 u_forward;       // the camera's view direction and screen axes (OrbitCamera.ScreenAxes), unit vectors
uniform vec3 u_right;
uniform vec3 u_up;
uniform vec2 u_tanHalf;       // tan of half the field of view, horizontally and vertically
uniform vec3 u_eye;           // the eye, scene units, relative to the centre

uniform float u_sphereR;      // the opaque sphere (0: none)
uniform vec3 u_sphereColor;   // its colour, HDR (brightness and exposure gain applied on the CPU)
uniform float u_gainR;        // the gain shell (0: none)
uniform vec3 u_gainColor;
uniform float u_gainAlpha;
uniform float u_shockR;       // the shock's mean radius (0: none)
uniform vec3 u_shockColor;
uniform float u_shockAlpha;
uniform float u_amp;          // the distortion's rms size, a fraction of u_shockR
uniform float u_phase;        // the sloshing's phase, radians

out vec4 o_color;

const int MARCH_STEPS = 48;
// The shape below has |g| <= 1 and an rms of about 0.3 over the sphere at the sloshing's peak; scaled by SHAPE_SCALE its rms
// deformation is about u_amp (Hanke et al. 2013's rms deformation, compact C1.1), its largest about 2.5 u_amp. Retuned after Task 9's
// launch, where an unscaled 10% read as round.
const float SHAPE_SCALE = 2.5;
const int BISECT_STEPS = 6;

// The schematic distortion, |g| <= 1 for a unit vector n.
float shape(vec3 n)
{
    vec3 a = normalize(vec3(0.35, 0.25, 1.0));                 // the sloshing axis
    vec3 e1 = normalize(cross(a, vec3(1.0, 0.0, 0.0)));
    vec3 e2 = cross(a, e1);
    vec3 spiral = cos(0.5 * u_phase) * e1 + sin(0.5 * u_phase) * e2;
    float slosh = sin(u_phase) * dot(n, a);
    float mu = dot(n, normalize(vec3(1.0, -0.4, 0.3)));
    float p2 = 0.5 * (3.0 * mu * mu - 1.0);                    // -0.5 to 1
    float drift = 0.25 * u_phase;
    float bumps = sin(5.0 * n.x + 1.3 + drift) * sin(5.0 * n.y + 0.7 - drift) * sin(5.0 * n.z + 2.1);
    return 0.45 * slosh + 0.2 * dot(n, spiral) + 0.2 * p2 + 0.15 * bumps;
}

// Signed distance-like function of the distorted shock: negative inside.
float shockF(vec3 p)
{
    float r = length(p);
    if (r < 1e-6) return -u_shockR;
    return r - u_shockR * (1.0 + u_amp * SHAPE_SCALE * shape(p / r));
}

vec3 shockNormal(vec3 p)
{
    float e = 0.01 * u_shockR;
    vec3 g = vec3(shockF(p + vec3(e, 0.0, 0.0)) - shockF(p - vec3(e, 0.0, 0.0)),
                  shockF(p + vec3(0.0, e, 0.0)) - shockF(p - vec3(0.0, e, 0.0)),
                  shockF(p + vec3(0.0, 0.0, e)) - shockF(p - vec3(0.0, 0.0, e)));
    float l = length(g);
    return l > 0.0 ? g / l : normalize(p);
}

// Where the line u_eye + t d meets the sphere of radius r: (near, far), or (1, -1) when it misses.
vec2 sphereHits(vec3 d, float r)
{
    float b = dot(u_eye, d);
    float c = dot(u_eye, u_eye) - r * r;
    float disc = b * b - c;
    if (disc < 0.0) return vec2(1.0, -1.0);
    float s = sqrt(disc);
    return vec2(-b - s, -b + s);
}

// Up to five layers along the line: distance, premultiplied colour, alpha.
float lt[5];
vec3 lc[5];
float la[5];
int count = 0;

void addLayer(float t, vec3 c, float a)
{
    if (count >= 5) return;
    int i = count;
    while (i > 0 && lt[i - 1] > t) { lt[i] = lt[i - 1]; lc[i] = lc[i - 1]; la[i] = la[i - 1]; i--; }
    lt[i] = t; lc[i] = c; la[i] = a;
    count++;
}

// A translucent sheet seen at this angle: brighter edge-on.
float sheet(float alpha, vec3 n, vec3 d)
{
    float rim = 1.0 - abs(dot(n, d));
    return alpha * (0.3 + 0.7 * rim * rim);
}

void main()
{
    vec3 d = normalize(u_forward + v_ndc.x * u_tanHalf.x * u_right + v_ndc.y * u_tanHalf.y * u_up);
    float tFar = 1e30;

    if (u_sphereR > 0.0)
    {
        vec2 h = sphereHits(d, u_sphereR);
        if (h.x <= h.y && h.y > 0.0)
        {
            float t = max(h.x, 0.0);
            vec3 n = normalize(u_eye + t * d);
            float mu = max(-dot(n, d), 0.0);
            addLayer(t, u_sphereColor * (0.35 + 0.65 * mu), 1.0);
            tFar = t;
        }
    }

    if (u_gainR > 0.0)
    {
        vec2 h = sphereHits(d, u_gainR);
        if (h.x <= h.y)
        {
            for (int k = 0; k < 2; k++)
            {
                float t = k == 0 ? h.x : h.y;
                if (t <= 0.0 || t >= tFar) continue;
                vec3 n = normalize(u_eye + t * d);
                float a = sheet(u_gainAlpha, n, d);
                addLayer(t, u_gainColor * a, a);
            }
        }
    }

    if (u_shockR > 0.0)
    {
        vec2 h = sphereHits(d, u_shockR * (1.0 + SHAPE_SCALE * u_amp) * 1.001);
        if (h.x <= h.y && h.y > 0.0)
        {
            float t0 = max(h.x, 0.0);
            float t1 = min(h.y, tFar);
            float prevT = t0;
            float prevF = shockF(u_eye + t0 * d);
            for (int i = 1; i <= MARCH_STEPS && t1 > t0; i++)
            {
                float t = t0 + (t1 - t0) * float(i) / float(MARCH_STEPS);
                float f = shockF(u_eye + t * d);
                if ((f < 0.0) != (prevF < 0.0))
                {
                    float lo = prevT, hi = t, flo = prevF;
                    for (int j = 0; j < BISECT_STEPS; j++)
                    {
                        float mid = 0.5 * (lo + hi);
                        float fm = shockF(u_eye + mid * d);
                        if ((fm < 0.0) == (flo < 0.0)) { lo = mid; flo = fm; } else hi = mid;
                    }
                    float th = 0.5 * (lo + hi);
                    vec3 n = shockNormal(u_eye + th * d);
                    float a = sheet(u_shockAlpha, n, d);
                    addLayer(th, u_shockColor * a, a);
                }
                prevT = t;
                prevF = f;
            }
        }
    }

    if (count == 0) discard;
    vec3 c = vec3(0.0);
    float a = 0.0;
    for (int i = 0; i < count; i++)
    {
        c += (1.0 - a) * lc[i];
        a += (1.0 - a) * la[i];
    }
    o_color = vec4(c, a);
}

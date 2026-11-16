#version 410 core
// The collapsar's accretion disk where a traced ray meets it (plan Task 11; spec section 6.2). Linked into lensing_shade.frag (the
// cached ray map's colouring, ruling R-P3-10) and lensing.frag (its direct path), which call diskShade with the light's redshift
// factor g and the radius r (GM/c^2) that lensing.frag found. This is the GLSL mirror of DiskRenderer (SupernovaLab.Rendering).
//
// Exact: g = 1/(u^t (1 - Omega l)) for gas on a prograde circular Kerr orbit (Bardeen, Press and Teukolsky 1972; computed in
// lensing.frag), and the brightness's g^4 (the bolometric intensity over nu^4 is the same all along a ray). Schematic: the radial
// profile (r_isco/r)^2 (1 - sqrt(r_isco/r)), 1 at its peak, and the colour, the blackbody colour (from a ramp of C# BlackbodyColor
// values) of g times a temperature proportional to profile^(1/4). Nothing here depends on the time, so nothing flickers.

uniform float u_diskInner;     // the inner edge, the ISCO of the drawn spin (GM/c^2)
uniform float u_diskLevel;     // the brightness: the fade in and out times the exposure's gain (0: not drawn)

const int RAMP_SIZE = 12;
uniform vec3 u_diskRamp[RAMP_SIZE];   // linear blackbody colours, RAMP_FIRST_K * 2^(k RAMP_STEP)

const float PROFILE_PEAK_ISCOS = 1.5625;
const float PEAK_TEMPERATURE_K = 6500.0;
const float RAMP_FIRST_K = 1000.0;
const float RAMP_STEP = 0.500000;

// The schematic profile, 1 at PROFILE_PEAK_ISCOS ISCO radii, 0 at and inside the ISCO (DiskRenderer.Profile).
float diskProfile(float r)
{
    if (!(u_diskInner > 0.0) || !(r >= u_diskInner)) return 0.0;
    float peakU = 1.0 / PROFILE_PEAK_ISCOS;
    float peak = peakU * peakU * (1.0 - sqrt(peakU));
    float u = u_diskInner / r;
    return u * u * (1.0 - sqrt(u)) / peak;
}

// The ramp's colour at this temperature, linear between entries, clamped (DiskRenderer.RampColor).
vec3 rampColor(float kelvin)
{
    float k = log2(max(kelvin, RAMP_FIRST_K) / RAMP_FIRST_K) / RAMP_STEP;
    k = clamp(k, 0.0, float(RAMP_SIZE - 1));
    int i = int(floor(k));
    int j = min(i + 1, RAMP_SIZE - 1);
    return mix(u_diskRamp[i], u_diskRamp[j], k - float(i));
}

// The disk's colour for light shifted by g from radius r (DiskRenderer.Color times the level).
vec3 diskShade(float g, float r)
{
    float kelvin = g * PEAK_TEMPERATURE_K * pow(diskProfile(r), 0.25);
    return rampColor(kelvin) * (g * g * g * g * diskProfile(r)) * u_diskLevel;
}

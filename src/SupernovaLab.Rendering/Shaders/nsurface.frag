#version 410 core
// The neutron star's surface where a traced ray hits it (plan Task 6; spec section 4.1). Linked into lensing_shade.frag (the
// cached ray map's colouring, ruling R-P3-10) and lensing.frag (its direct path), which call nsSurface with the hit point.
//
// The colour is the blackbody colour of the surface temperature seen from afar (BlackbodyColor in C#), tone-mapped by the
// caller (spec C12). A blackbody radiates the same intensity in every direction and the star is static, so the colour is
// uniform: no limb darkening is drawn. The schematic markings turn with the star: latitude and longitude lines every 30 degrees
// in the star's own (spinning) frame, faded near the spin poles, and a ring round each magnetic pole at the polar cap's
// angular radius (where the open field lines leave). The spin is about +z, the phase as the beams' (BeamClock); the magnetic
// axis is tilted by u_surfaceTilt from +z toward the star's own +x at phase 0, as Magnetosphere and the beams draw it.

uniform vec3 u_surfaceColor;   // the surface's colour (tone-mapped by the caller)
uniform float u_surfacePhase;  // the spin phase about +z, rad
uniform float u_surfaceTilt;   // the magnetic axis' tilt from the spin axis, rad
uniform float u_capCos;        // cos of the polar cap's angular radius
uniform int u_markings;        // 1: draw the schematic markings

const float PI = 3.14159265;
const float GRID_STEP = PI / 6.0;   // 30 degrees
const float LINE_WIDTH = 0.018;     // rad
const float CAP_WIDTH = 0.022;      // rad

// 1 on a grid line of this angle (every GRID_STEP), falling to 0 at the given half-width.
float gridLine(float angle, float halfWidth)
{
    float d = abs(fract(angle / GRID_STEP + 0.5) - 0.5) * GRID_STEP;
    return 1.0 - smoothstep(0.5 * halfWidth, halfWidth, d);
}

vec3 nsSurface(vec3 hit)
{
    vec3 color = u_surfaceColor;
    if (u_markings == 0) return color;
    vec3 n = normalize(hit);
    float c = cos(u_surfacePhase);
    float s = sin(u_surfacePhase);
    vec3 b = vec3(c * n.x + s * n.y, -s * n.x + c * n.y, n.z);   // the star's own frame: turned back by the phase
    float lat = asin(clamp(b.z, -1.0, 1.0));
    float lon = atan(b.y, b.x);
    float parallels = gridLine(lat, LINE_WIDTH);
    float meridians = gridLine(lon, LINE_WIDTH / max(cos(lat), 0.05)) * (1.0 - smoothstep(1.15, 1.35, abs(lat)));
    color *= 1.0 - 0.55 * max(parallels, meridians);

    vec3 m = vec3(sin(u_surfaceTilt), 0.0, cos(u_surfaceTilt));
    float fromPole = acos(clamp(abs(dot(b, m)), 0.0, 1.0));
    float cap = acos(clamp(u_capCos, -1.0, 1.0));
    float ring = 1.0 - smoothstep(0.5 * CAP_WIDTH, CAP_WIDTH, abs(fromPole - cap));
    float peak = max(max(u_surfaceColor.r, u_surfaceColor.g), u_surfaceColor.b);
    return mix(color, vec3(1.0, 0.8, 0.35) * peak, 0.75 * ring);
}

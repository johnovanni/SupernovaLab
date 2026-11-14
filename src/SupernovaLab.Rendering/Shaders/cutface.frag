#version 410 core
// The cut faces' colour (ruling R-P2-21; Task 16), per pixel. Mirrors CutFaceModel.At and Opacity (C#, single precision, the same
// hash and noise as FaceNoise) operation for operation: change one, change the other. Schematic. In face coordinates (a along y,
// b >= 0 along the face's outward axis, both over the debris' outer radius; r and psi = atan(a, b)):
//  - layers: boundary k at R_k (1 + A_k fbm1(psi)), A_k = min(cap_k, out + (in - out) W), W = 1 inside a cluster's chord, fading
//    beyond it: ragged, more so in the mixing cones, never past a neighbouring boundary (cap_k);
//  - nickel fingers (each cluster = one cone's chord across this face, 3 to 5 fingers): a wavy, ragged stalk from the finger root
//    up to a mushroom cap whose top is the tip (at most NickelMax);
//  - hydrogen tongues in the gaps between fingers, from the tongue root down to their tips (at least HydrogenMin).
// Nickel wins over hydrogen, which wins over the layer; a finger or tongue counts only where it intrudes into another layer.
// Once there are shocks the radii are the confined ones (as the particles), and the opacity reads the speed back through
// ParticleKinematics.FreeRadiusCm's inverse map (freeRadius below).

in vec3 v_world;
in vec2 v_face;                  // (a, b) over the debris' outer radius

uniform float u_extent;          // the debris' outer radius, scene units: nothing is drawn beyond it
uniform int u_plane;
uniform int u_layerCount;
uniform float u_layerOuter[6];   // outer radius of each layer over the outer radius, centre outward (the last is 1)
uniform float u_layerCap[6];     // each boundary's largest relative move
uniform vec3 u_layerColor[6];    // HDR colours, in the same order
uniform int u_nickelLayer;       // index of the nickel layer (-1: none)
uniform int u_hydrogenLayer;     // index of the hydrogen layer (-1: none)
uniform vec3 u_nickelColor;
uniform vec3 u_hydrogenColor;
uniform float u_fingerRoot;      // where the fingers start (over the outer radius)
uniform float u_tongueRoot;      // where the tongues hang from; negative: no tongues
uniform int u_clusterCount;      // clusters on this face
uniform vec4 u_cluster[10];      // centre (rad), half-width (rad), first finger, finger count
uniform vec4 u_finger[50];       // centre (rad), tip radius, widest stalk half-width (rad), seed
uniform vec4 u_tongue[40];       // centre (rad), tip radius, root half-width (rad), seed; cluster c's start at first finger - c
uniform float u_transitionRadius; // radius of the profile's break speed v_t, scene units
uniform float u_densityIndex;     // the profile's outer slope n
uniform float u_freeScale;        // K = R* + v_max (t - t_bo), scene units: the fastest particle's free radius
uniform float u_reverse;          // R_r and R_c, scene units, and ruling R-P2-4's weight (0: no shocks)
uniform float u_contact;
uniform float u_shockWeight;
uniform float u_shockRatio;       // s_m (ParticleKinematics.ShockedSpeedRatio); >= 1: nothing shocked

out vec4 o_color;

// CutFaceModel's presentation constants (the same values).
const float SPREAD = 0.7;
const float STALK_ROOT = 0.25;
const float NECK_START = 0.5;
const float STALK_NARROW = 0.3;
const float STALK_MAX_ANGLE = 0.42;
const float CAP_LENGTH = 0.07;
const float CAP_UNDER = 0.5;
const float CAP_WIDTH = 2.4;
const float WIGGLE_AMP = 0.1;
const float WIGGLE_FREQ = 2.5;
const float TONGUE_WIGGLE = 0.06;
const float TONGUE_MAX_ANGLE = 0.7;
const float EDGE_FREQ = 28.0;
const float RAGGED = 0.5;
const float BOUNDARY_FREQ = 7.0;
const float BOUNDARY_OUT = 0.02;
const float BOUNDARY_IN = 0.08;
const float BOUNDARY_FALLOFF = 0.25;
const uint EDGE_SEED = 17u;
const uint BOUNDARY_SEED = 101u;

// FaceNoise: lowbias32, 24-bit unit floats, smoothstep value noise and its fractal sums.
uint hash(uint x)
{
    x ^= x >> 16u;
    x *= 0x7feb352du;
    x ^= x >> 15u;
    x *= 0x846ca68bu;
    x ^= x >> 16u;
    return x;
}

float unit(uint n)
{
    return float(hash(n) >> 8u) * (1.0 / 16777216.0);
}

float value1(uint seed, float x)
{
    float fl = floor(x);
    int i = int(fl);
    float f = x - fl;
    float t = f * f * (3.0 - 2.0 * f);
    float a = 2.0 * unit(uint(i) + seed * 0x9e3779b1u) - 1.0;
    float b = 2.0 * unit(uint(i + 1) + seed * 0x9e3779b1u) - 1.0;
    return a + (b - a) * t;
}

float value2(uint seed, float x, float y)
{
    float fx = floor(x), fy = floor(y);
    int i = int(fx), j = int(fy);
    float f = x - fx;
    float tx = f * f * (3.0 - 2.0 * f);
    f = y - fy;
    float ty = f * f * (3.0 - 2.0 * f);
    float a = 2.0 * unit(uint(i) + hash(uint(j) + seed * 0x9e3779b1u)) - 1.0;
    float b = 2.0 * unit(uint(i + 1) + hash(uint(j) + seed * 0x9e3779b1u)) - 1.0;
    float c = 2.0 * unit(uint(i) + hash(uint(j + 1) + seed * 0x9e3779b1u)) - 1.0;
    float d = 2.0 * unit(uint(i + 1) + hash(uint(j + 1) + seed * 0x9e3779b1u)) - 1.0;
    float lo = a + (b - a) * tx;
    float hi = c + (d - c) * tx;
    return lo + (hi - lo) * ty;
}

float fbm1(uint seed, float x, int octaves)
{
    float sum = 0.0, amp = 1.0, norm = 0.0;
    for (int o = 0; o < octaves; o++)
    {
        sum += amp * value1(seed + uint(o), x);
        norm += amp;
        x *= 2.0;
        amp *= 0.5;
    }
    return sum / norm;
}

float fbm2(uint seed, float x, float y, int octaves)
{
    float sum = 0.0, amp = 1.0, norm = 0.0;
    for (int o = 0; o < octaves; o++)
    {
        sum += amp * value2(seed + uint(o), x, y);
        norm += amp;
        x *= 2.0;
        y *= 2.0;
        amp *= 0.5;
    }
    return sum / norm;
}

float sstep(float e0, float e1, float x)
{
    float t = clamp((x - e0) / (e1 - e0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

// CutFaceModel.InFinger: f = (centre, tip, widest stalk half-width (rad), seed).
bool inFinger(float r, float psi, vec4 f, float w, float slot, float rag)
{
    if (r > f.y || r < u_fingerRoot) return false;
    float s = (r - u_fingerRoot) / (f.y - u_fingerRoot);
    float centre = f.x + WIGGLE_AMP * w * value1(uint(f.w), s * WIGGLE_FREQ);
    float d = abs(psi - centre);
    float capR = CAP_LENGTH * f.y;
    float capC = f.y - capR;
    if (r >= capC - CAP_UNDER * capR)
    {
        float x = d / (CAP_WIDTH * f.z * rag);
        float y = (r - capC) / (r >= capC ? capR : CAP_UNDER * capR);
        if (x * x + y * y <= 1.0) return true;
    }
    if (r > capC) return false;
    float grow = STALK_ROOT + (1.0 - STALK_ROOT) * min(1.0, s / NECK_START);
    float neck = 1.0 - STALK_NARROW * max(0.0, s - NECK_START) / (1.0 - NECK_START);
    float hw = f.z * grow * neck * rag;
    return d <= min(hw, STALK_MAX_ANGLE * slot);
}

// CutFaceModel.InTongue: t = (centre, tip, half-width at the root, seed).
bool inTongue(float r, float psi, vec4 t, float w, float slot, float rag)
{
    if (r < t.y || r > u_tongueRoot) return false;
    float s = (r - t.y) / (u_tongueRoot - t.y);
    float centre = t.x + TONGUE_WIGGLE * w * value1(uint(t.w), s * WIGGLE_FREQ);
    float d = abs(psi - centre);
    float hw = t.z * (0.2 + 0.8 * s) * rag;
    return d <= min(hw, TONGUE_MAX_ANGLE * slot);
}

// ParticleKinematics.FreeRadiusCm: the free-expansion radius of the debris drawn at radius r (identity without shocks). Above
// s_m the drawn radius is min(confined line, free line) (ruling R-P2-33's clamp), so the inverse is the larger of the two inverses.
float freeRadius(float r)
{
    float sm = u_shockRatio;
    if (u_shockWeight <= 0.0 || u_freeScale <= 0.0 || sm >= 1.0) return r;
    float first = (1.0 - u_shockWeight) * sm * u_freeScale + u_shockWeight * u_reverse;   // the confined line at s_m+, unclamped
    float slope = (1.0 - u_shockWeight) * u_freeScale + u_shockWeight * (u_contact - u_reverse) / (1.0 - sm);
    if (slope > 0.0 && r >= min(first, sm * u_freeScale)) return max(r / u_freeScale, sm + (r - first) / slope) * u_freeScale;
    return min(r / u_freeScale, sm) * u_freeScale;
}

void main()
{
    float rho = length(v_world);
    float aa = max(fwidth(rho), 1e-6);
    float alpha = 1.0 - smoothstep(u_extent - aa, u_extent, rho);
    if (alpha <= 0.01) discard;

    float r = sqrt(v_face.x * v_face.x + v_face.y * v_face.y);
    float psi = atan(v_face.x, v_face.y);

    // Smeared boundaries.
    float weight = 0.0;
    for (int c = 0; c < u_clusterCount; c++)
        weight = max(weight, 1.0 - sstep(u_cluster[c].y, u_cluster[c].y + BOUNDARY_FALLOFF, abs(psi - u_cluster[c].x)));
    float amp = BOUNDARY_OUT + (BOUNDARY_IN - BOUNDARY_OUT) * weight;
    int layer = u_layerCount - 1;
    for (int k = 0; k < u_layerCount - 1; k++)
    {
        float R = u_layerOuter[k], A = min(amp, u_layerCap[k]);
        if (r <= R * (1.0 - A)) { layer = k; break; }
        if (r <= R * (1.0 + A) && r <= R * (1.0 + A * fbm1(BOUNDARY_SEED + uint(k), psi * BOUNDARY_FREQ, 3))) { layer = k; break; }
    }
    vec3 color = u_layerColor[layer];
    bool mixed = false;

    // Fingers, then tongues, where they intrude into another layer; the edge noise only where a cluster covers the pixel.
    if (u_clusterCount > 0 && layer != u_nickelLayer)
    {
        float rag = 0.0;
        for (int c = 0; c < u_clusterCount && !mixed; c++)
        {
            vec4 cl = u_cluster[c];
            if (abs(psi - cl.x) > cl.y) continue;
            if (rag == 0.0) rag = 1.0 + RAGGED * fbm2(EDGE_SEED + uint(u_plane), v_face.x * EDGE_FREQ, v_face.y * EDGE_FREQ, 2);
            int first = int(cl.z), count = int(cl.w);
            float slot = 2.0 * SPREAD * cl.y / float(count);
            for (int j = 0; j < count; j++)
                if (inFinger(r, psi, u_finger[first + j], cl.y, slot, rag)) { color = u_nickelColor; mixed = true; break; }
        }
        if (u_tongueRoot > 0.0 && layer != u_hydrogenLayer)
            for (int c = 0; c < u_clusterCount && !mixed; c++)
            {
                vec4 cl = u_cluster[c];
                if (abs(psi - cl.x) > cl.y) continue;
                int first = int(cl.z) - c, count = int(cl.w) - 1;
                float slot = 2.0 * SPREAD * cl.y / float(count + 1);
                for (int j = 0; j < count; j++)
                    if (inTongue(r, psi, u_tongue[first + j], cl.y, slot, rag)) { color = u_hydrogenColor; mixed = true; break; }
            }
    }

    // Ruling R-P2-23: opacity follows the density profile, 1 inside v_t and (v / v_t)^-n outside (free radius ~ speed); fingers
    // and tongues are opaque.
    float free = freeRadius(rho);
    if (!mixed && free > u_transitionRadius) alpha *= clamp(pow(free / u_transitionRadius, -u_densityIndex), 0.0, 1.0);
    o_color = vec4(color, alpha);   // the alpha is also the face mask the tone map reads
}

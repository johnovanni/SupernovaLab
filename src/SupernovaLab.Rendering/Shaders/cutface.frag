#version 410 core
// The cut faces' colour (ruling R-P2-21; Task 16; ruling R-P2-35), per pixel. Mirrors CutFaceModel.At and Opacity (C#, single
// precision, the same hash and noise as FaceNoise) operation for operation: change one, change the other. Schematic. In face
// coordinates (a along y, b >= 0 along the face's outward axis, both over the debris' outer radius; r and psi = atan(a, b)),
// Rayleigh-Taylor mixing at two scales:
//  - every layer boundary undulates slowly and carries small fingers both ways all around the face (the inner layer's outward
//    with mushroom heads, the outer layer's inward), alternating in cells along the face angle; a point is inside boundary k when
//    it is below the undulating base and not in an inward finger, or in an outward finger; neighbouring boundaries never meet;
//  - big nickel plumes from the physics cones (1 or 2 per cone's chord on this face, within BAND chord half-widths of its centre;
//    widths are arcs): a narrower base widening to a meandering, lumpy stalk that is clumpy at mid-length, ending in a mushroom
//    head about twice as wide with a rolled-back underside; the top is the tip (at most NickelMax). Each plume wears an
//    oxygen/silicon then a helium sheath; hydrogen tongues (with a helium rim) hang beside and between the plumes from the hydrogen
//    layer down to HydrogenMin at most.
// Precedence: a plume's nickel, a tongue's hydrogen, the plumes' sheaths, a tongue's helium rim, the layer. Nothing is mixed inside
// the nickel layer, and a plume, sheath or tongue counts only where it intrudes into another layer.
// Once there are shocks the radii are the confined ones (as the particles), and the opacity reads the speed back through
// ParticleKinematics.FreeRadiusCm's inverse map (freeRadius below).

in vec3 v_world;
in vec2 v_face;                  // (a, b) over the debris' outer radius

uniform float u_extent;          // the debris' outer radius, scene units: nothing is drawn beyond it
uniform int u_plane;
uniform int u_layerCount;
uniform float u_layerOuter[6];   // outer radius of each layer over the outer radius, centre outward (the last is 1)
uniform vec4 u_boundary[6];      // per interior boundary: radius, finger length outward, inward, cell period (rad)
uniform vec4 u_boundary2[6];     // per interior boundary: largest undulation outward, inward (relative), cell phase, unused
uniform vec3 u_layerColor[6];    // HDR colours, in the same order
uniform int u_nickelLayer;       // indices of the nickel, oxygen/silicon, helium and hydrogen layers (-1: none)
uniform int u_oxygenLayer;
uniform int u_heliumLayer;
uniform int u_hydrogenLayer;
uniform vec3 u_nickelColor;
uniform vec3 u_hydrogenColor;
uniform float u_fingerRoot;      // where the plumes start (over the outer radius)
uniform float u_tongueRoot;      // where the tongues hang from; negative: no tongues
uniform float u_tongueCut;       // tongues only below this radius
uniform int u_clusterCount;      // clusters on this face
uniform vec4 u_cluster[10];      // centre (rad), chord half-width (rad), first plume, plume count
uniform vec4 u_finger[20];       // centre (rad), tip radius, stalk half-width (arc), seed
uniform vec4 u_tongue[30];       // centre (rad), tip radius, root half-width (arc), seed; cluster c's start at first plume + c
uniform float u_transitionRadius; // radius of the profile's break speed v_t, scene units
uniform float u_densityIndex;     // the profile's outer slope n
uniform float u_freeScale;        // K = R* + v_max (t - t_bo), scene units: the fastest particle's free radius
uniform float u_reverse;          // R_r and R_c, scene units, and ruling R-P2-4's weight (0: no shocks)
uniform float u_contact;
uniform float u_shockWeight;
uniform float u_shockRatio;       // s_m (ParticleKinematics.ShockedSpeedRatio); >= 1: nothing shocked

out vec4 o_color;

// CutFaceModel's presentation constants (the same values).
const float FINGER_ASPECT = 2.0;
const float FINGER_HEAD = 1.35;
const float FINGER_HEAD_HEIGHT = 0.8;
const float FINGER_NARROW = 0.15;
const float FINGER_LEAN = 0.15;
const float FINGER_GAP = 0.2;
const float FINGER_JITTER = 0.08;
const float BAND = 2.0;
const float STALK_NARROW = 0.15;
const float STALK_ROOT = 0.3;
const float BASE_LENGTH = 0.45;
const float CAP_WIDTH = 2.0;
const float CAP_HEIGHT = 0.6;
const float CAP_DROOP0 = 0.35;
const float CAP_DROOP = 0.9;
const float MEANDER_AMP = 0.3;
const float MEANDER_FREQ = 1.3;
const float RIM_OXYGEN = 0.016;
const float RIM_HELIUM = 0.013;
const float TONGUE_MEANDER = 0.08;
const float EDGE_LOW_FREQ = 9.0;
const float EDGE_LOW_AMP = 0.3;
const float EDGE_HIGH_FREQ = 40.0;
const float EDGE_HIGH_AMP = 0.1;
const float BOUNDARY_FREQ = 3.0;
const float BOUNDARY_GAIN = 1.6;
const float BOUNDARY_OUT = 0.04;
const float BOUNDARY_IN = 0.08;
const float BOUNDARY_FALLOFF = 0.3;
const float CLUMP_FREQ = 20.0;
const float CLUMP_LEVEL = -0.2;
const float CLUMP_FROM = 0.05;
const float CLUMP_TO = 0.2;
const float CLUMP_SOLID_FROM = 0.55;
const float CLUMP_SOLID_TO = 0.9;
const uint EDGE_SEED = 17u;
const uint BOUNDARY_SEED = 101u;
const uint CLUMP_SEED = 29u;
const uint BOUNDARY_FINGER_SEED = 0x7a3c0000u;

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


// CutFaceModel.InHead: a mushroom head, in arcs: a dome above its widest line, the rolled-back underside below it.
bool inHead(float arc, float y, float W, float R, float capC)
{
    float x = arc / W;
    if (x >= 1.0) return false;
    if (y >= capC)
    {
        float t = (y - capC) / R;
        return x * x + t * t <= 1.0;
    }
    return capC - y <= R * (CAP_DROOP0 + CAP_DROOP * x * x) * sqrt(1.0 - x * x);
}

// CutFaceModel.InBoundaryFinger: a boundary finger (seed s) at arc offset x from its axis and height y above its base.
bool inBoundaryFinger(float x, float y, float len, uint s, float rag)
{
    if (!(len > 0.0) || unit(s + 4u) < FINGER_GAP) return false;
    float l = len * (0.5 + 0.5 * unit(s));
    float h = l / (2.0 * FINGER_ASPECT) * (0.8 + 0.25 * unit(s + 2u));
    x = abs(x - FINGER_LEAN * (2.0 * unit(s + 3u) - 1.0) * y);
    float capW = FINGER_HEAD * h * rag;
    float capH = FINGER_HEAD_HEIGHT * FINGER_HEAD * h;
    float capC = l - capH;
    if (y <= capC && x <= h * (1.0 - FINGER_NARROW * y / l) * rag) return true;
    return inHead(x, y, capW, capH, capC);
}

// CutFaceModel.InsideBoundary: whether the point is inside boundary k (in layer k or below).
bool insideBoundary(int k, float r, float psi, float amp, float rag)
{
    vec4 b = u_boundary[k];
    vec4 b2 = u_boundary2[k];
    float lo = min(amp, b2.y), hi = min(amp, b2.x);
    if (r <= b.x * (1.0 - lo) - b.z) return true;
    if (r > b.x * (1.0 + hi) + b.y) return false;
    float nn = clamp(BOUNDARY_GAIN * fbm1(BOUNDARY_SEED + uint(k), psi * BOUNDARY_FREQ, 3), -1.0, 1.0);
    float rb = b.x * (1.0 + (nn >= 0.0 ? hi : lo) * nn);
    if (!(b.w > 0.0)) return r <= rb;
    float u = psi / b.w + b2.z;
    if (r > rb)
    {
        float cell = floor(u);
        uint s = hash(BOUNDARY_FINGER_SEED + uint(k) * 7919u + uint(int(cell)));
        float x = (u - cell - 0.5 - FINGER_JITTER * (2.0 * unit(s + 1u) - 1.0)) * b.w * b.x;
        return inBoundaryFinger(x, r - rb, b.y, s, rag);
    }
    float j = floor(u + 0.5);
    uint s2 = hash(BOUNDARY_FINGER_SEED + 0x8000u + uint(k) * 7919u + uint(int(j)));
    float x2 = (u - j - FINGER_JITTER * (2.0 * unit(s2 + 1u) - 1.0)) * b.w * b.x;
    return !inBoundaryFinger(x2, rb - r, b.z, s2, rag);
}

// CutFaceModel.InPlume: f = (centre, tip, stalk half-width (arc), seed). 0 outside, 1 nickel, 2 oxygen sheath, 3 helium sheath.
int inPlume(float r, float psi, vec4 f, float w, float rag, float clump, float rimO, float rimHe)
{
    float rims = rimO + rimHe;
    if (r < u_fingerRoot || r > f.y * (1.0 + rims)) return 0;
    float s = clamp((r - u_fingerRoot) / (f.y - u_fingerRoot), 0.0, 1.0);
    float centre = f.x + MEANDER_AMP * w * s * value1(uint(f.w), s * MEANDER_FREQ);
    float arc = abs(psi - centre) * r;
    float stalk = f.z * (1.0 - STALK_NARROW * s) * rag;
    float hw = stalk * (STALK_ROOT + (1.0 - STALK_ROOT) * sstep(0.0, BASE_LENGTH, s));
    float capW = CAP_WIDTH * f.z * rag;
    float capR = CAP_HEIGHT * CAP_WIDTH * f.z;
    float capC = f.y - capR;
    float clumpy = sstep(CLUMP_FROM, CLUMP_TO, s) * (1.0 - sstep(CLUMP_SOLID_FROM, CLUMP_SOLID_TO, s));
    bool dense = clump > -2.0 + (CLUMP_LEVEL + 2.0) * clumpy;
    for (int level = 1; level <= 3; level++)
    {
        float ex = level == 1 ? 0.0 : level == 2 ? rimO : rims;
        if (level > 1 && ex == (level == 2 ? 0.0 : rimO)) continue;
        if (r <= capC && arc <= hw + ex * r && (level > 1 || dense)) return level;
        if (inHead(arc, r, capW + ex * r, capR + ex * f.y, capC)) return level;
    }
    return 0;
}

// CutFaceModel.InTongue: t = (centre, tip, root half-width (arc), seed). 0 outside, 1 hydrogen, 2 its helium rim.
int inTongue(float r, float psi, vec4 t, float w, float rag, float rimHe)
{
    if (r < t.y || r > u_tongueRoot) return 0;
    float s = (r - t.y) / (u_tongueRoot - t.y);
    float centre = t.x + TONGUE_MEANDER * w * value1(uint(t.w), s * MEANDER_FREQ);
    float arc = abs(psi - centre) * r;
    float hw = t.z * (0.15 + 0.85 * s) * rag;
    if (arc <= hw) return 1;
    return arc <= hw + rimHe * r ? 2 : 0;
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

    // The layer, with each boundary's undulation and fingers.
    float weight = 0.0;
    for (int c = 0; c < u_clusterCount; c++)
        weight = max(weight, 1.0 - sstep(u_cluster[c].y, u_cluster[c].y + BOUNDARY_FALLOFF, abs(psi - u_cluster[c].x)));
    float amp = BOUNDARY_OUT + (BOUNDARY_IN - BOUNDARY_OUT) * weight;
    float rag = 1.0 + EDGE_LOW_AMP * fbm2(EDGE_SEED + uint(u_plane), v_face.x * EDGE_LOW_FREQ, v_face.y * EDGE_LOW_FREQ, 2)
        + EDGE_HIGH_AMP * value2(EDGE_SEED + 8u + uint(u_plane), v_face.x * EDGE_HIGH_FREQ, v_face.y * EDGE_HIGH_FREQ);
    int layer = u_layerCount - 1;
    for (int k = 0; k < u_layerCount - 1; k++)
        if (insideBoundary(k, r, psi, amp, rag)) { layer = k; break; }
    int drawn = layer;   // the layer whose colour is drawn (100: a plume's nickel, 101: a tongue's hydrogen)
    bool mixed = false;

    if (u_clusterCount > 0 && layer != u_nickelLayer)
    {
        float rimO = u_oxygenLayer >= 0 ? RIM_OXYGEN : 0.0, rimHe = u_heliumLayer >= 0 ? RIM_HELIUM : 0.0;
        float clump = fbm2(CLUMP_SEED + uint(u_plane), v_face.x * CLUMP_FREQ, v_face.y * CLUMP_FREQ, 2);
        int level = 0;
        for (int c = 0; c < u_clusterCount; c++)
        {
            vec4 cl = u_cluster[c];
            float off = abs(psi - cl.x), band = BAND * cl.y;
            if (off > band + rimO + rimHe) continue;
            int first = int(cl.z), count = int(cl.w);
            for (int j = 0; j < count; j++)
            {
                int l = inPlume(r, psi, u_finger[first + j], cl.y, rag, clump, rimO, rimHe);
                if (l == 1 && off > band) l = rimO > 0.0 ? 2 : 3;
                if (l > 0 && (level == 0 || l < level)) level = l;
            }
        }
        int tongue = 0;
        if (level != 1 && u_tongueRoot > 0.0 && layer != u_hydrogenLayer && r <= u_tongueCut)
            for (int c = 0; c < u_clusterCount; c++)
            {
                vec4 cl = u_cluster[c];
                if (abs(psi - cl.x) > BAND * cl.y) continue;
                int first = int(cl.z) + c, count = int(cl.w) + 1;
                for (int j = 0; j < count; j++)
                {
                    int l = inTongue(r, psi, u_tongue[first + j], cl.y, rag, rimHe);
                    if (l > 0 && (tongue == 0 || l < tongue)) tongue = l;
                }
            }
        // Nickel, then a tongue's hydrogen, then the plumes' sheaths, then a tongue's helium rim; each only where it intrudes.
        if (level == 1) drawn = 100;
        else if (tongue == 1) drawn = 101;
        else if (level == 2) drawn = u_oxygenLayer;
        else if (level == 3 || tongue == 2) drawn = u_heliumLayer;
        mixed = drawn != layer;
    }
    vec3 color = drawn == 100 ? u_nickelColor : drawn == 101 ? u_hydrogenColor : u_layerColor[drawn];

    // Ruling R-P2-23: opacity follows the density profile, 1 inside v_t and (v / v_t)^-n outside (free radius ~ speed); plumes,
    // sheaths and tongues are opaque.
    float free = freeRadius(rho);
    if (!mixed && free > u_transitionRadius) alpha *= clamp(pow(free / u_transitionRadius, -u_densityIndex), 0.0, 1.0);
    o_color = vec4(color, alpha);   // the alpha is also the face mask the tone map reads
}

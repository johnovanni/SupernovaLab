#version 410 core
// The neutron star's cutaway (plan Task 7; spec section 4.2): a sphere of radius 1 (the star's radius) with the wedge x > 0, z > 0
// (the scene's axes; the spin is about +z) removed, ray-cast per pixel with no bending of light (ruling R-P3-19), drawn after the
// bloom over the lensed scene (whose star is hidden while the cutaway is on). The two flat cut faces are coloured by the layer at
// each radius (schematic colours, radii to scale); the neutron drip line is a ring on each face; the atmosphere is a thin
// bright line along the cut edge of the surface. Before the crust forms (u_layered = 0) the inside is one hot liquid, brighter
// toward the centre. Pixels the sphere does not cover are discarded; rays that cross the removed wedge without meeting a face are opaque black (the notch).

uniform vec3 u_eye;            // the camera, in radii
uniform vec3 u_forward;        // the camera's basis (unit vectors) and tan of half the field of view across and up
uniform vec3 u_right;
uniform vec3 u_up;
uniform vec2 u_tanHalf;
uniform vec2 u_viewport;       // pixels
uniform float u_radiiPerPx;    // one pixel's size across the star, in radii: the lines keep a least width in pixels
uniform int u_layered;         // 1: the crust has formed (layers and the drip line); 0: the hot uniform proto-neutron star
uniform float u_bound[5];      // the layers' outer radii over the star's: inner core, outer core, pasta, inner crust, outer crust (1)
uniform vec3 u_color[5];       // their colours (tone-mapped by the caller)
uniform vec3 u_dripColor;      // the drip line's colour
uniform float u_drip;          // the drip line's radius over the star's
uniform vec3 u_atmoColor;      // the atmosphere line's colour
uniform vec3 u_hotColor;       // the proto-neutron star's colour at its centre
uniform vec3 u_surfaceColor;   // the surface's colour (the blackbody colour, tone-mapped)

out vec4 o_color;

const float BOUNDARY_WIDTH = 0.004;   // radii, the dark line between layers; at least 1 pixel (NeutronStarCutawayRenderer.BoundaryMinPx)
const float DRIP_WIDTH = 0.007;       // radii, the drip ring; at least 2 pixels
const float ATMOSPHERE_WIDTH = 0.007; // radii; at least 1.5 pixels

// The interval of t over which the ray is inside the half-space e + t d > 0.
vec2 positive(float e, float d)
{
    const float BIG = 1e30;
    if (abs(d) < 1e-9) return e > 0.0 ? vec2(-BIG, BIG) : vec2(BIG, -BIG);
    float root = -e / d;
    return d > 0.0 ? vec2(root, BIG) : vec2(-BIG, root);
}

vec3 layerColor(float r, float ring)
{
    vec3 c = u_color[4];
    if (r < u_bound[0]) c = u_color[0];
    else if (r < u_bound[1]) c = u_color[1];
    else if (r < u_bound[2]) c = u_color[2];
    else if (r < u_bound[3]) c = u_color[3];
    // A thin dark line at each boundary between layers.
    float bw = max(BOUNDARY_WIDTH, 1.0 * u_radiiPerPx);
    float edge = 0.0;
    for (int i = 0; i < 4; i++) edge = max(edge, 1.0 - smoothstep(0.5 * bw, bw, abs(r - u_bound[i])));
    c *= 1.0 - 0.6 * edge;
    // The atmosphere, a line along the edge of the cut face (the drip line is drawn over it: it lies only a few pixels below the surface).
    float aw = max(ATMOSPHERE_WIDTH, 1.5 * u_radiiPerPx);
    float atmo = smoothstep(1.0 - aw, 1.0 - 0.5 * aw, r);
    c = mix(c, u_atmoColor, atmo);
    // The drip line, over everything.
    float dw = max(DRIP_WIDTH, 2.0 * u_radiiPerPx);
    float drip = 1.0 - smoothstep(0.6 * dw, dw, abs(r - u_drip));
    c = mix(c, u_dripColor, drip);
    return c;
}

void main()
{
    vec2 ndc = 2.0 * gl_FragCoord.xy / u_viewport - 1.0;
    vec3 d = normalize(u_forward + u_right * (ndc.x * u_tanHalf.x) + u_up * (ndc.y * u_tanHalf.y));
    float b = dot(u_eye, d);
    float disc = b * b - (dot(u_eye, u_eye) - 1.0);
    if (disc < 0.0) discard;
    float root = sqrt(disc);
    float t0 = -b - root;
    float t1 = -b + root;

    // The removed wedge: x > 0 and z > 0.
    vec2 wx = positive(u_eye.x, d.x);
    vec2 wz = positive(u_eye.z, d.z);
    float wa = max(wx.x, wz.x);
    float wb = min(wx.y, wz.y);
    bool inWedge = wa < wb && wa < t0 && t0 < wb;

    vec3 color;
    if (inWedge)
    {
        // The ray enters the sphere inside the removed wedge: it sees nothing until it leaves the wedge through a cut face.
        if (wb >= t1) { o_color = vec4(0.0, 0.0, 0.0, 1.0); return; }   // the notch: nothing behind it but the black
        vec3 p = u_eye + wb * d;
        float r = length(p);
        // Which face: the upper bound came from x (the plane x = 0) or from z (the plane z = 0).
        bool onX = wx.y <= wz.y;
        float shade = onX ? 1.0 : 0.82;
        color = (u_layered == 1 ? layerColor(r, 0.0) : u_hotColor * (1.25 - 0.55 * r)) * shade;
    }
    else
    {
        vec3 p = u_eye + t0 * d;
        float lit = 0.55 + 0.45 * clamp(dot(normalize(p), -d), 0.0, 1.0);
        color = u_surfaceColor * lit;
    }
    o_color = vec4(color, 1.0);
}

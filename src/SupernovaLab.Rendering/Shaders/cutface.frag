#version 410 core
// The cut faces' colour (ruling R-P2-21), per pixel: the radius on the face selects the element layer (the particle set's
// layer boundaries), then the finger cones: hydrogen mixed inward (u_hydrogenMin up to the hydrogen layer's inner boundary), nickel mixed
// outward (the nickel layer's boundary up to u_nickelMax; nickel wins). Mirrors CutFaceModel.LayerAt and Opacity (C#): change one,
// change the other. Schematic. Once there are shocks the radii are the confined ones (as the particles), and the opacity reads the
// speed back through ParticleKinematics.FreeRadiusCm's inverse map (freeRadius below).

in vec3 v_world;

uniform float u_extent;          // the debris' outer radius, scene units: nothing is drawn beyond it
uniform int u_layerCount;
uniform float u_layerOuter[6];   // outer radius of each layer, centre outward, scene units
uniform vec3 u_layerColor[6];    // HDR colours, in the same order
uniform vec3 u_nickelColor;
uniform vec3 u_hydrogenColor;
uniform float u_nickelMax;       // radius of the nickel finger tips; negative: no nickel fingers
uniform float u_nickelInner;     // outer boundary of the nickel layer: a nickel finger starts here
uniform float u_hydrogenInner;   // inner boundary of the hydrogen layer: a hydrogen finger ends here (negative: none)
uniform float u_hydrogenMin;     // radius the hydrogen fingers reach inward to; negative: none
uniform int u_coneCount;
uniform vec3 u_coneAxis[10];
uniform float u_coneCos;
uniform float u_transitionRadius; // radius of the profile's break speed v_t, scene units
uniform float u_densityIndex;     // the profile's outer slope n
uniform float u_freeScale;        // K = R* + v_max (t - t_bo), scene units: the fastest particle's free radius
uniform float u_reverse;          // R_r and R_c, scene units, and ruling R-P2-4's weight (0: no shocks)
uniform float u_contact;
uniform float u_shockWeight;
uniform float u_shockRatio;       // s_m (ParticleKinematics.ShockedSpeedRatio); >= 1: nothing shocked

out vec4 o_color;

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
    float edge = max(fwidth(rho), 1e-6);
    float a = 1.0 - smoothstep(u_extent - edge, u_extent, rho);
    if (a <= 0.01) discard;
    float alpha = a;
    vec3 c = u_layerColor[u_layerCount - 1];
    for (int k = 0; k < u_layerCount; k++)
        if (rho <= u_layerOuter[k]) { c = u_layerColor[k]; break; }
    bool finger = false;
    if (u_coneCount > 0)
    {
        vec3 d = v_world / max(rho, 1e-9);
        bool inCone = false;
        for (int k = 0; k < u_coneCount; k++)
            if (dot(d, u_coneAxis[k]) >= u_coneCos) inCone = true;
        if (inCone)
        {
            if (u_nickelMax > 0.0 && rho > u_nickelInner && rho <= u_nickelMax) { c = u_nickelColor; finger = true; }
            else if (u_hydrogenMin > 0.0 && rho >= u_hydrogenMin && rho <= u_hydrogenInner) { c = u_hydrogenColor; finger = true; }
        }
    }
    // Ruling R-P2-23: opacity follows the density profile, 1 inside v_t and (v / v_t)^-n outside (free radius ~ speed); fingers
    // are opaque.
    float free = freeRadius(rho);
    if (!finger && free > u_transitionRadius) alpha *= clamp(pow(free / u_transitionRadius, -u_densityIndex), 0.0, 1.0);
    o_color = vec4(c, alpha);   // the alpha is also the face mask the tone map reads
}

#version 410 core
// u_mode 0, the star: a blackbody-coloured disk with schematic linear limb darkening, I(mu) / I(1) = 1 - 0.6 (1 - mu).
// u_mode 1, the gas around the star: a translucent haze whose opacity follows the path length through a uniform ball,
// sqrt(1 - r^2), plus a faint rim so its edge reads as a shell (schematic).
//
// The wedge cutaway (u_cut = 1, mode 0 only; plan rulings P7 and P8). The disc is an impostor of a sphere, so each pixel's
// front point on the sphere is p = right * x + up * y + back * mu (units of the star's radius; scene axes, z up). The removed
// quarter is x > 0 and z > 0. A pixel whose front point is not in it shows the star's surface (u_pass 0). A pixel whose front
// point is in it looks through the gap: the ray p - back * s leaves the quarter through the plane x = 0 or z = 0 (a cut face)
// unless it reaches the far side first (then there is nothing to see). The face is painted in u_pass 1, after the bloom, with the
// layer discs of the presupernova star (nested spheres, drawn only where wider than a pixel; structure section S6) and the shock front:
// a bright ring at u_shockR, with the gas inside it brighter (structure section S5).

in vec2 v_corner;

uniform vec3 u_color;
uniform int u_mode;
uniform float u_limbU;           // the limb-darkening coefficient u (StarRenderer.LimbDarkeningU)
uniform float u_alpha;

uniform int u_cut;
uniform int u_pass;
uniform vec3 u_back;            // unit vector from the star to the camera
uniform vec3 u_right;
uniform vec3 u_up;
uniform vec3 u_wedgeX;          // the wedge frame (WedgeOrientation)
uniform vec3 u_wedgeZ;
uniform int u_layerCount;
uniform float u_layerOuter[6];  // outer radius of each layer, centre outward, in units of the star's radius
uniform vec3 u_layerColor[6];   // HDR colours
uniform float u_shockR;         // the shock's radius in units of the star's radius; negative when there is none
uniform vec3 u_ringColor;
uniform float u_insideBoost;    // gas inside the shock is this much brighter

out vec4 o_color;

void main()
{
    float r = length(v_corner);
    float edge = max(fwidth(r), 1e-4);
    float a = 1.0 - smoothstep(1.0 - edge, 1.0, r);
    if (a <= 0.0) discard;
    float mu = sqrt(max(0.0, 1.0 - r * r));
    float px = max(fwidth(v_corner.x), fwidth(v_corner.y));   // one pixel, in disc units
    if (u_mode == 1)
    {
        float rim = smoothstep(0.85, 0.98, r);
        o_color = vec4(u_color, a * u_alpha * (0.6 * mu + 0.6 * rim));
        return;
    }
    if (u_cut != 0)
    {
        vec3 p = u_right * v_corner.x + u_up * v_corner.y + u_back * mu;
        vec3 pw = vec3(dot(p, u_wedgeX), p.y, dot(p, u_wedgeZ));     // the point and the view vector in the wedge frame
        vec3 bw = vec3(dot(u_back, u_wedgeX), u_back.y, dot(u_back, u_wedgeZ));
        if (pw.x > 0.0 && pw.z > 0.0)
        {
            if (u_pass == 0) discard;                   // faces are painted later
            float sx = bw.x > 1e-6 ? pw.x / bw.x : 1e9;
            float sz = bw.z > 1e-6 ? pw.z / bw.z : 1e9;
            float s = min(sx, sz);
            if (s > 2.0 * mu) discard;                  // through the gap to the far side: nothing there
            vec3 q = p - u_back * s;
            float rho = length(q);
            int idx = u_layerCount - 1;
            for (int k = 0; k < u_layerCount; k++)
                if (u_layerOuter[k] >= rho && (u_layerOuter[k] >= px || k == u_layerCount - 1)) { idx = k; break; }
            vec3 c = u_layerColor[idx];
            if (u_shockR > 0.0)
            {
                float width = max(0.012, 1.5 * px);
                if (rho < u_shockR) c *= u_insideBoost;
                c = mix(u_ringColor, c, smoothstep(0.0, width, abs(rho - u_shockR)));
            }
            o_color = vec4(c, a);
            return;
        }
        if (u_pass == 1) discard;
    }
    float limb = 1.0 - u_limbU * (1.0 - mu);
    o_color = vec4(u_color * limb, a);
}

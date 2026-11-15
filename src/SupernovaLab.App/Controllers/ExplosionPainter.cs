using System.Windows;
using SupernovaLab.Core.Structure;
using SupernovaLab.Rendering;
using static SupernovaLab.App.Controllers.SceneController;

namespace SupernovaLab.App.Controllers;

/// <summary>
/// Draws the explosion's matter of one <see cref="SceneState"/> (Project 2's scene: the gas around the star, the star, the debris
/// particles, the pulsar wind nebula and the shock shells) with any camera. The main view draws it with its own camera
/// (<see cref="SceneController"/>), and the Close-up draws it six times into its environment cube map from the remnant's position
/// (<see cref="EnvironmentCapture"/>, plan ruling P3-6), each window with its own painter in its own GL context. The colours and
/// intensities are <see cref="SceneController"/>'s. Create and use with the GL context current.
/// </summary>
public sealed class ExplosionPainter : IDisposable
{
    /// <param name="particleCapacity">The most particles the painter can hold (the main view: High's 200,000; the Close-up: Low's 50,000).</param>
    public ExplosionPainter(int particleCapacity)
    {
        Star = new StarRenderer();
        Particles = new ParticleRenderer(particleCapacity);
        Shells = new ShellRenderer();
    }

    public StarRenderer Star { get; }
    public ParticleRenderer Particles { get; }
    public ShellRenderer Shells { get; }

    /// <summary>The particle set on the GPU now (its v_max scales the shader's positions); null when there is none.</summary>
    public ParticleSet? Uploaded { get; private set; }

    /// <summary>Replaces the particles on the GPU with this set (null: none).</summary>
    public void Upload(ParticleSet? set)
    {
        Uploaded = set;
        if (set is null) Particles.Upload(Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>(), 0);
        else Particles.Upload(set.Velocity, set.Layer, set.Seed, set.Count);
    }

    /// <summary>
    /// Draws the state's matter, in scene units of <paramref name="unit"/> cm, with the wedge cutaway's quarter dropped from the star
    /// (<paramref name="starCut"/>), the particles (<paramref name="particleCut"/>) and the shells (<paramref name="cutaway"/>), the wedge
    /// at <paramref name="wedgeAngleRad"/>. The cut faces themselves are the main view's (drawn after its bloom).
    /// </summary>
    public void Paint(OrbitCamera camera, Size viewportPx, double pxPerDip, SceneState s, double unit, bool starCut = false,
        bool particleCut = false, bool cutaway = false, double wedgeAngleRad = 0)
    {
        Star.WedgeAngleRad = Particles.WedgeAngleRad = wedgeAngleRad;
        Shells.WedgeAngleRad = wedgeAngleRad;
        Shells.Cutaway = cutaway;
        if (s.Surroundings is not null) DrawSurroundings(camera, viewportPx, s, unit);
        if (s.ShowStar) Star.Render(camera, viewportPx, s.StarRadiusCm / unit, s.StarColor, starCut);
        if (s.ShowParticles && Uploaded is { } set)
        {
            // The brightness is Project 1's, spread over the particle count so the picture keeps its overall level.
            // Positions follow ParticleKinematics.RadiusCm: the shader gets R*, v_max * (t - t_bo) and the shock radii in
            // scene units (doubles divided by the unit before they become floats).
            float perParticle = ParticleGain * ReferenceParticleCount / Particles.Count;
            double starCm = double.IsFinite(s.StarRadiusCm) ? s.StarRadiusCm : 0;
            var shocks = s.Shocks;
            var hot = ExplosionScene.HotGasColor;
            Particles.Render(camera, viewportPx, pxPerDip, new ParticleUniforms((float)(starCm / unit),
                (float)(set.MaxCmS * s.SinceBreakoutS / unit),
                new OpenTK.Mathematics.Vector3(s.EjectaColor.R, s.EjectaColor.G, s.EjectaColor.B), s.EjectaBrightness * perParticle,
                particleCut, shocks is not null, (float)((shocks?.ReverseCm ?? 0) / unit), (float)((shocks?.ContactCm ?? 0) / unit),
                (float)(shocks?.ConfinementWeight ?? 0), new OpenTK.Mathematics.Vector3(hot.R, hot.G, hot.B),
                (float)ParticleKinematics.ShockedSpeedRatio(shocks, s.SinceBreakoutS, starCm, set.MaxCmS)));
        }
        if (double.IsFinite(s.NebulaRadiusCm) && s.NebulaRadiusCm > 0)
            Shells.Render(camera, viewportPx, ShellKind.Ball, 0, s.NebulaRadiusCm / unit, NebulaColor, NebulaIntensity, NebulaCover);
        if (s.Shocks is { } sh) DrawShells(camera, viewportPx, sh, unit);
    }

    /// <summary>Project 1's point count: its brightness was tuned for 50,000 points, so it is scaled by this over the count drawn.</summary>
    private const int ReferenceParticleCount = 50_000;

    /// <summary>
    /// The shock shells (structure §S4, plan ruling P8): the swept-up gas between the contact surface and the forward shock, and
    /// the reverse shock as a thin shell at R_r, each faded in with ruling R-P2-4's weight so they do not pop in when the shocks
    /// start. Radii in scene units (cm divided by the unit before they become floats).
    /// </summary>
    private void DrawShells(OrbitCamera camera, Size viewportPx, ShockRadii shocks, double unit)
    {
        float w = (float)Math.Clamp(shocks.ConfinementWeight, 0, 1);
        if (!(w > 0)) return;
        Shells.Render(camera, viewportPx, ShellKind.Shell, shocks.ContactCm / unit, shocks.ForwardCm / unit, ForwardShellColor,
            ForwardShellIntensity * w);
        if (!(shocks.ReverseCm > 0)) return;
        double pxUnits = 2 * camera.DistanceMpc * Math.Tan(camera.FieldOfViewDeg * Math.PI / 360) / viewportPx.Height;
        double rOut = shocks.ReverseCm / unit;
        double thick = Math.Min(rOut, Math.Max(ReverseShellFraction * rOut, ReverseShellMinPx * pxUnits));
        Shells.Render(camera, viewportPx, ShellKind.Shell, rOut - thick, rOut, ExplosionScene.HotGasColor, ReverseShellIntensity * w);
    }

    /// <summary>
    /// The gas around the star (structure §S7, plan ruling P8): the wind haze (ρ ∝ 1/r², R* out to the wind's outer extent) drawn only
    /// outside the forward shock, the dense shell (uniform, R* to its outer radius, flaring with the shock's glow) likewise, and
    /// at the forward shock a thin lit shell scaled by the glow. All clip to the removed quarter in the cutaway (the shell
    /// shader). Radii in scene units (cm over the unit). The haze's brightness is pinned to its column at b = 2 R*, since the
    /// shader normalizes by the largest column (at the wind's far edge), which is meaningless against the star.
    /// </summary>
    private void DrawSurroundings(OrbitCamera camera, Size viewportPx, SceneState s, double unit)
    {
        var csm = s.Surroundings!;
        double forwardCm = s.Shocks?.ForwardCm ?? 0;
        float glow = s.SurroundingsGlow;
        double rOut = csm.WindOuterCm / unit;
        if (csm.WindAStar > 0 && csm.WindOuterCm > csm.WindInnerCm && csm.WindInnerCm > 0)
        {
            double inner = Math.Max(csm.WindInnerCm, forwardCm) / unit;
            if (inner < rOut)
            {
                // intensity such that the column through the whole wind at b = 2 R* has this brightness.
                double bRef = 2 * csm.WindInnerCm / unit;
                double reference = ColumnDensity.Wind(bRef, csm.WindInnerCm / unit, rOut);
                double peak = ColumnDensity.Peak(ShellKind.Wind, inner, rOut);
                if (reference > 0 && peak > 0)
                {
                    float target = (float)(HazeIntensityAtTenAStar * HazeScale(csm.WindAStar));
                    Shells.Render(camera, viewportPx, ShellKind.Wind, inner, rOut, HazeColor, (float)(target * peak / reference));
                }
            }
        }
        if (csm.ShellOuterCm is { } shellOuterCm && csm.ShellInnerCm is { } shellInnerCm)
        {
            double inner = Math.Max(shellInnerCm, forwardCm) / unit, outer = shellOuterCm / unit;
            if (inner < outer)
                Shells.Render(camera, viewportPx, ShellKind.Shell, inner, outer, DenseShellColor, DenseShellIntensity * (1 + DenseShellFlare * s.ShellFlare));
        }
        if (s.Shocks is { } shocks && glow > 0 && shocks.ForwardCm > 0)
        {
            bool inDense = csm.ShellOuterCm is { } o && shocks.ForwardCm < o;
            bool inWind = csm.WindAStar > 0 && shocks.ForwardCm < csm.WindOuterCm;
            if (!inDense && !inWind) return;
            double rf = shocks.ForwardCm / unit;
            float w = (float)Math.Clamp(shocks.ConfinementWeight, 0, 1);
            Shells.Render(camera, viewportPx, ShellKind.Shell, rf * (1 - GlowShellFraction), rf, GlowColor,
                GlowShellIntensity * glow * w * (inDense ? DenseGlowGain : 1));
        }
    }

    public void Dispose()
    {
        Shells.Dispose();
        Particles.Dispose();
        Star.Dispose();
    }
}

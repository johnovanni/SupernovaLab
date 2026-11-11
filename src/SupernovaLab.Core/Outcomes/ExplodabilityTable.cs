using System.Globalization;

namespace SupernovaLab.Core.Outcomes;

/// <summary>What the collapsing core leaves behind.</summary>
public enum CollapseResult { NeutronStar, FallbackBlackHole, DirectBlackHole }

/// <summary>
/// One progenitor model of the solar-metallicity explodability table (outcomes §1.2–§1.4). Masses in M☉, energy in erg,
/// radius in cm. <see cref="RemnantBaryonicMass"/> is the NS baryonic mass, the fallback-BH mass, or for a failed
/// explosion the He-core mass (the S16 default BH mass, outcomes §1.5). <see cref="HydrogenEnvelopeMass"/> is the
/// H-rich envelope mass M_f − M_He (outcomes §1.3), not the mass of hydrogen itself.
/// </summary>
public sealed record ExplodabilityRow(double InitialMass, bool Explodes, CollapseResult Result,
    double RemnantBaryonicMass, double ExplosionEnergyErg, double NickelMass, double? Compactness25,
    double PreSnMass, double HeCoreMass, double CoCoreMass, double HydrogenEnvelopeMass, double RadiusCm);

/// <summary>
/// Per-mass supernova outcomes for single stars at solar metallicity, read from the embedded
/// <c>explodability-s16.csv</c> (provenance in tools/explodability/README.md). Rows are sorted by initial mass.
/// </summary>
public sealed class ExplodabilityTable
{
    private const string ResourceSuffix = "Data.Embedded.explodability-s16.csv";
    private const string SourcePrefix = "# source:";
    private const int ColumnCount = 12;

    private readonly ExplodabilityRow[] _rows;

    private ExplodabilityTable(string sourceDescription, ExplodabilityRow[] rows)
    {
        SourceDescription = sourceDescription;
        _rows = rows;
    }

    /// <summary>The citation and data description from the CSV's first line.</summary>
    public string SourceDescription { get; }

    public IReadOnlyList<ExplodabilityRow> Rows => _rows;

    public static ExplodabilityTable LoadEmbedded()
    {
        var assembly = typeof(ExplodabilityTable).Assembly;
        string name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    /// <summary>
    /// The row with the nearest initial mass (the lower one on a tie). Masses outside the table clamp to the first or
    /// last row; that is a safety net only, since callers consult the table only inside 9–120 M☉ (ruling R1).
    /// </summary>
    public ExplodabilityRow Lookup(double initialMass)
    {
        if (initialMass <= _rows[0].InitialMass) return _rows[0];
        if (initialMass >= _rows[^1].InitialMass) return _rows[^1];

        int upper = 1;
        while (_rows[upper].InitialMass < initialMass) upper++;
        var below = _rows[upper - 1];
        var above = _rows[upper];
        return above.InitialMass - initialMass < initialMass - below.InitialMass ? above : below;
    }

    private static ExplodabilityTable Parse(TextReader reader)
    {
        string first = reader.ReadLine() ?? throw new InvalidDataException("Explodability CSV is empty.");
        if (!first.StartsWith(SourcePrefix, StringComparison.Ordinal))
            throw new InvalidDataException("Explodability CSV must start with a '# source:' line.");
        string source = first[SourcePrefix.Length..].Trim();

        reader.ReadLine(); // column header row

        var rows = new List<ExplodabilityRow>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            string[] f = line.Split(',');
            if (f.Length != ColumnCount)
                throw new InvalidDataException($"Explodability CSV row has {f.Length} columns, expected {ColumnCount}: {line}");

            rows.Add(new ExplodabilityRow(
                InitialMass: D(f[0]),
                Explodes: bool.Parse(f[1]),
                Result: Enum.Parse<CollapseResult>(f[2]),
                RemnantBaryonicMass: D(f[3]),
                ExplosionEnergyErg: D(f[4]),
                NickelMass: D(f[5]),
                Compactness25: f[6].Length == 0 ? null : D(f[6]),
                PreSnMass: D(f[7]),
                HeCoreMass: D(f[8]),
                CoCoreMass: D(f[9]),
                HydrogenEnvelopeMass: D(f[10]),
                RadiusCm: D(f[11])));
        }

        if (rows.Count == 0) throw new InvalidDataException("Explodability CSV has no data rows.");
        return new ExplodabilityTable(source, rows.OrderBy(r => r.InitialMass).ToArray());
    }

    private static double D(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}

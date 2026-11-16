using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Services;

/// <summary>
/// The panel states the app remembers across launches (Task 15): which panels are folded and what is expanded. Every field has its
/// default, so a file with some fields missing, or from an older version, still loads.
/// </summary>
public sealed record LayoutSettings
{
    public bool LabPanelFolded { get; init; }
    public bool OutcomeColumnFolded { get; init; }
    public bool FocusView { get; init; }
    public bool KeyFolded { get; init; }
    public bool NotesExpanded { get; init; }
    public bool CaptionCollapsed { get; init; }

    /// <summary>
    /// The Close-up's light-bending detail (Task 4b, ruling R-P3-9), stored by name ("Auto", "Full", "Half", "Quarter"); any other
    /// stored value reads as Auto without disturbing the rest of the layout.
    /// </summary>
    [JsonConverter(typeof(LensingDetailJsonConverter))]
    public LensingDetail CloseUpLensingDetail { get; init; }
}

/// <summary>Writes a <see cref="LensingDetail"/> by name and reads any JSON value, falling back to Auto (<see cref="LensingDetails.Parse"/>).</summary>
public sealed class LensingDetailJsonConverter : JsonConverter<LensingDetail>
{
    public override bool HandleNull => true;

    public override LensingDetail Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return LensingDetails.Parse(reader.GetString());
        reader.Skip();   // a number, an object, an array, true, false or null: not a name
        return LensingDetail.Auto;
    }

    public override void Write(Utf8JsonWriter writer, LensingDetail value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

/// <summary>
/// Reads and writes <c>layout.json</c> in a folder (the app passes <c>%LOCALAPPDATA%\SupernovaLab</c>; tests pass a temporary one).
/// A missing, unreadable or corrupt file gives the defaults and never throws: it must not block startup. A write that fails is
/// ignored (the layout is a convenience).
/// </summary>
public sealed class LayoutStore
{
    public const string FileName = "layout.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LayoutStore(string folder) => Folder = folder ?? throw new ArgumentNullException(nameof(folder));

    public string Folder { get; }

    public string Path => System.IO.Path.Combine(Folder, FileName);

    /// <summary>The app's own folder: %LOCALAPPDATA%\SupernovaLab.</summary>
    public static LayoutStore ForCurrentUser()
        => new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SupernovaLab"));

    public LayoutSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new LayoutSettings();
            return JsonSerializer.Deserialize<LayoutSettings>(File.ReadAllText(Path)) ?? new LayoutSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException
                                      or ArgumentException or InvalidOperationException)
        {
            return new LayoutSettings();
        }
    }

    public void Save(LayoutSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            // Atomic: write a temporary file beside the real one, then move it over, so a crash or a second instance never
            // leaves a half-written layout.json.
            string temp = Path + "." + Environment.ProcessId + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
                File.Move(temp, Path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // Nothing to do: the next change tries again.
        }
    }
}

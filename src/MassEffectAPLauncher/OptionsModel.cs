// A game's player options (from its options schema: groups, options, presets) and the YAML they become.
// The window edits Values; the --write-yaml check uses the same code without a window.
using System.Text;
using System.Text.Json;

namespace APSetup;

sealed class OptionDef
{
    public string Key, Display, Doc, Type, Group;
    public JsonElement Default;
    public string[] Choices = [];
    public int Min, Max;
}

sealed class OptionsModel
{
    public readonly JsonElement Schema;
    public readonly List<OptionDef> Options = [];
    public readonly List<string> Groups = [];
    public readonly List<string> Presets = ["Default"];
    // toggle: bool; choice: string; range: int; set: HashSet<string>
    public readonly Dictionary<string, object> Values = new();

    public OptionsModel(JsonElement schema)
    {
        Schema = schema;
        foreach (var group in schema.GetProperty("groups").EnumerateArray())
        {
            var name = group.GetProperty("name").GetString();
            Groups.Add(name);
            foreach (var o in group.GetProperty("options").EnumerateArray())
            {
                var def = new OptionDef
                {
                    Key = o.GetProperty("key").GetString(), Display = o.GetProperty("display").GetString(), Group = name,
                    Doc = o.GetProperty("doc").GetString(), Type = o.GetProperty("type").GetString(), Default = o.GetProperty("default").Clone(),
                };
                if (o.TryGetProperty("choices", out var choices)) def.Choices = choices.EnumerateArray().Select(c => c.GetString()).ToArray();
                if (def.Type == "range") { def.Min = o.GetProperty("min").GetInt32(); def.Max = o.GetProperty("max").GetInt32(); }
                Options.Add(def);
            }
        }
        foreach (var preset in schema.GetProperty("presets").EnumerateObject()) Presets.Add(preset.Name);
        ApplyPreset("Default");
    }

    // The schema a game's options come from: the downloaded payload's options.json (matches that world version), else the built-in one.
    public static OptionsModel For(GameProfile profile, string payload)
    {
        var file = payload == null ? null : Path.Combine(payload, "options.json");
        if (file != null && File.Exists(file))
            return new OptionsModel(JsonDocument.Parse(File.ReadAllText(file)).RootElement.Clone());
        using var stream = typeof(OptionsModel).Assembly.GetManifestResourceStream(profile.OptionsResource);
        return new OptionsModel(JsonDocument.Parse(stream).RootElement.Clone());
    }

    public string Game => Schema.GetProperty("game").GetString();
    public string WorldVersion => Schema.GetProperty("world_version").GetString();

    object Parse(OptionDef def, JsonElement value) => def.Type switch
    {
        "toggle" => value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.Number && value.GetInt32() != 0),
        "choice" => value.ValueKind == JsonValueKind.String ? value.GetString() : def.Choices[Math.Clamp(value.GetInt32(), 0, def.Choices.Length - 1)],
        "range" => Math.Clamp(value.GetInt32(), def.Min, def.Max),
        _ => value.EnumerateArray().Select(v => v.GetString()).ToHashSet(),
    };

    public void ApplyPreset(string preset)
    {
        foreach (var def in Options) Values[def.Key] = Parse(def, def.Default);
        if (preset != "Default" && Schema.GetProperty("presets").TryGetProperty(preset, out var values))
            foreach (var entry in values.EnumerateObject())
                if (Options.FirstOrDefault(d => d.Key == entry.Name) is { } def) Values[def.Key] = Parse(def, entry.Value);
    }

    static string Quote(string s) => "'" + s.Replace("'", "''") + "'";

    public static string SafeName(string slot)
    {
        var name = string.Concat((slot ?? "").Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        return name.Length > 0 ? name : "Shepard";
    }

    public string BuildYaml(string slot, string preset, string appName, string version)
    {
        var game = Game;
        var yaml = new StringBuilder();
        yaml.AppendLine($"# Written by {appName} {version}. Put it in Archipelago's Players folder and run Generate.");
        yaml.AppendLine($"name: {Quote(slot.Trim().Length > 0 ? slot.Trim() : "Shepard")}");
        yaml.AppendLine($"description: {Quote(appName + ", preset " + preset)}");
        yaml.AppendLine($"game: {Quote(game)}");
        yaml.AppendLine("requires:");
        yaml.AppendLine($"  version: {Schema.GetProperty("minimum_ap_version").GetString()}");
        yaml.AppendLine("  game:");
        yaml.AppendLine($"    {Quote(game)}: {WorldVersion}");
        yaml.AppendLine($"{Quote(game)}:");
        foreach (var def in Options)
        {
            var value = Values[def.Key] switch
            {
                bool b => b ? "'true'" : "'false'",
                string s => Quote(s),
                int i => i.ToString(),
                HashSet<string> set => "[" + string.Join(", ", def.Choices.Where(set.Contains).Select(Quote)) + "]",
                _ => throw new InvalidOperationException(def.Key),
            };
            yaml.AppendLine($"  {def.Key}: {value}");
        }
        return yaml.ToString();
    }
}

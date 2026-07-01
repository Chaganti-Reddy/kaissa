using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaissa.Training;

/// <summary>
/// The set of patterns and scenarios available for training. Loaded from JSON content so the
/// catalogue can grow without code changes.
/// </summary>
public sealed class ScenarioLibrary
{
    private readonly Dictionary<PatternId, Pattern> _patterns;
    private readonly Dictionary<PatternId, List<Scenario>> _byPattern;

    public ScenarioLibrary(IEnumerable<Pattern> patterns, IEnumerable<Scenario> scenarios)
    {
        _patterns = patterns.ToDictionary(p => p.Id);
        _byPattern = new Dictionary<PatternId, List<Scenario>>();

        foreach (var scenario in scenarios)
        {
            if (!_patterns.ContainsKey(scenario.Pattern))
                throw new InvalidOperationException($"Scenario '{scenario.Id}' references unknown pattern '{scenario.Pattern}'.");

            if (!_byPattern.TryGetValue(scenario.Pattern, out var list))
                _byPattern[scenario.Pattern] = list = new List<Scenario>();
            list.Add(scenario);
        }
    }

    /// <summary>All patterns, in a stable order.</summary>
    public IReadOnlyList<PatternId> Patterns => _patterns.Keys.OrderBy(p => p.Value).ToList();

    public Pattern Describe(PatternId id) => _patterns[id];

    public IReadOnlyList<Scenario> ForPattern(PatternId id) =>
        _byPattern.TryGetValue(id, out var list) ? list : Array.Empty<Scenario>();

    public IEnumerable<Scenario> AllScenarios => _byPattern.Values.SelectMany(s => s);

    /// <summary>Loads the bundled default content shipped as an embedded resource.</summary>
    public static ScenarioLibrary LoadDefault()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("scenarios.json", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Bundled scenarios.json is missing.");

        return Load(stream);
    }

    public static ScenarioLibrary Load(Stream json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<ContentDto>(json, options)
            ?? throw new InvalidOperationException("Content could not be parsed.");

        var patterns = dto.Patterns.Select(p => new Pattern(new PatternId(p.Id), p.Name, p.Description));
        var scenarios = dto.Scenarios.Select(s =>
            new Scenario(s.Id, new PatternId(s.Pattern), s.Fen, s.Solutions, s.Prompt));

        return new ScenarioLibrary(patterns, scenarios);
    }

    private sealed class ContentDto
    {
        [JsonPropertyName("patterns")] public List<PatternDto> Patterns { get; init; } = new();
        [JsonPropertyName("scenarios")] public List<ScenarioDto> Scenarios { get; init; } = new();
    }

    private sealed class PatternDto
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
    }

    private sealed class ScenarioDto
    {
        public string Id { get; init; } = "";
        public string Pattern { get; init; } = "";
        public string Fen { get; init; } = "";
        public List<string> Solutions { get; init; } = new();
        public string Prompt { get; init; } = "";
    }
}

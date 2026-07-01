using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaissa.Training.Play;

/// <summary>
/// A local, persistent store of practice positions generated from the player's own games. Deduped
/// by position so the same blunder is not stored twice. Loaded into the training library at start
/// so these positions are scheduled and drilled like any other pattern.
/// </summary>
public sealed class PlayerPracticeStore
{
    private readonly Dictionary<string, Scenario> _byFen = new();

    public IReadOnlyCollection<Scenario> Scenarios => _byFen.Values;

    public int Count => _byFen.Count;

    public void AddRange(IEnumerable<Scenario> scenarios)
    {
        foreach (var scenario in scenarios)
            _byFen[scenario.Fen] = scenario;
    }

    public static PlayerPracticeStore Load(string path)
    {
        var store = new PlayerPracticeStore();
        if (!File.Exists(path))
            return store;

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
            return store;

        var dtos = JsonSerializer.Deserialize<List<ScenarioDto>>(text) ?? new List<ScenarioDto>();
        store.AddRange(dtos.Select(d => new Scenario(
            d.Id, GamePractice.Pattern.Id, d.Fen, d.Solutions, d.Prompt, d.Rating)));
        return store;
    }

    public void Save(string path)
    {
        var dtos = _byFen.Values.Select(s => new ScenarioDto
        {
            Id = s.Id,
            Fen = s.Fen,
            Solutions = s.Solutions.ToList(),
            Prompt = s.Prompt,
            Rating = s.Rating,
        }).ToList();

        File.WriteAllText(path, JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class ScenarioDto
    {
        [JsonPropertyName("id")] public string Id { get; init; } = "";
        [JsonPropertyName("fen")] public string Fen { get; init; } = "";
        [JsonPropertyName("solutions")] public List<string> Solutions { get; init; } = new();
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = "";
        [JsonPropertyName("rating")] public int Rating { get; init; }
    }
}

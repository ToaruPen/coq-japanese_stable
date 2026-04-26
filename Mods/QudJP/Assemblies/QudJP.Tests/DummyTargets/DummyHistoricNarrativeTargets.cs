namespace QudJP.Tests.DummyTargets;

internal sealed class DummyHistoricEntitySnapshot
{
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> ListProperties { get; } = new(StringComparer.Ordinal);

    public string? GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : null;
    public IReadOnlyList<string>? GetList(string name) => ListProperties.TryGetValue(name, out var list) ? list : null;
    public bool HasListProperty(string name) => ListProperties.ContainsKey(name);
}

internal sealed class DummySetEntityPropertyEvent
{
    public required string Name { get; init; }
    public required string Value { get; init; }
}

internal sealed class DummyMutateListPropertyEvent
{
    public required string Name { get; init; }
    public required Func<string, string> Mutation { get; init; }
}

internal sealed class DummyHistoricEntity
{
    private readonly DummyHistoricEntitySnapshot snapshot = new();

    public List<DummySetEntityPropertyEvent> PropertyEvents { get; } = new();
    public List<DummyMutateListPropertyEvent> MutateListEvents { get; } = new();

    public DummyHistoricEntitySnapshot Snapshot => snapshot;

    /// <summary>
    /// Replays recorded PropertyEvents (overwrite) and MutateListEvents (apply mutation
    /// to seeded list elements) on a fresh copy of the seeded snapshot. Mirrors
    /// HistoricEntity.GetCurrentSnapshot semantics for L1/L2 dummy tests.
    /// </summary>
    public DummyHistoricEntitySnapshot GetCurrentSnapshot()
    {
        var fresh = new DummyHistoricEntitySnapshot();
        foreach (var (k, v) in snapshot.Properties) fresh.Properties[k] = v;
        foreach (var (k, list) in snapshot.ListProperties) fresh.ListProperties[k] = new List<string>(list);

        foreach (var ev in PropertyEvents)
        {
            fresh.Properties[ev.Name] = ev.Value;
        }
        foreach (var ev in MutateListEvents)
        {
            if (fresh.ListProperties.TryGetValue(ev.Name, out var existing))
            {
                fresh.ListProperties[ev.Name] = existing.Select(ev.Mutation).ToList();
            }
        }
        return fresh;
    }

    public void SetEntityPropertyAtCurrentYear(string name, string value)
    {
        PropertyEvents.Add(new DummySetEntityPropertyEvent { Name = name, Value = value });
    }

    public void MutateListPropertyAtCurrentYear(string name, Func<string, string> mutation)
    {
        MutateListEvents.Add(new DummyMutateListPropertyEvent { Name = name, Mutation = mutation });
    }

    public void SeedProperty(string name, string value) => snapshot.Properties[name] = value;
    public void SeedList(string name, params string[] items) => snapshot.ListProperties[name] = items.ToList();
}

internal sealed class DummyHistoricEvent
{
    public Dictionary<string, string> EventProperties { get; } = new(StringComparer.Ordinal);
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QudJP.QudTest;

public sealed class QudTestFixtureDocument
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonProperty("suite")]
    public string Suite { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("cases")]
    public List<QudTestCase> Cases { get; } = [];
}

public sealed class QudTestCase
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("route")]
    public string Route { get; set; } = string.Empty;

    [JsonProperty("input")]
    public string Input { get; set; } = string.Empty;

    [JsonProperty("expected")]
    public string Expected { get; set; } = string.Empty;

    [JsonProperty("patch")]
    public string Patch { get; set; } = string.Empty;

    [JsonProperty("expectedTargets")]
    public List<string> ExpectedTargets { get; } = [];
}

public sealed class QudTestRunResult
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("command")]
    public string Command { get; set; } = string.Empty;

    [JsonProperty("suite")]
    public string Suite { get; set; } = string.Empty;

    [JsonProperty("modLanguage")]
    public string ModLanguage { get; set; } = string.Empty;

    [JsonProperty("startedAtUtc")]
    public DateTimeOffset StartedAtUtc { get; set; }

    [JsonProperty("endedAtUtc")]
    public DateTimeOffset EndedAtUtc { get; set; }

    [JsonProperty("passed")]
    public bool Passed { get; set; }

    [JsonProperty("totalCount")]
    public int TotalCount { get; set; }

    [JsonProperty("passCount")]
    public int PassCount { get; set; }

    [JsonProperty("failCount")]
    public int FailCount { get; set; }

    [JsonProperty("cases")]
    public List<QudTestCaseResult> Cases { get; } = [];
}

public sealed class QudTestCaseResult
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("route")]
    public string Route { get; set; } = string.Empty;

    [JsonProperty("input")]
    public string Input { get; set; } = string.Empty;

    [JsonProperty("expected")]
    public string Expected { get; set; } = string.Empty;

    [JsonProperty("actual")]
    public string Actual { get; set; } = string.Empty;

    [JsonProperty("passed")]
    public bool Passed { get; set; }

    [JsonProperty("diagnostic")]
    public string Diagnostic { get; set; } = string.Empty;
}

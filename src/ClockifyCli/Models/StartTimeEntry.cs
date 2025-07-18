using Newtonsoft.Json;

namespace ClockifyCli.Models;

public record StartTimeEntry(
    [property: JsonProperty("start")] string Start,
    [property: JsonProperty("projectId")] string ProjectId,
    [property: JsonProperty("taskId")] string? TaskId,
    [property: JsonProperty("description")]
    string? Description
);

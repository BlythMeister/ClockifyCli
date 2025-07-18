using Newtonsoft.Json;

namespace ClockifyCli.Models;

public record StartTimeEntry(
    [JsonProperty("start")] string Start,
    [JsonProperty("projectId")] string ProjectId,
    [JsonProperty("taskId")] string? TaskId,
    [JsonProperty("description")] string? Description
);
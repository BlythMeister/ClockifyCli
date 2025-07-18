using Newtonsoft.Json;

namespace ClockifyCli.Models;

public record TempoLog(
    [property: JsonProperty("authorAccountId")]
    string AuthorAccountId,
    [property: JsonProperty("description")]
    string Description,
    [property: JsonProperty("issueId")] long IssueId,
    [property: JsonProperty("startDate")] string StartDate,
    [property: JsonProperty("startTime")] string StartTime,
    [property: JsonProperty("timeSpentSeconds")]
    long TimeSpentSeconds,
    [property: JsonProperty("remainingEstimateSeconds")]
    long? RemainingEstimateSeconds);

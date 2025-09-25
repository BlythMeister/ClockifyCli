using Newtonsoft.Json;

namespace ClockifyCli.Models;

public record NewProject([property: JsonProperty("name")] string Name);

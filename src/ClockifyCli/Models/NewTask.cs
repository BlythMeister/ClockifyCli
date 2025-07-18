using Newtonsoft.Json;

namespace ClockifyCli.Models;

public record NewTask([property: JsonProperty("name")] string Name);
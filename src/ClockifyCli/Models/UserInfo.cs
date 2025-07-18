using Newtonsoft.Json;

namespace ClockifyCli.Models;

public record UserInfo(string Id, string Name, string Email, string DefaultWorkspace);
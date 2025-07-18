namespace ClockifyCli.Models;

public record TempoPage<T>(TempoPageMeta MetaData, List<T> Results);
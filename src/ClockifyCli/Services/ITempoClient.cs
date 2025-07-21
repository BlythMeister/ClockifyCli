using ClockifyCli.Models;

namespace ClockifyCli.Services;

public interface ITempoClient
{
    Task Delete(TempoTime tempoTime);
    Task ExportTimeEntry(TimeEntry timeEntry, TaskInfo taskInfo);
    Task<List<TempoTime>> GetCurrentTime(DateTime startDate, DateTime endDate);
    Task<List<TempoApproval>> GetUnsubmittedPeriods();
}

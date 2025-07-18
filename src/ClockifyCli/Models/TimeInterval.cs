namespace ClockifyCli.Models;

public record TimeInterval(string Start, string End)
{
    public DateTime StartDate => DateTime.Parse(Start);
    public DateTime EndDate => DateTime.Parse(End);
    public TimeSpan DurationSpan => EndDate.Subtract(StartDate);
}

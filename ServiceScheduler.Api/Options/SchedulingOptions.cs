namespace ServiceScheduler.Api.Options;

public class SchedulingOptions
{
    public const string Section = "Scheduling";
    public int BufferMinutes { get; set; } = 10;
}
